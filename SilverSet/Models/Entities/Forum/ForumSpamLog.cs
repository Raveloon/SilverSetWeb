using System.ComponentModel.DataAnnotations;

namespace SilverSet.Models.Entities.Forum;

public class ForumSpamLog
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string Type { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string CaughtWord { get; set; } = null!;

    public int? TopicId { get; set; }
    public ForumTopic? Topic { get; set; }

    public int? PostId { get; set; }
    public ForumPost? Post { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsHandled { get; set; } = false;

    public int? RelatedSpamId { get; set; }
    public ForumSpamLog? RelatedSpam { get; set; }
    public ICollection<ForumSpamLog> SecondaryLogs { get; set; } = new List<ForumSpamLog>();
}