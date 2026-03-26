using Microsoft.AspNetCore.SignalR;
using SilverSet.Data;
using SilverSet.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;

namespace SilverSet.Hubs;

[Authorize]
public class TicketHub : Hub
{
    private readonly SilverSetContext _context;
    private readonly IMemoryCache _cache;

    public TicketHub(SilverSetContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task JoinTicketGroup(string ticketId)
    {
        if (!int.TryParse(ticketId, out int id)) return;

        var currentUserIdString = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(currentUserIdString, out int currentUserId)) return;

        var isAdmin = Context.User!.IsInRole("Admin");

        var hasAccess = await _context.Tickets
            .AsNoTracking()
            .AnyAsync(t => t.Id == id && (t.UserId == currentUserId || isAdmin));

        if (hasAccess)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Ticket_{ticketId}");
        }
    }

    public async Task SendMessage(int ticketId, string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 2000) return;

        var currentUserIdString = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(currentUserIdString, out int currentUserId)) return;

        string cacheKey = $"last_msg_{currentUserId}";
        if (_cache.TryGetValue(cacheKey, out DateTime lastMessageTime))
        {
            if ((DateTime.UtcNow - lastMessageTime).TotalSeconds < 1)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "[SYSTEM]: Excessive transmission detected. Please wait.", true);
                return;
            }
        }

        _cache.Set(cacheKey, DateTime.UtcNow, TimeSpan.FromSeconds(2));

        var isAdmin = Context.User!.IsInRole("Admin");

        var ticket = await _context.Tickets
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.IsOpen && (t.UserId == currentUserId || isAdmin));

        if (ticket != null)
        {
            var ticketMessage = new TicketMessage
            {
                TicketId = ticketId,
                SenderId = currentUserId,
                Content = message.Trim(),
                IsAdminReply = isAdmin,
                SentAt = DateTime.UtcNow
            };

            _context.TicketMessages.Add(ticketMessage);
            await _context.SaveChangesAsync();

            await Clients.Group($"Ticket_{ticketId}").SendAsync("ReceiveMessage", message.Trim(), isAdmin);
        }
    }
}