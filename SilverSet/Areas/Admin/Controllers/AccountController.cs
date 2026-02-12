using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using SilverSet.Models.Entities;
using SilverSet.Models.ViewModels;

namespace SilverSet.Areas.Admin.Controllers
{
    [Area("Admin")]
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
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model) { 
            var admin = await _context.Admins.FirstOrDefaultAsync(x => x.AdminName == model.AdminName);

            bool isSuccess = false;

            if (admin != null) {
                var hasher = new PasswordHasher<SilverSet.Areas.Admin.Models.Entities.Admin>();
                var result = hasher.VerifyHashedPassword(admin, admin.PasswordHash,
                    model.Password);
                isSuccess = (result == PasswordVerificationResult.Success);

                if (isSuccess) { 
                    admin.LastLogin = DateTime.Now;
                    _context.Admins.Update(admin);
                }

            }
            var loginLog = new LoginLog
            {
               Email = model.AdminName ?? "Bilinmeyen Deneme",
                LoginTime = DateTime.Now,
               IsSuccessful = isSuccess,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Bilinmeyen IP"
            };
            if (isSuccess) {
                return RedirectToAction("Index", "Home", new { area = "Admin" }); // TODO ! buraya admin paneli gelecek unutma!!!!!!!
            }
            ViewBag.ErrorMessage = "Geçersiz giriş denemesi!";
            return View(model);
        }
    }
}
