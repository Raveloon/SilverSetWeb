using System.ComponentModel.DataAnnotations;

namespace SilverSet.Areas.Admin.Models.Entities
{
    public class Admin
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Admin adı boş bırakılamaz.")]
        [StringLength(50)]
        public required string AdminName { get; set; }

        [Required(ErrorMessage = "Admin şifresi boş bırakılamaz.")]
        public required string PasswordHash { get; set; }

        public DateTime LastLogin { get; set; } = DateTime.Now;

    }
}
