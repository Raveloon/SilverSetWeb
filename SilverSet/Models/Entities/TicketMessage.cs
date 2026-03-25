using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SilverSet.Models.Entities;

public class TicketMessage
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    [Required]
    public int SenderId { get; set; } // string yerine int oldu ve "= string.Empty" silindi

    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public bool IsAdminReply { get; set; }

    public Ticket? Ticket { get; set; }

    [ForeignKey("SenderId")]
    public AppUser? Sender { get; set; }
}