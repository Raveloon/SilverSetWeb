using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using SilverSet.Models.Entities;
using SilverSet.Services;
using System.Security.Claims;

namespace SilverSet.Areas.Admin.Controllers;

public class AdminTicketDashboardViewModel
{
    public List<Ticket> ActiveTickets { get; set; } = new();
    public List<Ticket> SpamTickets { get; set; } = new();
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public string? TimeRange { get; set; }
}

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class TicketsController : Controller
{
    private readonly SilverSetContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly ProfanityFilterService _profanityFilter;

    public TicketsController(SilverSetContext context, UserManager<AppUser> userManager, ProfanityFilterService profanityFilter)
    {
        _context = context;
        _userManager = userManager;
        _profanityFilter = profanityFilter;
    }

    public async Task<IActionResult> Index(string searchTerm, string status, string timeRange, string view = "active")
    {
        var query = _context.Tickets.AsNoTracking().Include(t => t.User).AsQueryable();

        ViewBag.HasActiveTickets = await _context.Tickets.AnyAsync(t => !t.IsSpam && t.IsOpen);
        ViewBag.HasActiveSpamTickets = await _context.Tickets.AnyAsync(t => t.IsSpam);
        ViewBag.ActiveMenu = (view == "spam") ? "Spam" : "Tickets";

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(t => t.User!.UserName!.Contains(searchTerm) || t.Subject.Contains(searchTerm));
        }

        if (status == "open") query = query.Where(t => t.IsOpen);
        else if (status == "closed") query = query.Where(t => !t.IsOpen);

        DateTime? filterDate = timeRange switch
        {
            "7days" => DateTime.UtcNow.AddDays(-7),
            "1month" => DateTime.UtcNow.AddMonths(-1),
            _ => null
        };

        if (filterDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= filterDate.Value);
        }

        var allTickets = await query
            .OrderByDescending(t => t.IsOpen)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync();

        var viewModel = new AdminTicketDashboardViewModel
        {
            ActiveTickets = allTickets.Where(t => !t.IsSpam).ToList(),
            SpamTickets = allTickets.Where(t => t.IsSpam).ToList(),
            SearchTerm = searchTerm,
            Status = status,
            TimeRange = timeRange
        };

        return View(viewModel);
    }

    public async Task<IActionResult> Details(int id)
    {
        var ticket = await _context.Tickets
            .AsNoTracking()
            .Include(t => t.User)
            .Include(t => t.Messages)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null) return NotFound();

        ViewBag.HasActiveTickets = await _context.Tickets.AnyAsync(t => !t.IsSpam && t.IsOpen);
        ViewBag.HasActiveSpamTickets = await _context.Tickets.AnyAsync(t => t.IsSpam);
        ViewBag.ActiveMenu = ticket.IsSpam ? "Spam" : "Tickets";

        foreach (var msg in ticket.Messages)
        {
            msg.Content = await _profanityFilter.FilterText(msg.Content);
        }

        return View(ticket);
    }

    [HttpPost]
    public async Task<IActionResult> AddBadWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return BadRequest();

        var normalizedWord = word.Trim().ToLower();
        if (normalizedWord.Length > 100) normalizedWord = normalizedWord.Substring(0, 100);

        var exists = await _context.BadWords.AnyAsync(b => b.Word == normalizedWord);

        if (!exists)
        {
            _context.BadWords.Add(new BadWord { Word = normalizedWord });
            await _context.SaveChangesAsync();
            _profanityFilter.ClearCache();
        }

        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseTicket(int id, string closureMessage)
    {
        var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null || string.IsNullOrWhiteSpace(closureMessage)) return RedirectToAction(nameof(Index));

        var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminIdClaim) || !int.TryParse(adminIdClaim, out int adminId)) return Unauthorized();

        string safeMessage = closureMessage.Trim();
        if (safeMessage.Length > 450) safeMessage = safeMessage.Substring(0, 450);

        var finalMsg = new TicketMessage
        {
            TicketId = id,
            SenderId = adminId,
            Content = "[CLOSED] " + safeMessage,
            IsAdminReply = true,
            SentAt = DateTime.UtcNow
        };

        ticket.IsOpen = false;
        _context.TicketMessages.Add(finalMsg);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index), new { view = ticket.IsSpam ? "spam" : "active" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReopenTicket(int id, string openingReason)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null || string.IsNullOrWhiteSpace(openingReason)) return RedirectToAction(nameof(Index));

        var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminIdClaim) || !int.TryParse(adminIdClaim, out int adminId)) return Unauthorized();

        string safeReason = openingReason.Trim();
        if (safeReason.Length > 450) safeReason = safeReason.Substring(0, 450);

        var systemMsg = new TicketMessage
        {
            TicketId = id,
            SenderId = adminId,
            Content = "[RE-OPENED] " + safeReason,
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
    public async Task<IActionResult> RestoreSpamTicket(int id)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null) return RedirectToAction(nameof(Index));

        var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminIdClaim) || !int.TryParse(adminIdClaim, out int adminId)) return Unauthorized();

        ticket.IsSpam = false;
        ticket.SpamReason = null;

        var systemMsg = new TicketMessage
        {
            TicketId = id,
            SenderId = adminId,
            Content = "[RESTORED] Ticket has been marked as safe and restored to active list.",
            IsAdminReply = true,
            SentAt = DateTime.UtcNow
        };

        _context.TicketMessages.Add(systemMsg);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = ticket.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsSpam(int id, string reason)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null || string.IsNullOrWhiteSpace(reason)) return RedirectToAction(nameof(Index));

        var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminIdClaim) || !int.TryParse(adminIdClaim, out int adminId)) return Unauthorized();

        string cleanReason = reason.Trim();
        if (cleanReason.Length > 480) cleanReason = cleanReason.Substring(0, 480);

        ticket.IsSpam = true;
        ticket.SpamReason = "MANUAL : " + cleanReason;

        var systemMsg = new TicketMessage
        {
            TicketId = id,
            SenderId = adminId,
            Content = "[FLAGGED] " + cleanReason,
            IsAdminReply = true,
            SentAt = DateTime.UtcNow
        };

        _context.TicketMessages.Add(systemMsg);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index), new { view = "spam" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BanUser(int ticketId, string banReason)
    {
        var ticket = await _context.Tickets.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == ticketId);
        if (ticket == null || ticket.User == null || string.IsNullOrWhiteSpace(banReason)) return RedirectToAction(nameof(Index));

        var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminIdClaim) || !int.TryParse(adminIdClaim, out int adminId)) return Unauthorized();

        string safeBanReason = banReason.Trim();
        if (safeBanReason.Length > 450) safeBanReason = safeBanReason.Substring(0, 450);

        var user = ticket.User;
        user.IsBanned = true;
        user.BanReason = safeBanReason;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        await _userManager.UpdateAsync(user);

        var systemMsg = new TicketMessage
        {
            TicketId = ticketId,
            SenderId = adminId,
            Content = "[BANNED] " + safeBanReason,
            IsAdminReply = true,
            SentAt = DateTime.UtcNow
        };

        ticket.IsOpen = false;
        _context.TicketMessages.Add(systemMsg);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = ticketId });
    }
}