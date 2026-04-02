using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SilverSet.Models.Entities;

public class TicketAttachment
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    [Required]
    public string FileUrl { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("TicketId")]
    public Ticket? Ticket { get; set; }
}