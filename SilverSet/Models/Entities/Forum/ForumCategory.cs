using System.ComponentModel.DataAnnotations;

namespace SilverSet.Models.Entities.Forum;

public class ForumCategory
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = null!;

    [StringLength(500)]
    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ForumTopic> Topics { get; set; } = new List<ForumTopic>();
}