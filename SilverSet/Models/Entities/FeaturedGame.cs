using System.ComponentModel.DataAnnotations;

namespace SilverSet.Models.Entities
{
    public class FeaturedGame
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(500)]
        public string? ImagePath { get; set; }

        [MaxLength(50)]
        public string? Genre { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsVisible { get; set; }
    }
}