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
        try
        {
            if (!int.TryParse(ticketId, out int id)) return;

            var isAdmin = Context.User!.IsInRole("Admin");
            var currentUserIdString = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(currentUserIdString, out int currentUserId);

            var hasAccess = await _context.Tickets
                .AsNoTracking()
                .AnyAsync(t => t.Id == id && (isAdmin || t.UserId == currentUserId));

            if (hasAccess)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Ticket_{ticketId}");
            }
        }
        catch (Exception)
        {
        }
    }

    public async Task SendMessage(int ticketId, string message)
    {
        try
        {
            var isAdmin = Context.User!.IsInRole("Admin");
            var currentUserIdString = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(currentUserIdString, out int currentUserId)) return;

            if (!isAdmin)
            {
                string cacheKey = $"last_msg_{currentUserId}";
                if (_cache.TryGetValue(cacheKey, out DateTime lastMessageTime))
                {
                    if ((DateTime.UtcNow - lastMessageTime).TotalSeconds < 1)
                    {
                        await Clients.Caller.SendAsync("ReceiveMessage", "[SİSTEM]: Lütfen mesaj gönderirken biraz bekleyin.", true);
                        return;
                    }
                }
                _cache.Set(cacheKey, DateTime.UtcNow, TimeSpan.FromSeconds(2));
            }

            var ticket = await _context.Tickets
                .FirstOrDefaultAsync(t => t.Id == ticketId && t.IsOpen && (isAdmin || t.UserId == currentUserId));

            if (ticket != null && !string.IsNullOrWhiteSpace(message))
            {
                var spamCheck = await _filterService.DetectSpam("", message);

                if (spamCheck.IsSpam)
                {
                    ticket.IsSpam = true;
                    ticket.SpamReason = $"Filtreye Takıldı: {spamCheck.Reason}";
                    message = await _filterService.FilterText(message);
                }

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
        catch (Exception)
        {
        }
    }
}