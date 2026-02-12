using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SilverSet.Models.Entities
{
    public class AppUser :IdentityUser<int>
    {

        [Required(ErrorMessage = "kullanıcı adı boş bırakılamaz.")]
        [StringLength(50)]
        public string FirstName { get; set; } 

        [Required(ErrorMessage = "kullanıcı soyadı boş bırakılamaz.")]
        [StringLength(50)]
        public string LastName { get; set; } 
    }

    public class AppRole :IdentityRole<int>{}
}
