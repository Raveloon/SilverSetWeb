using System.ComponentModel.DataAnnotations;

namespace SilverSet.Models.Entities;

public class GlobalSetting
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Key { get; set; } = null!;

    [Required]
    public string Value { get; set; } = null!;

    public string? Description { get; set; }
}