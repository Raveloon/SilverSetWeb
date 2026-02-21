using System.ComponentModel.DataAnnotations;

namespace SilverSet.Models.ViewModels
{
    public class UserLoginViewModel
    {
        [Required(ErrorMessage ="Kullanıcı adı boş bırakılamaz ")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Parola boş bırakılamaz ")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

    }
}
