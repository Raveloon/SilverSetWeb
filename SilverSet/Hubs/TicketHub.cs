using Microsoft.AspNetCore.SignalR;
using SilverSet.Data;
using SilverSet.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using SilverSet.Services;

namespace SilverSet.Hubs;

[Authorize]
public class TicketHub : Hub
{
    private readonly SilverSetContext _context;
    private readonly IMemoryCache _cache;
    private readonly ProfanityFilterService _filterService;

    public TicketHub(SilverSetContext context, IMemoryCache cache, ProfanityFilterService filterService)
    {
        _context = context;
        _cache = cache;
        _filterService = filterService;
    }

    public async Task JoinTicketGroup(string ticketId)
    {
        if (!int.TryParse(ticketId, out int id)) return;

        var user = Context.User;
        if (user == null) return;

        var isAdmin = user.IsInRole("Admin");
        var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out int currentUserId)) return;

        var hasAccess = await _context.Tickets
            .AsNoTracking()
            .AnyAsync(t => t.Id == id && (isAdmin || t.UserId == currentUserId));

        if (hasAccess)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Ticket_{id}");
        }
    }

    public async Task SendMessage(int ticketId, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        var user = Context.User;
        if (user == null) return;

        var currentUserIdString = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(currentUserIdString, out int currentUserId)) return;

        var isAdmin = user.IsInRole("Admin");

        if (!isAdmin)
        {
            string cacheKey = $"ratelimit_msg_{currentUserId}";
            if (_cache.TryGetValue(cacheKey, out _))
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "[SYSTEM]: Please wait before sending another message.", true);
                return;
            }
            _cache.Set(cacheKey, true, TimeSpan.FromSeconds(2));
        }

        var ticket = await _context.Tickets
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.IsOpen && (isAdmin || t.UserId == currentUserId));

        if (ticket == null) return;

        var cleanMessage = message.Trim();
        if (cleanMessage.Length > 500)
        {
            cleanMessage = cleanMessage.Substring(0, 500);
        }

        var spamCheck = await _filterService.DetectSpam("", cleanMessage);

        if (spamCheck.IsSpam)
        {
            ticket.IsSpam = true;
            ticket.SpamReason = $"Auto-Flag: {spamCheck.CaughtWord}";
            cleanMessage = await _filterService.FilterText(cleanMessage);
        }

        var ticketMessage = new TicketMessage
        {
            TicketId = ticketId,
            SenderId = currentUserId,
            Content = cleanMessage,
            IsAdminReply = isAdmin,
            SentAt = DateTime.UtcNow
        };

        _context.TicketMessages.Add(ticketMessage);
        await _context.SaveChangesAsync();

        await Clients.Group($"Ticket_{ticketId}").SendAsync("ReceiveMessage", cleanMessage, isAdmin);
    }
}