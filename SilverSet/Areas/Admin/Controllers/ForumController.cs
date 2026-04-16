using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SilverSet.Areas.Admin.Models;
using SilverSet.Data;
using SilverSet.Models.Entities;
using SilverSet.Models.Entities.Forum;
using SilverSet.Models.ViewModels;
using SilverSet.Services;
using System.Text.RegularExpressions;

namespace SilverSet.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ForumController : Controller
{
    private readonly ForumSettingsService _settingsService;
    private readonly SilverSetContext _context;
    private readonly ProfanityFilterService _profanityService;

    public ForumController(ForumSettingsService settingsService, SilverSetContext context, ProfanityFilterService profanityService)
    {
        _settingsService = settingsService;
        _context = context;
        _profanityService = profanityService;
    }

    [HttpGet]
    public async Task<IActionResult> SpamLogs()
    {
        var logs = await _context.ForumSpamLogs
            .Include(l => l.User)
            .Include(l => l.Topic)
                .ThenInclude(t => t.Category)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();
        return View(logs);
    }

    [HttpPost]
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

    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        var settings = await _settingsService.GetAllForumSettingsAsync();

        var model = new ForumSettingsViewModel
        {
            IsForumActive = settings.FirstOrDefault(s => s.Key == "ForumStatus")?.Value.ToLower() == "true",
            PostRestrictionTime = settings.FirstOrDefault(s => s.Key == "ForumPostDelay")?.Value ?? "24h",
            ShadowPostLimit = int.Parse(settings.FirstOrDefault(s => s.Key == "ForumApprovalThreshold")?.Value ?? "5")
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSettings(ForumSettingsViewModel model)
    {
        try
        {
            string newDelay = model.PostRestrictionTime?.Trim().ToLower() ?? "0";

            if (!Regex.IsMatch(newDelay, @"^[0-9]+[hm]?$"))
            {
                TempData["Error"] = "Invalid format.";
                return RedirectToAction(nameof(Settings));
            }

            if (newDelay != "0" && !newDelay.EndsWith("h") && !newDelay.EndsWith("m"))
            {
                newDelay += "h";
            }

            await _settingsService.UpdateSettingAsync("ForumStatus", model.IsForumActive.ToString().ToLower());
            await _settingsService.UpdateSettingAsync("ForumPostDelay", newDelay);
            await _settingsService.UpdateSettingAsync("ForumApprovalThreshold", model.ShadowPostLimit.ToString());

            TempData["Success"] = "Settings updated.";
        }
        catch (Exception)
        {
            TempData["Error"] = "Update failed.";
        }

        return RedirectToAction(nameof(Settings));
    }

    [HttpGet]
    public async Task<IActionResult> Categories(string query = null)
    {
        var categoriesQuery = _context.ForumCategories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
        {
            string search = query.Trim().ToLower();
            categoriesQuery = categoriesQuery.Where(c =>
                c.Name.ToLower().Contains(search) ||
                (c.Description != null && c.Description.ToLower().Contains(search)));

            ViewBag.Query = query;
        }

        var categories = await categoriesQuery.ToListAsync();
        return View(categories);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Name required.";
            return RedirectToAction(nameof(Categories));
        }

        var category = new ForumCategory
        {
            Name = name.Trim(),
            Description = description?.Trim() ?? "",
            IsActive = true
        };

        _context.ForumCategories.Add(category);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Category created.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCategory(int id, string name, string description)
    {
        var category = await _context.ForumCategories.FindAsync(id);
        if (category == null) return NotFound();

        category.Name = name.Trim();
        category.Description = description?.Trim() ?? "";

        await _context.SaveChangesAsync();
        TempData["Success"] = "Category updated.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCategoryStatus(int id)
    {
        var category = await _context.ForumCategories.FindAsync(id);
        if (category != null)
        {
            category.IsActive = !category.IsActive;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.ForumCategories
            .Include(c => c.Topics)
                .ThenInclude(t => t.Posts)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category != null)
        {
            foreach (var topic in category.Topics)
            {
                var postIds = topic.Posts.Select(p => p.Id).ToList();
                var postLogs = await _context.ForumSpamLogs.Where(l => l.PostId != null && postIds.Contains(l.PostId.Value)).ToListAsync();
                var postLogIds = postLogs.Select(l => l.Id).ToList();
                var childPostLogs = await _context.ForumSpamLogs.Where(l => l.RelatedSpamId != null && postLogIds.Contains(l.RelatedSpamId.Value)).ToListAsync();

                _context.ForumSpamLogs.RemoveRange(childPostLogs);
                _context.ForumSpamLogs.RemoveRange(postLogs);

                var topicLogs = await _context.ForumSpamLogs.Where(l => l.TopicId == topic.Id).ToListAsync();
                var topicLogIds = topicLogs.Select(l => l.Id).ToList();
                var childTopicLogs = await _context.ForumSpamLogs.Where(l => l.RelatedSpamId != null && topicLogIds.Contains(l.RelatedSpamId.Value)).ToListAsync();

                _context.ForumSpamLogs.RemoveRange(childTopicLogs);
                _context.ForumSpamLogs.RemoveRange(topicLogs);
                _context.ForumPosts.RemoveRange(topic.Posts);
            }
            _context.ForumTopics.RemoveRange(category.Topics);
            _context.ForumCategories.Remove(category);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Categories));
    }

    [HttpGet]
    public async Task<IActionResult> PendingApprovals()
    {
        var pendingTopics = await _context.ForumTopics.Include(t => t.User).Where(t => !t.IsApproved)
            .Select(t => new PendingItemViewModel { Id = t.Id, Type = "Topic", Title = t.Title, Content = t.Content, UserName = t.User.UserName, CreatedAt = t.CreatedAt }).ToListAsync();

        var pendingPosts = await _context.ForumPosts.Include(p => p.User).Include(p => p.Topic).Where(p => !p.IsApproved)
            .Select(p => new PendingItemViewModel { Id = p.Id, Type = "Post", Title = "Reply: " + p.Topic.Title, Content = p.Content, UserName = p.User.UserName, CreatedAt = p.CreatedAt }).ToListAsync();

        return View(pendingTopics.Concat(pendingPosts).OrderBy(x => x.CreatedAt).ToList());
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ModerationView(int topicId)
    {
        var topic = await _context.ForumTopics.Include(t => t.User).Include(t => t.Category).Include(t => t.Posts).ThenInclude(p => p.User).FirstOrDefaultAsync(t => t.Id == topicId);
        if (topic == null) return NotFound();
        return View(topic);
    }

    [HttpPost]
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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItem(int id, string type)
    {
        int userId = 0;
        if (type == "Topic" || type == "DENETIM" || type == "[YENİ KONU]" || type == "Post" || type == "[CEVAP]")
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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePin(int id)
    {
        var topic = await _context.ForumTopics.FindAsync(id);
        if (topic != null)
        {
            topic.IsPinned = !topic.IsPinned;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction("Topic", "Forum", new { area = "", id = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(int id)
    {
        var topic = await _context.ForumTopics.FindAsync(id);
        if (topic != null)
        {
            topic.IsLocked = !topic.IsLocked;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction("Topic", "Forum", new { area = "", id = id });
    }
}