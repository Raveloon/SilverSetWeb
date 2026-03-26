using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using SilverSet.Models.Entities;
using SilverSet.Models.ViewModels;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;

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
        var model = new ContactViewModel();

        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdString, out int userId))
            {
                model.ActiveTicket = await _context.Tickets
                    .Include(t => t.Messages)
                    .FirstOrDefaultAsync(t => t.UserId == userId && t.IsOpen);

                model.ClosedTicketsCount = await _context.Tickets
                    .AsNoTracking()
                    .CountAsync(t => t.UserId == userId && !t.IsOpen);
            }
        }

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [EnableRateLimiting("Strict")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTicket(string subject, string message)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId))
        {
            return RedirectToAction("Login", "Account");
        }

        var hasActiveTicket = await _context.Tickets
            .AsNoTracking()
            .AnyAsync(t => t.UserId == userId && t.IsOpen);

        if (hasActiveTicket)
        {
            TempData["Error"] = "You already have an active support request.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
        {
            TempData["Error"] = "All fields are required to initialize a ticket.";
            return RedirectToAction(nameof(Index));
        }

        var ticket = new Ticket
        {
            UserId = userId,
            Subject = subject.Trim(),
            IsOpen = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        var firstMessage = new TicketMessage
        {
            TicketId = ticket.Id,
            SenderId = userId,
            Content = message.Trim(),
            IsAdminReply = false,
            SentAt = DateTime.UtcNow
        };

        _context.TicketMessages.Add(firstMessage);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Secure line established. Our team will respond shortly.";
        return RedirectToAction(nameof(Index));
    }
    [Authorize]
    public async Task<IActionResult> History(int page = 1)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId)) return RedirectToAction("Login", "Account");

        int pageSize = 10;
        var query = _context.Tickets.Where(t => t.UserId == userId);

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

        return View(tickets);
    }

    [Authorize]
    public async Task<IActionResult> Details(int id)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId)) return RedirectToAction("Login", "Account");

        var ticket = await _context.Tickets
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

        if (ticket == null) return NotFound();

        return View(ticket);
    }
}