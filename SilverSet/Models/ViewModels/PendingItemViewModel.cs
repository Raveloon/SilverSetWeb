namespace SilverSet.Models.ViewModels;

public class PendingItemViewModel
{
    public int Id { get; set; }
    public string Type { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public string UserName { get; set; }
    public DateTime CreatedAt { get; set; }
}