using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using SilverSet.Models.Entities;
using SilverSet.Models.Entities.Forum;

namespace SilverSet.Controllers;

public class ForumController : Controller
{
    private readonly SilverSetContext _context;
    private readonly UserManager<AppUser> _userManager;

    public ForumController(SilverSetContext context, UserManager<AppUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var categories = await _context.ForumCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .Include(c => c.Topics)
            .ToListAsync();

        return View(categories);
    }

    public async Task<IActionResult> Category(int id, int page = 1)
    {
        int pageSize = 15;
        var category = await _context.ForumCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

        if (category == null) return NotFound();

        var topicsQuery = _context.ForumTopics
            .AsNoTracking()
            .Where(t => t.CategoryId == id)
            .Include(t => t.User)
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
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTopic(int categoryId, string title, string content, string? honeyPot)
    {
        
        if (!string.IsNullOrEmpty(honeyPot)) return Ok();

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
        {
            TempData["Error"] = "Title and Content are required.";
            return RedirectToAction(nameof(CreateTopic), new { categoryId });
        }

        var user = await _userManager.GetUserAsync(User);
        var topic = new ForumTopic
        {
            Title = title.Trim(),
            Content = content.Trim(),
            CategoryId = categoryId,
            UserId = user!.Id,
            CreatedAt = DateTime.UtcNow
        };

        _context.ForumTopics.Add(topic);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Topic), new { id = topic.Id });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePost(int topicId, string content, string? honeyPot)
    {
        if (!string.IsNullOrEmpty(honeyPot)) return Ok();

        if (string.IsNullOrWhiteSpace(content)) return RedirectToAction(nameof(Topic), new { id = topicId });

        var user = await _userManager.GetUserAsync(User);
        var post = new ForumPost
        {
            Content = content.Trim(),
            TopicId = topicId,
            UserId = user!.Id,
            CreatedAt = DateTime.UtcNow
        };

        _context.ForumPosts.Add(post);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Topic), new { id = topicId });
    }
    public async Task<IActionResult> Topic(int id, int page = 1)
    {
        int pageSize = 10;
        var topic = await _context.ForumTopics
            .Include(t => t.User)
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (topic == null) return NotFound();

        topic.ViewCount++;
        await _context.SaveChangesAsync();

        var postsQuery = _context.ForumPosts
            .AsNoTracking()
            .Where(p => p.TopicId == id)
            .Include(p => p.User)
            .OrderBy(p => p.CreatedAt);

        int totalPosts = await postsQuery.CountAsync();
        var posts = await postsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Topic = topic;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalPosts / (double)pageSize);

        return View(posts);
    }

    [Authorize]
    public IActionResult CreateTopic(int categoryId)
    {
        ViewBag.CategoryId = categoryId;
        return View();
    }
}