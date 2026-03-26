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
            context.Result = new RedirectToActionResult("Login", "Account", new { area = "" });
        }
        base.OnActionExecuting(context);
    }

    public async Task<IActionResult> Index(string? search, string? filter, int page = 1)
    {
        int pageSize = 20;
        var query = _userManager.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(u => u.UserName!.Contains(search) || (u.Email != null && u.Email.Contains(search)));
        }

        if (filter == "admins")
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            var adminIds = adminUsers.Select(u => u.Id).ToList();
            query = query.Where(u => adminIds.Contains(u.Id));
        }
        else if (filter == "banned")
        {
            query = query.Where(u => u.IsBanned);
        }
        else if (filter == "users")
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            var adminIds = adminUsers.Select(u => u.Id).ToList();
            query = query.Where(u => !adminIds.Contains(u.Id));
        }

        int totalCount = await _userManager.Users.CountAsync();
        int filteredCount = await query.CountAsync();

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

        ViewBag.Filter = filter;
        ViewBag.TotalCount = totalCount;
        ViewBag.FilteredCount = filteredCount;
        ViewBag.Search = search;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(filteredCount / (double)pageSize);
        ViewBag.UserRoles = userRoles;
        ViewBag.AdminCount = (await _userManager.GetUsersInRoleAsync("Admin")).Count;
        ViewBag.BannedCount = await _userManager.Users.CountAsync(u => u.IsBanned);
        ViewBag.RootAdminName = _configuration["AdminSetup:AdminName"];

        return View(users);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ban(int id, string banReason)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        string rootAdmin = _configuration["AdminSetup:AdminName"] ?? "Admin";

        if (user == null || string.IsNullOrWhiteSpace(banReason)) return NotFound();

        if (user.UserName == User.Identity?.Name || user.UserName == rootAdmin)
        {
            TempData["Error"] = "Security Protocol: You cannot suspend yourself or the Root Admin.";
            return RedirectToAction(nameof(Index));
        }

        user.IsBanned = true;
        user.BanReason = banReason.Trim();
        user.LockoutEnd = DateTimeOffset.MaxValue;

        await _userManager.UpdateAsync(user);
        TempData["Success"] = $"Operative {user.UserName} has been permanently suspended.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unban(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        user.IsBanned = false;
        user.BanReason = null;
        user.LockoutEnd = null;

        await _userManager.UpdateAsync(user);
        TempData["Success"] = $"Access restored for {user.UserName}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MakeAdmin(int id)
    {
        string rootAdmin = _configuration["AdminSetup:AdminName"] ?? "Admin";
        if (User.Identity?.Name != rootAdmin)
        {
            TempData["Error"] = "Unauthorized: Only Root Admin can promote operatives.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        if (!await _userManager.IsInRoleAsync(user, "Admin"))
            await _userManager.AddToRoleAsync(user, "Admin");

        TempData["Success"] = $"{user.UserName} promoted to Administrator.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAdmin(int id)
    {
        string rootAdmin = _configuration["AdminSetup:AdminName"] ?? "Admin";
        if (User.Identity?.Name != rootAdmin)
        {
            TempData["Error"] = "Unauthorized: Only Root Admin can revoke privileges.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        if (user.UserName == User.Identity?.Name || user.UserName == rootAdmin)
        {
            TempData["Error"] = "Critical: You cannot revoke your own or Root Admin's privileges.";
            return RedirectToAction(nameof(Index));
        }

        if (await _userManager.IsInRoleAsync(user, "Admin"))
            await _userManager.RemoveFromRoleAsync(user, "Admin");

        TempData["Success"] = $"{user.UserName} privileges revoked.";
        return RedirectToAction(nameof(Index));
    }
}