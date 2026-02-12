using Microsoft.AspNetCore.Identity;

namespace SilverSet.Models.Entities
{
    public class AppUser :IdentityUser<int>
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }

    public class AppRole :IdentityRole<int>{}
}
