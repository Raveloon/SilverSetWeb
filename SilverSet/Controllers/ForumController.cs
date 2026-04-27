using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using SilverSet.Models.Entities;
using SilverSet.Models.Entities.Forum;
using SilverSet.Filters;
using SilverSet.Services;
using SilverSet.Models.ViewModels;

namespace SilverSet.Controllers;

[ServiceFilter(typeof(ForumAccessFilter))]
public class ForumController : Controller
{
    private readonly SilverSetContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly ForumSettingsService _settingsService;
    private readonly ProfanityFilterService _profanityService;

    public ForumController(SilverSetContext context, UserManager<AppUser> userManager, ForumSettingsService settingsService, ProfanityFilterService profanityService)
    {
        _context = context;
        _userManager = userManager;
        _settingsService = settingsService;
        _profanityService = profanityService;
    }

    public async Task<IActionResult> Index(string filter = "all", string query = null, int page = 1)
    {
        int pageSize = 24;
        var categoriesQuery = _context.ForumCategories.Where(c => c.IsActive).Include(c => c.Topics.Where(t => t.IsApproved)).ThenInclude(t => t.Posts.Where(p => p.IsApproved)).AsNoTracking();
        var allCategories = await categoriesQuery.ToListAsync();
        var letters = allCategories.Where(c => !string.IsNullOrEmpty(c.Name)).Select(c => char.ToUpper(c.Name[0])).Distinct().OrderBy(l => l).ToList();
        ViewBag.AvailableLetters = letters;
        ViewBag.SelectedFilter = filter;
        ViewBag.Query = query;
        List<ForumCategory> filtered;
        if (!string.IsNullOrWhiteSpace(query))
        {
            string s = query.Trim().ToLower();
            filtered = allCategories.Where(c => c.Name.ToLower().Contains(s)).OrderByDescending(c => c.Topics.SelectMany(t => t.Posts).Count()).ToList();
            ViewBag.IsSearch = true;
        }
        else if (string.IsNullOrEmpty(filter) || filter.ToLower() == "all")
        {
            filtered = allCategories.OrderByDescending(c => c.Topics.SelectMany(t => t.Posts).Count()).ToList();
            ViewBag.IsSearch = false;
        }
        else
        {
            char f = char.ToUpper(filter[0]);
            filtered = allCategories.Where(c => char.ToUpper(c.Name[0]) == f).OrderByDescending(c => c.Topics.SelectMany(t => t.Posts).Count()).ToList();
            ViewBag.IsSearch = false;
        }
        int total = filtered.Count;
        var paged = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        return View(paged);
    }

