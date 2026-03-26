using Microsoft.AspNetCore.Identity;

namespace SilverSet.Models.Entities
{
    public class AppUser : IdentityUser<int>
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsBanned { get; set; } = false;
        public string? BanReason { get; set; }
    }

    public class AppRole : IdentityRole<int> { }
}