namespace SilverSet.Models.Entities.Forum;

public class ForumPost
{
    public int Id { get; set; }
    public string Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int TopicId { get; set; }
    public ForumTopic Topic { get; set; } = null!;

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;
}