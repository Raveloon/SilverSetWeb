using System.ComponentModel.DataAnnotations;
namespace SilverSet.Models.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage ="Email boş bırakılamaz .")]
        [EmailAddress(ErrorMessage ="Geçerli bir email adresi giriniz")]
        public string Email { get; set; }
    }
}
