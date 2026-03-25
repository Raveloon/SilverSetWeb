using Microsoft.AspNetCore.SignalR;
using SilverSet.Data;
using SilverSet.Models.Entities;

namespace SilverSet.Hubs;

public class TicketHub : Hub
{
    private readonly SilverSetContext _context;

    public TicketHub(SilverSetContext context)
    {
        _context = context;
    }

    public async Task JoinTicketGroup(string ticketId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Ticket_{ticketId}");
    }

    public async Task SendMessage(int ticketId, int userId, string message)
    {
        var ticketMessage = new TicketMessage
        {
            TicketId = ticketId,
            SenderId = userId,
            Content = message,
            IsAdminReply = false,
            SentAt = DateTime.UtcNow
        };

        _context.TicketMessages.Add(ticketMessage);
        await _context.SaveChangesAsync();

        await Clients.Group($"Ticket_{ticketId}").SendAsync("ReceiveMessage", message, false);
    }
}