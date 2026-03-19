using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;

namespace SilverSet.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class LogsController : Controller
{
    private readonly SilverSetContext _context;

    public LogsController(SilverSetContext context)
    {
        _context = context;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!User.IsInRole("Admin"))
        {
            context.Result = new RedirectToActionResult("Login", "Account", new { area = "Admin" });
        }
        base.OnActionExecuting(context);
    }

    public async Task<IActionResult> Index(string? search, string? filter, string? timeFilter, string? specificDate, int page = 1)
    {
        int pageSize = 20;

        var query = _context.UserLoginLogs.AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(l =>
                l.UserName.Contains(search) ||
                l.IpAddress.Contains(search) ||
                (l.Email != null && l.Email.Contains(search)));

        if (filter == "failed")
            query = query.Where(l => !l.IsSuccessful);
        else if (filter == "success")
            query = query.Where(l => l.IsSuccessful);

        var now = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(specificDate) && DateTime.TryParse(specificDate, out var parsedDate))
        {
            var dayStart = parsedDate.Date;
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(l => l.LoginTime >= dayStart && l.LoginTime < dayEnd);
        }
        else if (timeFilter == "7d")
            query = query.Where(l => l.LoginTime >= now.AddDays(-7));
        else if (timeFilter == "15d")
            query = query.Where(l => l.LoginTime >= now.AddDays(-15));
        else if (timeFilter == "1y")
            query = query.Where(l => l.LoginTime >= now.AddYears(-1));

        var today = DateTime.UtcNow.Date;

        ViewBag.TotalCount = await _context.UserLoginLogs.CountAsync();
        ViewBag.FailedCount = await _context.UserLoginLogs.CountAsync(l => !l.IsSuccessful);
        ViewBag.TodayCount = await _context.UserLoginLogs.CountAsync(l => l.LoginTime >= today);
        ViewBag.Search = search;
        ViewBag.Filter = filter;
        ViewBag.TimeFilter = timeFilter;
        ViewBag.SpecificDate = specificDate;
        ViewBag.CurrentPage = page;
        ViewBag.FilteredCount = await query.CountAsync();
        ViewBag.TotalPages = (int)Math.Ceiling(await query.CountAsync() / (double)pageSize);

        var logs = await query
            .OrderByDescending(l => l.LoginTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return View(logs);
    }
    public async Task<IActionResult> PasswordResets(string? search, string? timeFilter, string? specificDate, int page = 1)
    {
        int pageSize = 20;

        var query = _context.PasswordResetLogs.AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(l =>
                l.UserName.Contains(search) ||
                l.Email.Contains(search) ||
                l.IpAddress.Contains(search));

        var now = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(specificDate) && DateTime.TryParse(specificDate, out var parsedDate))
        {
            var dayStart = parsedDate.Date;
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(l => l.ResetTime >= dayStart && l.ResetTime < dayEnd);
        }
        else if (timeFilter == "7d")
            query = query.Where(l => l.ResetTime >= now.AddDays(-7));
        else if (timeFilter == "15d")
            query = query.Where(l => l.ResetTime >= now.AddDays(-15));
        else if (timeFilter == "1m")
            query = query.Where(l => l.ResetTime >= now.AddMonths(-1));
        else if (timeFilter == "6m")
            query = query.Where(l => l.ResetTime >= now.AddMonths(-6));
        else if (timeFilter == "1y")
            query = query.Where(l => l.ResetTime >= now.AddYears(-1));

        var weekAgo = now.AddDays(-7);

        ViewBag.TotalCount = await _context.PasswordResetLogs.CountAsync();
        ViewBag.SuccessCount = await _context.PasswordResetLogs.CountAsync(l => l.IsSuccessful && l.ResetTime >= weekAgo);
        ViewBag.FailCount = await _context.PasswordResetLogs.CountAsync(l => !l.IsSuccessful && l.ResetTime >= weekAgo);
        ViewBag.Search = search;
        ViewBag.TimeFilter = timeFilter;
        ViewBag.SpecificDate = specificDate;
        ViewBag.FilteredCount = await query.CountAsync();
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(await query.CountAsync() / (double)pageSize);

        var logs = await query
            .OrderByDescending(l => l.ResetTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return View(logs);
    }
}