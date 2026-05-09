using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using DinkToPdf;
using DinkToPdf.Contracts;

namespace SilverSet.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class LogsController : Controller
{
    private readonly SilverSetContext _context;
    private readonly IConverter _pdfConverter;

    public LogsController(SilverSetContext context, IConverter pdfConverter)
    {
        _context = context;
        _pdfConverter = pdfConverter;
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

        return View("LoginLogs", logs);
    }

    [HttpGet]
    public async Task<IActionResult> ExportLoginLogsPdf(string? search, string? filter, string? timeFilter, string? specificDate)
    {
        var query = _context.UserLoginLogs.AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(l => l.UserName.Contains(search) || l.IpAddress.Contains(search) || (l.Email != null && l.Email.Contains(search)));

        if (filter == "failed") query = query.Where(l => !l.IsSuccessful);
        else if (filter == "success") query = query.Where(l => l.IsSuccessful);

        var now = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(specificDate) && DateTime.TryParse(specificDate, out var parsedDate))
        {
            var dayStart = parsedDate.Date;
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(l => l.LoginTime >= dayStart && l.LoginTime < dayEnd);
        }
        else if (timeFilter == "7d") query = query.Where(l => l.LoginTime >= now.AddDays(-7));
        else if (timeFilter == "15d") query = query.Where(l => l.LoginTime >= now.AddDays(-15));
        else if (timeFilter == "1y") query = query.Where(l => l.LoginTime >= now.AddYears(-1));

        var logs = await query.OrderByDescending(l => l.LoginTime).ToListAsync();

        var htmlContent = $@"
        <!DOCTYPE html>
        <html>
        <head>
            <style>
                body {{ font-family: 'Segoe UI', Arial, sans-serif; color: #333; }}
                .header {{ border-bottom: 2px solid #2c3e50; padding-bottom: 10px; margin-bottom: 20px; }}
                table {{ width: 100%; border-collapse: collapse; font-size: 12px; page-break-inside: auto; }}
                tr {{ page-break-inside: avoid; page-break-after: auto; }}
                thead {{ display: table-header-group; }}
                tfoot {{ display: table-footer-group; }}
                th, td {{ padding: 8px; text-align: left; border-bottom: 1px solid #ddd; }}
                th {{ background-color: #f8fafc; color: #475569; text-transform: uppercase; }}
                .success {{ color: #166534; font-weight: bold; }}
                .fail {{ color: #991b1b; font-weight: bold; }}
            </style>
        </head>
        <body>
            <div class='header'>
                <h2>SilverSet Studios - Login Logs</h2>
                <p>Generated: {DateTime.Now:dd.MM.yyyy HH:mm}</p>
            </div>
            <table>
                <thead>
                    <tr>
                        <th>Time</th>
                        <th>Username</th>
                        <th>Email</th>
                        <th>IP Address</th>
                        <th>Status</th>
                    </tr>
                </thead>
                <tbody>";

        foreach (var log in logs)
        {
            var statusClass = log.IsSuccessful ? "success" : "fail";
            var statusText = log.IsSuccessful ? "SUCCESS" : "FAILED";
            htmlContent += $@"
                    <tr>
                        <td>{log.LoginTime:dd.MM.yyyy HH:mm:ss}</td>
                        <td>{log.UserName}</td>
                        <td>{log.Email}</td>
                        <td>{log.IpAddress}</td>
                        <td class='{statusClass}'>{statusText}</td>
                    </tr>";
        }

        htmlContent += "</tbody></table></body></html>";

        var globalSettings = new GlobalSettings
        {
            ColorMode = ColorMode.Color,
            Orientation = Orientation.Portrait,
            PaperSize = PaperKind.A4,
            Margins = new MarginSettings { Top = 15, Bottom = 15, Left = 15, Right = 15 },
            DocumentTitle = "Login Logs Report"
        };

        var objectSettings = new ObjectSettings
        {
            PagesCount = true,
            HtmlContent = htmlContent,
            WebSettings = { DefaultEncoding = "utf-8" },
            HeaderSettings = { FontName = "Arial", FontSize = 9, Right = "Page [page] of [toPage]", Line = true }
        };

        var pdfDocument = new HtmlToPdfDocument()
        {
            GlobalSettings = globalSettings,
            Objects = { objectSettings }
        };

        var pdfBytes = _pdfConverter.Convert(pdfDocument);

        return File(pdfBytes, "application/pdf", $"LoginLogs_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
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