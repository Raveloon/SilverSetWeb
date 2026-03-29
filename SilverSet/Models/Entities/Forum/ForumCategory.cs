namespace SilverSet.Models.Entities.Forum;

public class ForumCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ForumTopic> Topics { get; set; } = new List<ForumTopic>();
}