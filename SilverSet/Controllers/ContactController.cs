using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using SilverSet.Models.Entities;
using System.Security.Claims;

namespace SilverSet.Controllers;

public class ContactController : Controller
{
    private readonly SilverSetContext _context;

    public ContactController(SilverSetContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            // string'i int'e çeviriyoruz
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var activeTicket = await _context.Tickets
                .Include(t => t.Messages)
                .FirstOrDefaultAsync(t => t.UserId == userId && t.IsOpen);

            var closedTicketsCount = await _context.Tickets
                .CountAsync(t => t.UserId == userId && !t.IsOpen);

            ViewBag.ActiveTicket = activeTicket;
            ViewBag.ClosedTicketsCount = closedTicketsCount;
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTicket(string subject, string message)
    {
        if (User.Identity == null || !User.Identity.IsAuthenticated)
            return RedirectToAction("Login", "Account");

        // string'i int'e çeviriyoruz
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var hasActiveTicket = await _context.Tickets.AnyAsync(t => t.UserId == userId && t.IsOpen);
        if (hasActiveTicket)
        {
            TempData["Error"] = "You already have an active ticket.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
        {
            TempData["Error"] = "Subject and message are required.";
            return RedirectToAction(nameof(Index));
        }

        var ticket = new Ticket
        {
            UserId = userId,
            Subject = subject,
            IsOpen = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        var firstMessage = new TicketMessage
        {
            TicketId = ticket.Id,
            SenderId = userId,
            Content = message,
            IsAdminReply = false,
            SentAt = DateTime.UtcNow
        };

        _context.TicketMessages.Add(firstMessage);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Ticket initialized successfully. Support channel is now open.";
        return RedirectToAction(nameof(Index));
    }
}