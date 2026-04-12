using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SilverSet.Areas.Admin.Models;
using SilverSet.Services;
using System.Text.RegularExpressions;

namespace SilverSet.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ForumController : Controller
{
    private readonly ForumSettingsService _settingsService;

    public ForumController(ForumSettingsService settingsService)
    {
        _settingsService = settingsService;
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
                TempData["Error"] = "Geçersiz format. Sadece sayı veya sayı+h/m kullanın (Örn: 24h, 30m).";
                return RedirectToAction(nameof(Settings));
            }

            var currentSettings = await _settingsService.GetAllForumSettingsAsync();
            var currentStatus = currentSettings.FirstOrDefault(s => s.Key == "ForumStatus")?.Value.ToLower() == "true";
            var currentDelay = currentSettings.FirstOrDefault(s => s.Key == "ForumPostDelay")?.Value ?? "24h";
            var currentThreshold = int.Parse(currentSettings.FirstOrDefault(s => s.Key == "ForumApprovalThreshold")?.Value ?? "5");

            if (newDelay != "0" && !newDelay.EndsWith("h") && !newDelay.EndsWith("m"))
            {
                newDelay += "h";
            }

            if (currentStatus == model.IsForumActive &&
                currentDelay == newDelay &&
                currentThreshold == model.ShadowPostLimit)
            {
                TempData["Info"] = "Değiştirilecek bir şey bulunamadı.";
                return RedirectToAction(nameof(Settings));
            }

            await _settingsService.UpdateSettingAsync("ForumStatus", model.IsForumActive.ToString().ToLower());
            await _settingsService.UpdateSettingAsync("ForumPostDelay", newDelay);
            await _settingsService.UpdateSettingAsync("ForumApprovalThreshold", model.ShadowPostLimit.ToString());

            TempData["Success"] = "Değişiklikler başarıyla yapıldı.";
        }
        catch (Exception)
        {
            TempData["Error"] = "Değişiklikler yapılamadı.";
        }

        return RedirectToAction(nameof(Settings));
    }
}