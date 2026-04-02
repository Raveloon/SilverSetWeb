using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SilverSet.Models.Entities;

public class Ticket
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsOpen { get; set; } = true;

    [ForeignKey("UserId")]
    public AppUser? User { get; set; }

    public bool IsSpam { get; set; } = false;
    [MaxLength(500)]
    public string? SpamReason { get; set; }

    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();

    
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
}