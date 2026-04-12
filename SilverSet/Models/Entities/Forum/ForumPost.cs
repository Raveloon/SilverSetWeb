using System.ComponentModel.DataAnnotations;

namespace SilverSet.Models.Entities.Forum;

public class ForumPost
{
    public int Id { get; set; }

    [Required]
    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsApproved { get; set; } = false;

    public int TopicId { get; set; }
    public ForumTopic Topic { get; set; } = null!;

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public int? ParentPostId { get; set; }
    public ForumPost? ParentPost { get; set; }
    public ICollection<ForumPost> Replies { get; set; } = new List<ForumPost>();
}