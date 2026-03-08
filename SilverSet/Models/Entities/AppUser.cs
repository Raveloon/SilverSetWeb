using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SilverSet.Models.Entities
{


    public class AppUser : IdentityUser<int> { }
    public class AppRole :IdentityRole<int>{}
}
