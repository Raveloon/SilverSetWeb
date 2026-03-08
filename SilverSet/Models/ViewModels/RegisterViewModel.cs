using System.ComponentModel.DataAnnotations;

namespace SilverSet.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Kullanıcı adı alanı boş bırakılamaz.")]
        [Display(Name ="Kullanıcı Adı")]
        public string UserName { get; set; }
        

        [Required(ErrorMessage = "E-posta alanı boş bırakılamaz.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        public string Email{ get; set; }

        [Required(ErrorMessage = "Şifre alanı boş bırakılamaz.")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Şifre tekrar alanı boş bırakılamaz.")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre Tekrar")]
        [Compare("Password", ErrorMessage = "Girilen şifreler eşleşmiyor.")]
        public string ConfirmPassword { get; set; }

    }
}
