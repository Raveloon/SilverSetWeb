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
        var categoriesQuery = _context.ForumCategories
            .Where(c => c.IsActive)
            .Include(c => c.Topics.Where(t => t.IsApproved))
                .ThenInclude(t => t.Posts.Where(p => p.IsApproved))
            .AsNoTracking();

        var allCategories = await categoriesQuery.ToListAsync();

        var availableLetters = allCategories
            .Where(c => !string.IsNullOrEmpty(c.Name))
            .Select(c => char.ToUpper(c.Name[0]))
            .Distinct()
            .OrderBy(l => l)
            .ToList();

        ViewBag.AvailableLetters = availableLetters;
        ViewBag.SelectedFilter = filter;
        ViewBag.Query = query;

        List<ForumCategory> filteredCategories;

        if (!string.IsNullOrWhiteSpace(query))
        {
            string search = query.Trim().ToLower();
            filteredCategories = allCategories
                .Where(c => c.Name.ToLower().Contains(search))
                .OrderByDescending(c => c.Topics.SelectMany(t => t.Posts).Count())
                .ToList();
            ViewBag.IsSearch = true;
        }
        else if (string.IsNullOrEmpty(filter) || filter.ToLower() == "all")
        {
            filteredCategories = allCategories
                .OrderByDescending(c => c.Topics.SelectMany(t => t.Posts).Count())
                .ToList();
            ViewBag.IsSearch = false;
        }
        else
        {
            char filterChar = char.ToUpper(filter[0]);
            filteredCategories = allCategories
                .Where(c => char.ToUpper(c.Name[0]) == filterChar)
                .OrderByDescending(c => c.Topics.SelectMany(t => t.Posts).Count())
                .ToList();
            ViewBag.IsSearch = false;
        }

        int totalCategories = filteredCategories.Count;
        var pagedCategories = filteredCategories
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCategories / (double)pageSize);

        return View(pagedCategories);
    }

    public async Task<IActionResult> Category(int id, int page = 1)
    {
        int pageSize = 15;
        var category = await _context.ForumCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

        if (category == null) return NotFound();

        var currentUserId = _userManager.GetUserId(User);

        var topicsQuery = _context.ForumTopics
            .AsNoTracking()
            .Where(t => t.CategoryId == id && (t.IsApproved || t.UserId.ToString() == currentUserId))
            .Include(t => t.User)
            .Include(t => t.Posts.Where(p => p.IsApproved))
            .OrderByDescending(t => t.IsPinned)
            .ThenByDescending(t => t.CreatedAt);

        int totalTopics = await topicsQuery.CountAsync();
        var topics = await topicsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Category = category;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalTopics / (double)pageSize);

        return View(topics);
    }

    public async Task<IActionResult> Topic(int id, int page = 1)
    {
        int pageSize = 20;
        var topic = await _context.ForumTopics
            .Include(t => t.User)
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (topic == null) return NotFound();

        var currentUserId = _userManager.GetUserId(User);

        if (!topic.IsApproved && !User.IsInRole("Admin") && topic.UserId.ToString() != currentUserId)
        {
            return NotFound();
        }

        topic.ViewCount++;
        await _context.SaveChangesAsync();

        var postsQuery = _context.ForumPosts
            .AsNoTracking()
            .Where(p => p.TopicId == id && (p.IsApproved || p.UserId.ToString() == currentUserId))
            .Include(p => p.User)
            .OrderBy(p => p.CreatedAt);

        int totalPosts = await postsQuery.CountAsync();
        var posts = await postsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.TopicStarterId = topic.UserId;

        var model = new TopicDetailViewModel
        {
            Topic = topic,
            Posts = posts,
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling(totalPosts / (double)pageSize)
        };

        return View(model);
    }

    [Authorize]
    public IActionResult CreateTopic(int categoryId)
    {
        ViewBag.CategoryId = categoryId;
        return View();
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTopic(int categoryId, string title, string content, string? honeyPot)
    {
        if (!string.IsNullOrEmpty(honeyPot)) return RedirectToAction(nameof(Index));

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
        {
            TempData["DraftTitle"] = title;
            TempData["DraftContent"] = content;
            return RedirectToAction(nameof(CreateTopic), new { categoryId });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var spamCheck = await _profanityService.DetectSpam(title, content);
        title = await _profanityService.FilterText(title);
        content = await _profanityService.FilterText(content);

        var activeSpamId = await _profanityService.GetActiveSpamIdForUser(user.Id, null);
        bool forceApproval = user.ForcePostApproval || spamCheck.IsSpam;

        if (spamCheck.IsSpam && !user.ForcePostApproval)
        {
            user.ForcePostApproval = true;
            await _userManager.UpdateAsync(user);
        }

        var topic = new ForumTopic
        {
            Title = title.Trim(),
            Content = content.Trim(),
            CategoryId = categoryId,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            IsApproved = !forceApproval && User.IsInRole("Admin")
        };

        if (!forceApproval)
        {
            var threshold = await _settingsService.GetShadowPostLimit();
            var approvedCount = await _context.ForumPosts.CountAsync(p => p.UserId == user.Id && p.IsApproved)
                              + await _context.ForumTopics.CountAsync(t => t.UserId == user.Id && t.IsApproved);
            topic.IsApproved = User.IsInRole("Admin") || threshold == 0 || approvedCount >= threshold;
        }

        _context.ForumTopics.Add(topic);
        await _context.SaveChangesAsync();

        if (spamCheck.IsSpam)
        {
            await _profanityService.LogSpam(user.Id, "Topic", content, spamCheck.Reason, topic.Id, null, activeSpamId);
        }
        else if (user.ForcePostApproval)
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

        var spamCheck = await _profanityService.DetectSpam(string.Empty, content);
        content = await _profanityService.FilterText(content);

        var activeSpamId = await _profanityService.GetActiveSpamIdForUser(user.Id, topicId);
        bool forceApproval = user.ForcePostApproval || spamCheck.IsSpam;

        if (spamCheck.IsSpam && !user.ForcePostApproval)
        {
            user.ForcePostApproval = true;
            await _userManager.UpdateAsync(user);
        }

        var post = new ForumPost
        {
            Content = content.Trim(),
            TopicId = topicId,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            IsApproved = !forceApproval && User.IsInRole("Admin")
        };

        if (!forceApproval)
        {
            var threshold = await _settingsService.GetShadowPostLimit();
            var approvedCount = await _context.ForumPosts.CountAsync(p => p.UserId == user.Id && p.IsApproved)
                              + await _context.ForumTopics.CountAsync(t => t.UserId == user.Id && t.IsApproved);
            post.IsApproved = User.IsInRole("Admin") || threshold == 0 || approvedCount >= threshold;
        }

        _context.ForumPosts.Add(post);
        await _context.SaveChangesAsync();

        if (spamCheck.IsSpam)
        {
            await _profanityService.LogSpam(user.Id, "Post", content, spamCheck.Reason, topicId, post.Id, activeSpamId);
        }
        else if (user.ForcePostApproval)
        {
            await _profanityService.LogSpam(user.Id, "Restricted", content, "DENETIM", topicId, post.Id, activeSpamId);
        }

        var totalPosts = await _context.ForumPosts.CountAsync(p => p.TopicId == topicId && (p.IsApproved || p.UserId == user.Id));
        int targetPage = (int)Math.Ceiling(totalPosts / 20.0);
        return RedirectToAction(nameof(Topic), new { id = topicId, page = targetPage });
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> PendingApprovals()
    {
        var pendingTopics = await _context.ForumTopics
            .Include(t => t.User)
            .Where(t => !t.IsApproved)
            .Select(t => new PendingItemViewModel { Id = t.Id, Type = "Topic", Content = t.Content, Title = t.Title, UserName = t.User.UserName, CreatedAt = t.CreatedAt })
            .ToListAsync();

        var pendingPosts = await _context.ForumPosts
            .Include(p => p.User)
            .Include(p => p.Topic)
            .Where(p => !p.IsApproved)
            .Select(p => new PendingItemViewModel { Id = p.Id, Type = "Post", Content = p.Content, Title = "Reply to: " + p.Topic.Title, UserName = p.User.UserName, CreatedAt = p.CreatedAt })
            .ToListAsync();

        return View(pendingTopics.Concat(pendingPosts).OrderBy(x => x.CreatedAt).ToList());
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SpamLogs()
    {
        var logs = await _context.ForumSpamLogs.Include(l => l.User).Include(l => l.Topic).ThenInclude(t => t.Category).OrderByDescending(l => l.CreatedAt).ToListAsync();
        return View(logs);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ModerationView(int topicId)
    {
        var topic = await _context.ForumTopics.Include(t => t.User).Include(t => t.Category).Include(t => t.Posts).ThenInclude(p => p.User).FirstOrDefaultAsync(t => t.Id == topicId);
        if (topic == null) return NotFound();
        return View(topic);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBadWord(string word)
    {
        if (!string.IsNullOrWhiteSpace(word))
        {
            var cleanWord = word.Trim().ToLower();
            if (!await _context.BadWords.AnyAsync(b => b.Word == cleanWord))
            {
                _context.BadWords.Add(new BadWord { Word = cleanWord });
                await _context.SaveChangesAsync();
                _profanityService.ClearCache();
            }
        }
        return Ok();
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveItem(int id, string type)
    {
        int userId = 0;
        if (type == "Topic" || type == "[YENİ KONU]")
        {
            var topic = await _context.ForumTopics.FindAsync(id);
            if (topic != null)
            {
                topic.IsApproved = true;
                userId = topic.UserId;
                var logs = await _context.ForumSpamLogs.Where(l => l.TopicId == id || l.RelatedSpamId == id).ToListAsync();
                foreach (var l in logs) l.IsHandled = true;
            }
        }
        else
        {
            var post = await _context.ForumPosts.FindAsync(id);
            if (post != null)
            {
                post.IsApproved = true;
                userId = post.UserId;
                var logs = await _context.ForumSpamLogs.Where(l => l.PostId == id || l.RelatedSpamId == id).ToListAsync();
                foreach (var l in logs) l.IsHandled = true;
            }
        }

        await _context.SaveChangesAsync();

        if (userId != 0)
        {
            var hasPending = await _context.ForumSpamLogs.AnyAsync(l => l.UserId == userId && !l.IsHandled);
            if (!hasPending)
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.ForcePostApproval = false;
                    await _context.SaveChangesAsync();
                }
            }
        }

        return RedirectToAction(nameof(SpamLogs));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItem(int id, string type)
    {
        int userId = 0;
        if (type == "Topic" || type == "DENETIM" || type == "[YENİ KONU]")
        {
            var topic = await _context.ForumTopics.Include(t => t.Posts).FirstOrDefaultAsync(t => t.Id == id);
            if (topic != null)
            {
                userId = topic.UserId;
                var postIds = topic.Posts.Select(p => p.Id).ToList();
                var topicLogs = await _context.ForumSpamLogs.Where(l => l.TopicId == id || (l.PostId != null && postIds.Contains(l.PostId.Value))).ToListAsync();
                var topicLogIds = topicLogs.Select(l => l.Id).ToList();
                var childLogs = await _context.ForumSpamLogs.Where(l => l.RelatedSpamId != null && topicLogIds.Contains(l.RelatedSpamId.Value)).ToListAsync();

                _context.ForumSpamLogs.RemoveRange(childLogs);
                _context.ForumSpamLogs.RemoveRange(topicLogs);
                _context.ForumPosts.RemoveRange(topic.Posts);
                _context.ForumTopics.Remove(topic);
            }
            else
            {
                var fallback = await _context.ForumSpamLogs.FirstOrDefaultAsync(l => (l.TopicId == id || l.PostId == id) && l.RelatedSpamId == null);
                if (fallback != null)
                {
                    userId = fallback.UserId;
                    var related = await _context.ForumSpamLogs.Where(l => l.RelatedSpamId == fallback.Id).ToListAsync();
                    _context.ForumSpamLogs.RemoveRange(related);
                    _context.ForumSpamLogs.Remove(fallback);
                }
            }
        }
        else
        {
            var post = await _context.ForumPosts.FindAsync(id);
            if (post != null)
            {
                userId = post.UserId;
                var postLogs = await _context.ForumSpamLogs.Where(l => l.PostId == id).ToListAsync();
                var postLogIds = postLogs.Select(l => l.Id).ToList();
                var childLogs = await _context.ForumSpamLogs.Where(l => l.RelatedSpamId != null && postLogIds.Contains(l.RelatedSpamId.Value)).ToListAsync();

                _context.ForumSpamLogs.RemoveRange(childLogs);
                _context.ForumSpamLogs.RemoveRange(postLogs);
                _context.ForumPosts.Remove(post);
            }
            else
            {
                var fallback = await _context.ForumSpamLogs.FirstOrDefaultAsync(l => l.PostId == id);
                if (fallback != null)
                {
                    userId = fallback.UserId;
                    var related = await _context.ForumSpamLogs.Where(l => l.RelatedSpamId == fallback.Id).ToListAsync();
                    _context.ForumSpamLogs.RemoveRange(related);
                    _context.ForumSpamLogs.Remove(fallback);
                }
            }
        }

        await _context.SaveChangesAsync();

        if (userId != 0)
        {
            var hasPending = await _context.ForumSpamLogs.AnyAsync(l => l.UserId == userId && !l.IsHandled);
            if (!hasPending)
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.ForcePostApproval = false;
                    await _context.SaveChangesAsync();
                }
            }
        }

        return RedirectToAction(nameof(SpamLogs));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ManageCategories()
    {
        var categories = await _context.ForumCategories.OrderBy(c => c.Name).ToListAsync();
        return View(categories);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(string name, string description)
    {
        var category = new ForumCategory { Name = name.Trim(), Description = description?.Trim(), IsActive = true };
        _context.ForumCategories.Add(category);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(ManageCategories));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCategory(int id, string name, string description)
    {
        var category = await _context.ForumCategories.FindAsync(id);
        if (category == null) return NotFound();
        category.Name = name.Trim();
        category.Description = description?.Trim();
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(ManageCategories));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCategoryStatus(int id)
    {
        var category = await _context.ForumCategories.FindAsync(id);
        if (category == null) return NotFound();
        category.IsActive = !category.IsActive;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(ManageCategories));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.ForumCategories.Include(c => c.Topics).ThenInclude(t => t.Posts).FirstOrDefaultAsync(c => c.Id == id);
        if (category == null) return NotFound();
        foreach (var topic in category.Topics) { _context.ForumPosts.RemoveRange(topic.Posts); }
        _context.ForumTopics.RemoveRange(category.Topics);
        _context.ForumCategories.Remove(category);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(ManageCategories));
    }

    public IActionResult Maintenance() { return View(); }
}