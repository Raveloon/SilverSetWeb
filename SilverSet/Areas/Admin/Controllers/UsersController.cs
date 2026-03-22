using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using SilverSet.Models.Entities;

namespace SilverSet.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SilverSetContext _context;
    private readonly IConfiguration _configuration;

    public UsersController(UserManager<AppUser> userManager, SilverSetContext context, IConfiguration configuration)
    {
        _userManager = userManager;
        _context = context;
        _configuration = configuration;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!User.IsInRole("Admin"))
        {
            context.Result = new RedirectToActionResult("Login", "Account", new { area = "Admin" });
        }
        base.OnActionExecuting(context);
    }

    public async Task<IActionResult> Index(string? search, string? filter, int page = 1)
    {
        int pageSize = 20;

        var query = _userManager.Users.AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(u =>
                u.UserName.Contains(search) ||
                (u.Email != null && u.Email.Contains(search)));
        if (filter == "admins")
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            var adminIds = adminUsers.Select(u => u.Id).ToList();
            query = query.Where(u => adminIds.Contains(u.Id));
        }
        else if (filter == "banned")
        {
            query = query.Where(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow);
        }
        else if (filter == "users")
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            var adminIds = adminUsers.Select(u => u.Id).ToList();
            query = query.Where(u => !adminIds.Contains(u.Id));
        }

        ViewBag.Filter = filter;

        ViewBag.TotalCount = await _userManager.Users.CountAsync();
        ViewBag.FilteredCount = await query.CountAsync();
        ViewBag.Search = search;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(await query.CountAsync() / (double)pageSize);

        var users = await query
            .OrderBy(u => u.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userRoles = new Dictionary<int, IList<string>>();
        foreach (var user in users)
        {
            userRoles[user.Id] = await _userManager.GetRolesAsync(user);
        }

        ViewBag.UserRoles = userRoles;
        ViewBag.AdminCount = (await _userManager.GetUsersInRoleAsync("Admin")).Count;
        ViewBag.BannedCount = await _userManager.Users.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow);
        ViewBag.RootAdminName = _configuration["AdminSetup:AdminName"];
        return View(users);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ban(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        // GÜVENLİK KORUMASI: Admin kendini veya Kök(Root) Admin'i banlayamaz
        if (user.UserName == User.Identity?.Name || user.UserName == "Admin")
        {
            TempData["Error"] = "Kendinizi veya Kök(Root) Admin'i banlayamazsınız!";
            return RedirectToAction(nameof(Index));
        }

        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        await _userManager.SetLockoutEnabledAsync(user, true);

        TempData["Success"] = $"{user.UserName} banlandı.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unban(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        await _userManager.SetLockoutEndDateAsync(user, null);

        TempData["Success"] = $"{user.UserName} banı kaldırıldı.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MakeAdmin(int id)
    {
        // KORUMA: Sadece Root Admin yetki verebilir
        string rootAdmin = _configuration["AdminSetup:AdminName"] ?? "Admin";
        if (User.Identity?.Name != rootAdmin)
        {
            TempData["Error"] = "Yetki Hatası: Sadece Kök (Root) Admin başka birini admin yapabilir!";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        if (!await _userManager.IsInRoleAsync(user, "Admin"))
            await _userManager.AddToRoleAsync(user, "Admin");

        TempData["Success"] = $"{user.UserName} admin yapıldı.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAdmin(int id)
    {
        // KORUMA: Sadece Root Admin yetki alabilir
        string rootAdmin = _configuration["AdminSetup:AdminName"] ;
        if (User.Identity?.Name != rootAdmin)
        {
            TempData["Error"] = "Yetki Hatası: Sadece Kök (Root) Admin yetki alabilir!";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        // GÜVENLİK KORUMASI: Admin kendi yetkisini veya Kök(Root) Admin'in yetkisini alamaz
        if (user.UserName == User.Identity?.Name || user.UserName == rootAdmin)
        {
            TempData["Error"] = "Kritik Hata: Kendi yetkinizi veya Kök(Root) Admin'in yetkisini kaldıramazsınız!";
            return RedirectToAction(nameof(Index));
        }

        if (await _userManager.IsInRoleAsync(user, "Admin"))
            await _userManager.RemoveFromRoleAsync(user, "Admin");

        TempData["Success"] = $"{user.UserName} admin rolü kaldırıldı.";
        return RedirectToAction(nameof(Index));
    }

    
}