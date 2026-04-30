using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using SilverSet.Models.Entities;
using SilverSet.Models.ViewModels;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using SilverSet.Services;

namespace SilverSet.Controllers;

public class ContactController : Controller
{
    private readonly SilverSetContext _context;
    private readonly TurnstileService _turnstileService;

    public ContactController(SilverSetContext context, TurnstileService turnstileService)
    {
        _context = context;
        _turnstileService = turnstileService;
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
                    .AsNoTracking()
                    .Include(t => t.Messages)
                    .Include(t => t.Attachments)
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
    public async Task<IActionResult> CreateTicket(string subject, string message, List<IFormFile>? attachments, string? TerminalAccessCode, [FromForm(Name = "cf-turnstile-response")] string turnstileToken)
    {
        if (!string.IsNullOrEmpty(TerminalAccessCode)) return RedirectToAction(nameof(Index));

        var isTurnstileValid = await _turnstileService.VerifyAsync(turnstileToken);
        if (!isTurnstileValid)
        {
            TempData["Error"] = "Security verification failed.";
            return RedirectToAction(nameof(Index));
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId)) return RedirectToAction("Login", "Account");

        var hasActiveTicket = await _context.Tickets.AsNoTracking().AnyAsync(t => t.UserId == userId && t.IsOpen);
        if (hasActiveTicket) return RedirectToAction(nameof(Index));

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
        {
            TempData["Error"] = "Subject and message are required.";
            return RedirectToAction(nameof(Index));
        }

        var ticket = new Ticket
        {
            UserId = userId,
            Subject = subject.Trim(),
            IsOpen = true,
            CreatedAt = DateTime.UtcNow
        };

        if (attachments != null && attachments.Count > 0)
        {
            if (attachments.Count > 5)
            {
                TempData["Error"] = "Maximum 5 images allowed.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var file in attachments)
            {
                var fileUrl = await ProcessAttachment(file);
                if (fileUrl != null)
                {
                    ticket.Attachments.Add(new TicketAttachment
                    {
                        FileUrl = fileUrl,
                        UploadedAt = DateTime.UtcNow
                    });
                }
            }
        }

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

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(int ticketId, string content)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

        var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId && t.UserId == userId && t.IsOpen);
        if (ticket == null || string.IsNullOrWhiteSpace(content)) return RedirectToAction(nameof(Index));

        var message = new TicketMessage
        {
            TicketId = ticketId,
            SenderId = userId,
            Content = content.Trim(),
            IsAdminReply = false,
            SentAt = DateTime.UtcNow
        };

        _context.TicketMessages.Add(message);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [Authorize]
    public async Task<IActionResult> Details(int id)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId)) return RedirectToAction("Login", "Account");

        var activeTicketId = await _context.Tickets
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.IsOpen)
            .Select(t => t.Id)
            .FirstOrDefaultAsync();

        if (activeTicketId != 0 && activeTicketId != id) return RedirectToAction(nameof(Index));

        var ticket = await _context.Tickets
            .AsNoTracking()
            .Include(t => t.Attachments)
            .Include(t => t.Messages)
                .ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

        if (ticket == null) return NotFound();

        return View(ticket);
    }

    [Authorize]
    public async Task<IActionResult> History()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId)) return RedirectToAction("Login", "Account");

        var closedTickets = await _context.Tickets
            .AsNoTracking()
            .Include(t => t.Messages)
            .Include(t => t.Attachments)
            .Where(t => t.UserId == userId && !t.IsOpen)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return View(closedTickets);
    }

    private async Task<string?> ProcessAttachment(IFormFile? file)
    {
        if (file == null || file.Length == 0) return null;

        if (file.Length > 10 * 1024 * 1024)
        {
            return null;
        }

        var permittedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!permittedExtensions.Contains(extension))
        {
            return null;
        }

        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/tickets");
        if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadPath, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return $"/uploads/tickets/{fileName}";
    }
}