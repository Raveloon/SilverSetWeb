using System.ComponentModel.DataAnnotations;

namespace SilverSet.Areas.Admin.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Admin adı boş bırakılamaz.")]
        [Display(Name = "Admin Name")]
        public string AdminName { get; set; }

        [Required(ErrorMessage = "Şifre boş bırakılamaz.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

    }
}
