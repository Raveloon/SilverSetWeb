using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using SilverSet.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SilverSet.Areas.Admin.Controllers;

public record DailyRegistration(string Date, int Count, int CumulativeTotal);
public record DailyLogin(string Date, int Success, int Failed, int Total);
public record DailyReset(string Date, int Count);

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly SilverSetContext _context;
    private readonly UserManager<AppUser> _userManager;

    public DashboardController(SilverSetContext context, UserManager<AppUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!User.IsInRole("Admin"))
        {
            context.Result = new RedirectToActionResult("Login", "Account", new { area = "Admin" });
        }
        base.OnActionExecuting(context);
    }

    [HttpGet]
    public async Task<IActionResult> GetChartData(int days = 7)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;

        var firstUser = await _userManager.Users
            .Where(u => u.CreatedAt > new DateTime(2020, 1, 1))
            .OrderBy(u => u.CreatedAt)
            .Select(u => new { u.CreatedAt })
            .FirstOrDefaultAsync();

        DateTime firstDataDate = firstUser?.CreatedAt.Date ?? today.AddDays(-1);
        int totalDaysAvailable = (today - firstDataDate).Days + 1;

        int loopDays = (days <= 0 || days > totalDaysAvailable) ? totalDaysAvailable : days;
        if (loopDays < 2) loopDays = 2;

        DateTime queryStartDate = today.AddDays(-(loopDays - 1));

        var regDataRaw = await _userManager.Users
            .Where(u => u.CreatedAt >= queryStartDate)
            .GroupBy(u => u.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count);

        var loginDataRaw = await _context.UserLoginLogs
            .Where(l => l.LoginTime >= queryStartDate)
            .GroupBy(l => l.LoginTime.Date)
            .Select(g => new {
                Date = g.Key,
                Success = g.Count(x => x.IsSuccessful),
                Failed = g.Count(x => !x.IsSuccessful)
            })
            .ToDictionaryAsync(x => x.Date, x => new { x.Success, x.Failed });

        var dailyRegistrations = new List<DailyRegistration>();
        var dailyLogins = new List<DailyLogin>();
        int runningTotal = await _userManager.Users.CountAsync(u => u.CreatedAt < queryStartDate);

        for (int i = 0; i < loopDays; i++)
        {
            var date = queryStartDate.AddDays(i);
            regDataRaw.TryGetValue(date, out int regCount);
            loginDataRaw.TryGetValue(date, out var logInfo);
            runningTotal += regCount;

            dailyRegistrations.Add(new DailyRegistration(date.ToString("dd MMM"), regCount, runningTotal));
            dailyLogins.Add(new DailyLogin(date.ToString("dd MMM"), logInfo?.Success ?? 0, logInfo?.Failed ?? 0, (logInfo?.Success ?? 0) + (logInfo?.Failed ?? 0)));
        }

        return Json(new
        {
            registrations = dailyRegistrations,
            logins = dailyLogins,
            displayedDays = (today - firstDataDate).Days + 1,
            actualLoopDays = loopDays
        });
    }

    public async Task<IActionResult> Index(int days = 7)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;

        var firstUser = await _userManager.Users
            .Where(u => u.CreatedAt > new DateTime(2020, 1, 1))
            .OrderBy(u => u.CreatedAt)
            .Select(u => new { u.CreatedAt })
            .FirstOrDefaultAsync();

        DateTime firstDataDate = firstUser?.CreatedAt.Date ?? today.AddDays(-1);
        int totalDaysAvailable = (today - firstDataDate).Days + 1;
        if (totalDaysAvailable < 2) totalDaysAvailable = 2;

        int loopDays = (days <= 0 || days > totalDaysAvailable) ? totalDaysAvailable : days;
        if (loopDays < 2) loopDays = 2;

        DateTime queryStartDate = today.AddDays(-(loopDays - 1));

        ViewBag.SelectedDays = days;
        ViewBag.ActualDisplayedDays = totalDaysAvailable;

        ViewBag.TotalUsers = await _userManager.Users.CountAsync();
        ViewBag.NewThisWeek = await _userManager.Users.CountAsync(u => u.CreatedAt >= now.AddDays(-7));
        ViewBag.NewThisMonth = await _userManager.Users.CountAsync(u => u.CreatedAt >= now.AddMonths(-1));
        ViewBag.AdminCount = (await _userManager.GetUsersInRoleAsync("Admin")).Count;
        ViewBag.BannedCount = await _userManager.Users.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow);

        ViewBag.ActiveToday = await _context.UserLoginLogs
            .Where(l => l.IsSuccessful && l.LoginTime >= today)
            .Select(l => l.UserName).Distinct().CountAsync();

        ViewBag.FailedLoginsToday = await _context.UserLoginLogs.CountAsync(l => !l.IsSuccessful && l.LoginTime >= today);
        ViewBag.FailedLoginsWeek = await _context.UserLoginLogs.CountAsync(l => !l.IsSuccessful && l.LoginTime >= now.AddDays(-7));
        ViewBag.TotalLoginLogs = await _context.UserLoginLogs.CountAsync();
        ViewBag.TotalPasswordResets = await _context.PasswordResetLogs.CountAsync();
        ViewBag.TotalProjects = await _context.Assets.CountAsync();
        ViewBag.VisibleProjects = await _context.Assets.CountAsync(g => g.IsVisible);

        var allTimeRegDataRaw = await _userManager.Users
            .Where(u => u.CreatedAt >= firstDataDate)
            .GroupBy(u => u.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count);

        var allTimeRegistrations = new List<DailyRegistration>();
        int allTimeRunningTotal = 0;

        for (int i = 0; i < totalDaysAvailable; i++)
        {
            var date = firstDataDate.AddDays(i);
            allTimeRegDataRaw.TryGetValue(date, out int count);
            allTimeRunningTotal += count;
            allTimeRegistrations.Add(new DailyRegistration(date.ToString("dd MMM"), count, allTimeRunningTotal));
        }

        var loginDataRaw = await _context.UserLoginLogs
            .Where(l => l.LoginTime >= queryStartDate)
            .GroupBy(l => l.LoginTime.Date)
            .Select(g => new {
                Date = g.Key,
                Success = g.Count(x => x.IsSuccessful),
                Failed = g.Count(x => !x.IsSuccessful)
            })
            .ToDictionaryAsync(x => x.Date, x => new { x.Success, x.Failed });

        var resetDataRaw = await _context.PasswordResetLogs
            .Where(r => r.ResetTime >= queryStartDate)
            .GroupBy(r => r.ResetTime.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count);

        var dailyRegistrations = new List<DailyRegistration>();
        var dailyLogins = new List<DailyLogin>();
        var dailyResets = new List<DailyReset>();

        int runningTotal = await _userManager.Users.CountAsync(u => u.CreatedAt < queryStartDate);

        for (int i = 0; i < loopDays; i++)
        {
            var date = queryStartDate.AddDays(i);

            allTimeRegDataRaw.TryGetValue(date, out int regCount);
            loginDataRaw.TryGetValue(date, out var logInfo);
            resetDataRaw.TryGetValue(date, out int resetCount);

            runningTotal += regCount;

            dailyRegistrations.Add(new DailyRegistration(date.ToString("dd MMM"), regCount, runningTotal));
            dailyLogins.Add(new DailyLogin(date.ToString("dd MMM"), logInfo?.Success ?? 0, logInfo?.Failed ?? 0, (logInfo?.Success ?? 0) + (logInfo?.Failed ?? 0)));
            dailyResets.Add(new DailyReset(date.ToString("dd MMM"), resetCount));
        }

        var monthlyRegRaw = await _userManager.Users
            .Where(u => u.CreatedAt >= today.AddDays(-29))
            .GroupBy(u => u.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count);

        var monthlyRegistrations = new List<DailyRegistration>();
        for (int i = 29; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            monthlyRegRaw.TryGetValue(date, out int count);
            monthlyRegistrations.Add(new DailyRegistration(date.ToString("dd MMM"), count, 0));
        }

        ViewBag.AllTimeRegistrationsJson = System.Text.Json.JsonSerializer.Serialize(allTimeRegistrations);
        ViewBag.DailyRegistrationsJson = System.Text.Json.JsonSerializer.Serialize(dailyRegistrations);
        ViewBag.DailyLoginsJson = System.Text.Json.JsonSerializer.Serialize(dailyLogins);
        ViewBag.DailyResetsJson = System.Text.Json.JsonSerializer.Serialize(dailyResets);
        ViewBag.MonthlyRegistrationsJson = System.Text.Json.JsonSerializer.Serialize(monthlyRegistrations);

        ViewBag.DbStatus = await _context.Database.CanConnectAsync() ? "Healthy" : "Error";

        return View();
    }
}