    public async Task<IActionResult> Category(int id, int page = 1)
    {
        int pageSize = 15;
        var category = await _context.ForumCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.IsActive);
        if (category == null) return NotFound();
        var curId = _userManager.GetUserId(User);
        var tQuery = _context.ForumTopics.AsNoTracking().Where(t => t.CategoryId == id && (t.IsApproved || t.UserId.ToString() == curId)).Include(t => t.User).Include(t => t.Posts.Where(p => p.IsApproved)).OrderByDescending(t => t.IsPinned).ThenByDescending(t => t.CreatedAt);
        int total = await tQuery.CountAsync();
        var topics = await tQuery.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        ViewBag.Category = category;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        return View(topics);
    }

    public async Task<IActionResult> Topic(int id, int page = 1)
    {
        int pageSize = 20;
        var topic = await _context.ForumTopics.Include(t => t.User).Include(t => t.Category).FirstOrDefaultAsync(t => t.Id == id);
        if (topic == null) return NotFound();
        var curId = _userManager.GetUserId(User);
        if (!topic.IsApproved && !User.IsInRole("Admin") && topic.UserId.ToString() != curId) return NotFound();
        topic.ViewCount++;
        await _context.SaveChangesAsync();
        var pQuery = _context.ForumPosts.AsNoTracking().Where(p => p.TopicId == id && (p.IsApproved || p.UserId.ToString() == curId)).Include(p => p.User).OrderBy(p => p.CreatedAt);
        int total = await pQuery.CountAsync();
        var posts = await pQuery.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        ViewBag.TopicStarterId = topic.UserId;
        var model = new TopicDetailViewModel { Topic = topic, Posts = posts, CurrentPage = page, TotalPages = (int)Math.Ceiling(total / (double)pageSize) };
        return View(model);
    }

    [Authorize]
    public IActionResult CreateTopic(int categoryId) { ViewBag.CategoryId = categoryId; return View(); }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTopic(int categoryId, string title, string content, string? honeyPot)
    {
        if (!string.IsNullOrEmpty(honeyPot)) return RedirectToAction(nameof(Index));
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content)) { return RedirectToAction(nameof(CreateTopic), new { categoryId }); }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        bool isAdmin = User.IsInRole("Admin");

        var (isSpam, caughtWord, isInTitle) = await _profanityService.DetectSpam(title, content);

        if (isSpam && !isAdmin && !user.ForcePostApproval)
        {
            user.ForcePostApproval = true;
            await _userManager.UpdateAsync(user);
        }

        bool forceApproval = !isAdmin && (isSpam || user.ForcePostApproval);

        var topic = new ForumTopic
        {
            Title = title.Trim(),
            Content = content.Trim(),
            CategoryId = categoryId,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            IsApproved = isAdmin && !isSpam
        };

        if (!isAdmin && !forceApproval)
        {
            var threshold = await _settingsService.GetShadowPostLimit();
            var approvedCount = await _context.ForumPosts.CountAsync(p => p.UserId == user.Id && p.IsApproved) + await _context.ForumTopics.CountAsync(t => t.UserId == user.Id && t.IsApproved);
            topic.IsApproved = threshold == 0 || approvedCount >= threshold;
        }

        _context.ForumTopics.Add(topic);
        await _context.SaveChangesAsync();

        var activeSpamId = await _profanityService.GetActiveSpamIdForUser(user.Id, null);

        if (isSpam)
        {
            string logType = isInTitle ? "TopicTitle" : "Topic";
            await _profanityService.LogSpam(user.Id, logType, content, caughtWord, topic.Id, null, activeSpamId);
        }
        else if (user.ForcePostApproval && !isAdmin)
        {
            await _profanityService.LogSpam(user.Id, "Restricted", content, "DENETIM", topic.Id, null, activeSpamId);
        }

        return topic.IsApproved ? RedirectToAction(nameof(Topic), new { id = topic.Id }) : RedirectToAction(nameof(Category), new { id = categoryId });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePost(int topicId, string content, int? parentPostId, string? honeyPot)
    {
        if (!string.IsNullOrEmpty(honeyPot)) return RedirectToAction(nameof(Index));
        var topic = await _context.ForumTopics.FindAsync(topicId);
        if (topic == null || topic.IsLocked) return BadRequest();

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        bool isAdmin = User.IsInRole("Admin");

        var (isSpam, caughtWord, _) = await _profanityService.DetectSpam(string.Empty, content);

        if (isSpam && !isAdmin && !user.ForcePostApproval)
        {
            user.ForcePostApproval = true;
            await _userManager.UpdateAsync(user);
        }

        bool forceApproval = !isAdmin && (isSpam || user.ForcePostApproval);

        var post = new ForumPost
        {
            Content = content.Trim(),
            TopicId = topicId,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            IsApproved = isAdmin && !isSpam,
            ParentPostId = parentPostId
        };

        if (!isAdmin && !forceApproval)
        {
            var threshold = await _settingsService.GetShadowPostLimit();
            var approvedCount = await _context.ForumPosts.CountAsync(p => p.UserId == user.Id && p.IsApproved) + await _context.ForumTopics.CountAsync(t => t.UserId == user.Id && t.IsApproved);
            post.IsApproved = threshold == 0 || approvedCount >= threshold;
        }

        _context.ForumPosts.Add(post);
        await _context.SaveChangesAsync();

        var activeSpamId = await _profanityService.GetActiveSpamIdForUser(user.Id, topicId);

        if (isSpam)
        {
            await _profanityService.LogSpam(user.Id, "Post", content, caughtWord, topicId, post.Id, activeSpamId);
        }
        else if (user.ForcePostApproval && !isAdmin)
        {
            await _profanityService.LogSpam(user.Id, "Restricted", content, "DENETIM", topicId, post.Id, activeSpamId);
        }

        var totalPosts = await _context.ForumPosts.CountAsync(p => p.TopicId == topicId && (p.IsApproved || p.UserId == user.Id));
        int targetPage = (int)Math.Ceiling(totalPosts / 20.0);
        return RedirectToAction(nameof(Topic), new { id = topicId, page = targetPage < 1 ? 1 : targetPage });
    }

    public IActionResult Maintenance() { return View(); }
}