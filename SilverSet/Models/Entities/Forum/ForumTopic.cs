namespace SilverSet.Models.Entities.Forum;

public class ForumTopic
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsLocked { get; set; } = false;
    public bool IsPinned { get; set; } = false;
    public int ViewCount { get; set; }

    public int CategoryId { get; set; }
    public ForumCategory Category { get; set; } = null!;

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public ICollection<ForumPost> Posts { get; set; } = new List<ForumPost>();
}