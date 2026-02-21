using Microsoft.AspNetCore.Mvc;
using SilverSet.Data;
using SilverSet.Areas.Admin.Models.ViewModels;

namespace SilverSet.Controllers
{
    public class AccountController : Controller
    {
        private readonly SilverSetContext _context;
        
        public AccountController(SilverSetContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        //[HttpPost]
        //public IActionResult Login(LoginViewModel model)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        var user = _context.Users.FirstOrDefault(x => x.UserName == model.AdminName);
        //        if (user != null)
        //        {
        //            //TODO: 
        //        }
        //    }
        //}


    }
}
