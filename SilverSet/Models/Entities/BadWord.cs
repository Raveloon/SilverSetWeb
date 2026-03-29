namespace SilverSet.Models.Entities;

public class BadWord
{
    public int Id { get; set; }
    public string Word { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}