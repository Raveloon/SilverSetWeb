using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SilverSet.Areas.Admin.Models;
using SilverSet.Data;
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

    public ForumController(ForumSettingsService settingsService, SilverSetContext context)
    {
        _settingsService = settingsService;
        _context = context;
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
                TempData["Error"] = "Invalid format. Use numbers or number+h/m.";
                return RedirectToAction(nameof(Settings));
            }

            if (newDelay != "0" && !newDelay.EndsWith("h") && !newDelay.EndsWith("m"))
            {
                newDelay += "h";
            }

            await _settingsService.UpdateSettingAsync("ForumStatus", model.IsForumActive.ToString().ToLower());
            await _settingsService.UpdateSettingAsync("ForumPostDelay", newDelay);
            await _settingsService.UpdateSettingAsync("ForumApprovalThreshold", model.ShadowPostLimit.ToString());

            TempData["Success"] = "Forum settings updated successfully.";
        }
        catch (Exception)
        {
            TempData["Error"] = "Failed to update settings.";
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

        // DisplayOrder kaldırıldı, varsayılan liste sıralaması gelecek
        var categories = await categoriesQuery.ToListAsync();

        return View(categories);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Category name is required.";
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

        TempData["Success"] = "New category created.";
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
            TempData["Success"] = "Category status updated.";
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
                _context.ForumPosts.RemoveRange(topic.Posts);
            }
            _context.ForumTopics.RemoveRange(category.Topics);
            _context.ForumCategories.Remove(category);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Category and all related data removed.";
        }
        return RedirectToAction(nameof(Categories));
    }

    [HttpGet]
    public async Task<IActionResult> PendingApprovals()
    {
        var pendingTopics = await _context.ForumTopics
            .Include(t => t.User)
            .Where(t => !t.IsApproved)
            .Select(t => new PendingItemViewModel
            {
                Id = t.Id,
                Type = "Topic",
                Title = t.Title,
                Content = t.Content,
                UserName = t.User.UserName,
                CreatedAt = t.CreatedAt
            }).ToListAsync();

        var pendingPosts = await _context.ForumPosts
            .Include(p => p.User)
            .Include(p => p.Topic)
            .Where(p => !p.IsApproved)
            .Select(p => new PendingItemViewModel
            {
                Id = p.Id,
                Type = "Post",
                Title = "Reply to: " + p.Topic.Title,
                Content = p.Content,
                UserName = p.User.UserName,
                CreatedAt = p.CreatedAt
            }).ToListAsync();

        var model = pendingTopics.Concat(pendingPosts).OrderBy(x => x.CreatedAt).ToList();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveItem(int id, string type)
    {
        if (type == "Topic")
        {
            var topic = await _context.ForumTopics.FindAsync(id);
            if (topic != null) topic.IsApproved = true;
        }
        else
        {
            var post = await _context.ForumPosts.FindAsync(id);
            if (post != null) post.IsApproved = true;
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Item approved.";
        return RedirectToAction(nameof(PendingApprovals));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItem(int id, string type)
    {
        if (type == "Topic")
        {
            var topic = await _context.ForumTopics.FindAsync(id);
            if (topic != null) _context.ForumTopics.Remove(topic);
        }
        else
        {
            var post = await _context.ForumPosts.FindAsync(id);
            if (post != null) _context.ForumPosts.Remove(post);
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Item removed.";
        return RedirectToAction(nameof(PendingApprovals));
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
            TempData["Success"] = topic.IsPinned ? "Topic pinned." : "Topic unpinned.";
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
            TempData["Success"] = topic.IsLocked ? "Topic locked." : "Topic unlocked.";
        }
        return RedirectToAction("Topic", "Forum", new { area = "", id = id });
    }
}