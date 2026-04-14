using SilverSet.Models.Entities.Forum;

namespace SilverSet.Models.ViewModels;

public class TopicDetailViewModel
{
    public ForumTopic Topic { get; set; }
    public IEnumerable<ForumPost> Posts { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
}