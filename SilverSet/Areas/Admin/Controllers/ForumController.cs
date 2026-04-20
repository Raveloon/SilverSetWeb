using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    private readonly UserManager<AppUser> _userManager;

    public ForumController(ForumSettingsService settingsService, SilverSetContext context, ProfanityFilterService profanityService, UserManager<AppUser> userManager)
    {
        _settingsService = settingsService;
        _context = context;
        _profanityService = profanityService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> SpamLogs()
    {
        var logs = await _context.ForumSpamLogs
            .Include(l => l.User)
            .Include(l => l.Topic).ThenInclude(t => t.Category)
            .Include(l => l.Topic).ThenInclude(t => t.Posts)
            .Where(l => !l.IsHandled)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync();

        return View(logs);
    }

    [HttpGet]
    [ActionName("PendingApprovals")]
    public async Task<IActionResult> PendingApprovals()
    {
        var spamPostIds = await _context.ForumSpamLogs.Where(l => l.PostId != null && !l.IsHandled).Select(l => l.PostId).ToListAsync();
        var spamTopicIds = await _context.ForumSpamLogs.Where(l => l.PostId == null && !l.IsHandled).Select(l => l.TopicId).ToListAsync();

        var pendingTopics = await _context.ForumTopics.Include(t => t.User)
            .Where(t => !t.IsApproved && !spamTopicIds.Contains(t.Id))
            .Select(t => new PendingItemViewModel { Id = t.Id, Type = "Topic", Title = t.Title, Content = t.Content, UserName = t.User.UserName, CreatedAt = t.CreatedAt })
            .ToListAsync();

        var pendingPosts = await _context.ForumPosts.Include(p => p.User).Include(p => p.Topic)
            .Where(p => !p.IsApproved && !spamPostIds.Contains(p.Id))
            .Select(p => new PendingItemViewModel { Id = p.Id, Type = "Post", Title = "Reply: " + p.Topic.Title, Content = p.Content, UserName = p.User.UserName, CreatedAt = p.CreatedAt })
            .ToListAsync();

        return View(pendingTopics.Concat(pendingPosts).OrderBy(x => x.CreatedAt).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> PendingApproval() => await PendingApprovals();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveItem(int id, string type)
    {
        int userId = 0;
        if (type == "Topic" || type == "[YENİ KONU]" || type == "TopicTitle")
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
        if (userId != 0) await CheckAndReleaseUserStatus(userId);

        return RedirectToAction(nameof(SpamLogs));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItem(int id, string type)
    {
        int userId = 0;
        if (type == "Topic" || type == "DENETIM" || type == "[YENİ KONU]" || type == "TopicTitle")
        {
            var topic = await _context.ForumTopics.Include(t => t.Posts).FirstOrDefaultAsync(t => t.Id == id);
            if (topic != null)
            {
                userId = topic.UserId;
                var postIds = topic.Posts.Select(p => p.Id).ToList();

                await _context.ForumPosts
                    .Where(p => p.ParentPostId.HasValue && postIds.Contains(p.ParentPostId.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.ParentPostId, (int?)null));

                var logs = await _context.ForumSpamLogs.Where(l => l.TopicId == id || (l.PostId.HasValue && postIds.Contains(l.PostId.Value))).ToListAsync();

                _context.ForumSpamLogs.RemoveRange(logs.Where(l => l.RelatedSpamId.HasValue));
                await _context.SaveChangesAsync();

                _context.ForumSpamLogs.RemoveRange(logs.Where(l => !l.RelatedSpamId.HasValue));
                _context.ForumPosts.RemoveRange(topic.Posts);
                _context.ForumTopics.Remove(topic);
            }
        }
        else
        {
            var post = await _context.ForumPosts.FindAsync(id);
            if (post != null)
            {
                userId = post.UserId;

                await _context.ForumPosts
                    .Where(p => p.ParentPostId == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.ParentPostId, (int?)null));

                var postLogs = await _context.ForumSpamLogs.Where(l => l.PostId == id).ToListAsync();
                _context.ForumSpamLogs.RemoveRange(postLogs.Where(l => l.RelatedSpamId.HasValue));
                await _context.SaveChangesAsync();

                _context.ForumSpamLogs.RemoveRange(postLogs.Where(l => !l.RelatedSpamId.HasValue));
                _context.ForumPosts.Remove(post);
            }
        }

        await _context.SaveChangesAsync();
        if (userId != 0) await CheckAndReleaseUserStatus(userId);

        return RedirectToAction(nameof(SpamLogs));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveTopicLogs(int topicId, int userId)
    {
        var logs = await _context.ForumSpamLogs.Where(l => l.TopicId == topicId && l.UserId == userId && !l.IsHandled).ToListAsync();
        var posts = await _context.ForumPosts.Where(p => p.TopicId == topicId && p.UserId == userId && !p.IsApproved).ToListAsync();
        var topic = await _context.ForumTopics.FirstOrDefaultAsync(t => t.Id == topicId && t.UserId == userId && !t.IsApproved);

        if (topic != null) topic.IsApproved = true;
        foreach (var p in posts) p.IsApproved = true;
        foreach (var l in logs) l.IsHandled = true;

        await _context.SaveChangesAsync();
        await CheckAndReleaseUserStatus(userId);
        return RedirectToAction(nameof(SpamLogs));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTopicLogs(int topicId, int userId)
    {
        var topic = await _context.ForumTopics.FindAsync(topicId);
        if (topic == null) return RedirectToAction(nameof(SpamLogs));

        if (topic.UserId == userId)
        {
            return await DeleteItem(topicId, "Topic");
        }

        var firstViolationDate = await _context.ForumSpamLogs
            .Where(l => l.TopicId == topicId && l.UserId == userId && !l.IsHandled)
            .OrderBy(l => l.CreatedAt)
            .Select(l => l.CreatedAt)
            .FirstOrDefaultAsync();

        if (firstViolationDate == default) return RedirectToAction(nameof(SpamLogs));

        var postsToDelete = await _context.ForumPosts
            .Where(p => p.TopicId == topicId && p.UserId == userId && p.CreatedAt >= firstViolationDate)
            .ToListAsync();

        var postIds = postsToDelete.Select(p => p.Id).ToList();

        await _context.ForumPosts
            .Where(p => p.ParentPostId.HasValue && postIds.Contains(p.ParentPostId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ParentPostId, (int?)null));

        var logs = await _context.ForumSpamLogs.Where(l => l.TopicId == topicId && l.UserId == userId).ToListAsync();

        _context.ForumSpamLogs.RemoveRange(logs.Where(l => l.RelatedSpamId.HasValue));
        await _context.SaveChangesAsync();

        _context.ForumSpamLogs.RemoveRange(logs.Where(l => !l.RelatedSpamId.HasValue));
        _context.ForumPosts.RemoveRange(postsToDelete);

        await _context.SaveChangesAsync();
        await CheckAndReleaseUserStatus(userId);
        return RedirectToAction(nameof(SpamLogs));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveAllUserLogs(int userId)
    {
        var logs = await _context.ForumSpamLogs.Where(l => l.UserId == userId && !l.IsHandled).ToListAsync();
        var posts = await _context.ForumPosts.Where(p => p.UserId == userId && !p.IsApproved).ToListAsync();
        var topics = await _context.ForumTopics.Where(t => t.UserId == userId && !t.IsApproved).ToListAsync();

        foreach (var l in logs) l.IsHandled = true;
        foreach (var p in posts) p.IsApproved = true;
        foreach (var t in topics) t.IsApproved = true;

        await _context.SaveChangesAsync();
        await CheckAndReleaseUserStatus(userId);
        return RedirectToAction(nameof(SpamLogs));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAllUserLogs(int userId)
    {
        var postsToDelete = await _context.ForumPosts.Where(p => p.UserId == userId).ToListAsync();
        var postIds = postsToDelete.Select(p => p.Id).ToList();

        await _context.ForumPosts
            .Where(p => p.ParentPostId.HasValue && postIds.Contains(p.ParentPostId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ParentPostId, (int?)null));

        var logs = await _context.ForumSpamLogs.Where(l => l.UserId == userId).ToListAsync();
        var topics = await _context.ForumTopics.Where(t => t.UserId == userId).ToListAsync();

        _context.ForumSpamLogs.RemoveRange(logs.Where(l => l.RelatedSpamId.HasValue));
        await _context.SaveChangesAsync();

        _context.ForumSpamLogs.RemoveRange(logs.Where(l => !l.RelatedSpamId.HasValue));
        _context.ForumPosts.RemoveRange(postsToDelete);
        _context.ForumTopics.RemoveRange(topics);

        await _context.SaveChangesAsync();
        await CheckAndReleaseUserStatus(userId);
        return RedirectToAction(nameof(SpamLogs));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BanUserFromForum(int userId, string banReason)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return NotFound();

        user.IsBanned = true;
        user.BanReason = banReason?.Trim() ?? "Moderasyon İhlali";
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.ForcePostApproval = true;

        var posts = await _context.ForumPosts.Where(p => p.UserId == userId).ToListAsync();
        var postIds = posts.Select(p => p.Id).ToList();

        await _context.ForumPosts
            .Where(p => p.ParentPostId.HasValue && postIds.Contains(p.ParentPostId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ParentPostId, (int?)null));

        var topics = await _context.ForumTopics.Where(t => t.UserId == userId).ToListAsync();
        foreach (var p in posts) p.IsApproved = false;
        foreach (var t in topics) t.IsApproved = false;

        var logs = await _context.ForumSpamLogs.Where(l => l.UserId == userId).ToListAsync();
        _context.ForumSpamLogs.RemoveRange(logs.Where(l => l.RelatedSpamId.HasValue));
        await _context.SaveChangesAsync();
        _context.ForumSpamLogs.RemoveRange(logs.Where(l => !l.RelatedSpamId.HasValue));

        await _userManager.UpdateAsync(user);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Operative {user.UserName} has been permanently suspended.";
        return RedirectToAction(nameof(SpamLogs));
    }

    [HttpGet]
    public async Task<IActionResult> ModerationView(int topicId)
    {
        var topic = await _context.ForumTopics.Include(t => t.User).Include(t => t.Category).Include(t => t.Posts).ThenInclude(p => p.User).FirstOrDefaultAsync(t => t.Id == topicId);
        if (topic == null) return NotFound();
        return View(topic);
    }

    private async Task CheckAndReleaseUserStatus(int userId)
    {
        bool hasActiveViolations = await _context.ForumSpamLogs.AnyAsync(l => l.UserId == userId && !l.IsHandled && l.CaughtWord != null);
        if (!hasActiveViolations)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null && user.ForcePostApproval)
            {
                user.ForcePostApproval = false;
                await _context.SaveChangesAsync();
            }
        }
    }

    [HttpGet] public async Task<IActionResult> Settings() { var settings = await _settingsService.GetAllForumSettingsAsync(); var model = new ForumSettingsViewModel { IsForumActive = settings.FirstOrDefault(s => s.Key == "ForumStatus")?.Value.ToLower() == "true", PostRestrictionTime = settings.FirstOrDefault(s => s.Key == "ForumPostDelay")?.Value ?? "24h", ShadowPostLimit = int.Parse(settings.FirstOrDefault(s => s.Key == "ForumApprovalThreshold")?.Value ?? "5") }; return View(model); }
    [HttpPost][ValidateAntiForgeryToken] public async Task<IActionResult> UpdateSettings(ForumSettingsViewModel model) { try { string newDelay = model.PostRestrictionTime?.Trim().ToLower() ?? "0"; if (!Regex.IsMatch(newDelay, @"^[0-9]+[hm]?$")) { TempData["Error"] = "Invalid format."; return RedirectToAction(nameof(Settings)); } if (newDelay != "0" && !newDelay.EndsWith("h") && !newDelay.EndsWith("m")) { newDelay += "h"; } await _settingsService.UpdateSettingAsync("ForumStatus", model.IsForumActive.ToString().ToLower()); await _settingsService.UpdateSettingAsync("ForumPostDelay", newDelay); await _settingsService.UpdateSettingAsync("ForumApprovalThreshold", model.ShadowPostLimit.ToString()); TempData["Success"] = "Settings updated."; } catch (Exception) { TempData["Error"] = "Update failed."; } return RedirectToAction(nameof(Settings)); }
    [HttpGet] public async Task<IActionResult> Categories(string query = null) { var categoriesQuery = _context.ForumCategories.AsNoTracking(); if (!string.IsNullOrWhiteSpace(query)) { string search = query.Trim().ToLower(); categoriesQuery = categoriesQuery.Where(c => c.Name.ToLower().Contains(search) || (c.Description != null && c.Description.ToLower().Contains(search))); ViewBag.Query = query; } var categories = await categoriesQuery.ToListAsync(); return View(categories); }
    [HttpPost][ValidateAntiForgeryToken] public async Task<IActionResult> CreateCategory(string name, string description) { if (string.IsNullOrWhiteSpace(name)) { TempData["Error"] = "Name required."; return RedirectToAction(nameof(Categories)); } var category = new ForumCategory { Name = name.Trim(), Description = description?.Trim() ?? "", IsActive = true }; _context.ForumCategories.Add(category); await _context.SaveChangesAsync(); TempData["Success"] = "Category created."; return RedirectToAction(nameof(Categories)); }
    [HttpPost][ValidateAntiForgeryToken] public async Task<IActionResult> UpdateCategory(int id, string name, string description) { var category = await _context.ForumCategories.FindAsync(id); if (category == null) return NotFound(); category.Name = name.Trim(); category.Description = description?.Trim() ?? ""; await _context.SaveChangesAsync(); TempData["Success"] = "Category updated."; return RedirectToAction(nameof(Categories)); }
    [HttpPost][ValidateAntiForgeryToken] public async Task<IActionResult> ToggleCategoryStatus(int id) { var category = await _context.ForumCategories.FindAsync(id); if (category != null) { category.IsActive = !category.IsActive; await _context.SaveChangesAsync(); } return RedirectToAction(nameof(Categories)); }
    [HttpPost][ValidateAntiForgeryToken] public async Task<IActionResult> DeleteCategory(int id) { var category = await _context.ForumCategories.Include(c => c.Topics).ThenInclude(t => t.Posts).FirstOrDefaultAsync(c => c.Id == id); if (category != null) { foreach (var topic in category.Topics) { var postIds = topic.Posts.Select(p => p.Id).ToList(); var postLogs = await _context.ForumSpamLogs.Where(l => l.PostId != null && postIds.Contains(l.PostId.Value)).ToListAsync(); _context.ForumSpamLogs.RemoveRange(postLogs); _context.ForumPosts.RemoveRange(topic.Posts); } _context.ForumTopics.RemoveRange(category.Topics); _context.ForumCategories.Remove(category); await _context.SaveChangesAsync(); } return RedirectToAction(nameof(Categories)); }
    [HttpPost][ValidateAntiForgeryToken] public async Task<IActionResult> AddBadWord(string word) { if (!string.IsNullOrWhiteSpace(word)) { var cleanWord = word.Trim().ToLower(); if (!await _context.BadWords.AnyAsync(b => b.Word == cleanWord)) { _context.BadWords.Add(new BadWord { Word = cleanWord }); await _context.SaveChangesAsync(); _profanityService.ClearCache(); } } return Ok(); }
    [HttpPost][ValidateAntiForgeryToken] public async Task<IActionResult> TogglePin(int id) { var topic = await _context.ForumTopics.FindAsync(id); if (topic != null) { topic.IsPinned = !topic.IsPinned; await _context.SaveChangesAsync(); } return RedirectToAction("Topic", "Forum", new { area = "", id = id }); }
    [HttpPost][ValidateAntiForgeryToken] public async Task<IActionResult> ToggleLock(int id) { var topic = await _context.ForumTopics.FindAsync(id); if (topic != null) { topic.IsLocked = !topic.IsLocked; await _context.SaveChangesAsync(); } return RedirectToAction("Topic", "Forum", new { area = "", id = id }); }
}