using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using SilverSet.Models.Entities;
using System.Security.Claims;

namespace SilverSet.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class TicketsController : Controller
{
    private readonly SilverSetContext _context;
    private readonly UserManager<AppUser> _userManager;

    public TicketsController(SilverSetContext context, UserManager<AppUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseTicket(int id, string closureMessage)
    {
        var ticket = await _context.Tickets.Include(t => t.Messages).FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null || string.IsNullOrWhiteSpace(closureMessage)) return RedirectToAction(nameof(Details), new { id });

        var finalMsg = new TicketMessage
        {
            TicketId = id,
            SenderId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            Content = "[CLOSED] " + closureMessage.Trim(),
            IsAdminReply = true,
            SentAt = DateTime.UtcNow
        };

        ticket.IsOpen = false;
        _context.TicketMessages.Add(finalMsg);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReopenTicket(int id, string openingReason)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null || string.IsNullOrWhiteSpace(openingReason)) return RedirectToAction(nameof(Details), new { id });

        var systemMsg = new TicketMessage
        {
            TicketId = id,
            SenderId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            Content = "[RE-OPENED] " + openingReason.Trim(),
            IsAdminReply = true,
            SentAt = DateTime.UtcNow
        };

        ticket.IsOpen = true;
        _context.TicketMessages.Add(systemMsg);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = ticket.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BanUser(int ticketId, string banReason)
    {
        var ticket = await _context.Tickets.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == ticketId);
        if (ticket == null || ticket.User == null || string.IsNullOrWhiteSpace(banReason)) return RedirectToAction(nameof(Index));

        var user = ticket.User;
        user.IsBanned = true;
        user.BanReason = banReason.Trim();
        user.LockoutEnd = DateTimeOffset.MaxValue;

        await _userManager.UpdateAsync(user);

        var systemMsg = new TicketMessage
        {
            TicketId = ticketId,
            SenderId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            Content = "[BANNED] " + banReason.Trim(),
            IsAdminReply = true,
            SentAt = DateTime.UtcNow
        };

        ticket.IsOpen = false;
        _context.TicketMessages.Add(systemMsg);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = ticketId });
    }

    public async Task<IActionResult> Index(string searchTerm, string status, string timeRange, int page = 1)
    {
        int pageSize = 10;
        var query = _context.Tickets.Include(t => t.User).AsQueryable();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(t => t.User!.UserName!.Contains(searchTerm) || t.Subject.Contains(searchTerm));
        }

        if (status == "open") query = query.Where(t => t.IsOpen);
        else if (status == "closed") query = query.Where(t => !t.IsOpen);

        DateTime? filterDate = timeRange switch
        {
            "7days" => DateTime.UtcNow.AddDays(-7),
            "15days" => DateTime.UtcNow.AddDays(-15),
            "1month" => DateTime.UtcNow.AddMonths(-1),
            "3months" => DateTime.UtcNow.AddMonths(-3),
            "1year" => DateTime.UtcNow.AddYears(-1),
            _ => null
        };

        if (filterDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= filterDate.Value);
        }

        int totalItems = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var tickets = await query
            .OrderByDescending(t => t.IsOpen)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.Status = status;
        ViewBag.TimeRange = timeRange;

        return View(tickets);
    }

    public async Task<IActionResult> Details(int id)
    {
        var ticket = await _context.Tickets
            .Include(t => t.User)
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null) return NotFound();

        return View(ticket);
    }
}