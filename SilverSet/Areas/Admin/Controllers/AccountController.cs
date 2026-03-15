using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SilverSet.Areas.Admin.Models.ViewModels;
using SilverSet.Data;
using SilverSet.Models.Entities;

namespace SilverSet.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AccountController : Controller
    {
        private readonly SilverSetContext _context;

        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;

        public AccountController(SilverSetContext context, SignInManager<AppUser> signInManager, UserManager<AppUser> userManager)
        {
            _context = context;
            _signInManager = signInManager;
            _userManager = userManager;
        }
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }
        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model) {

            if (!ModelState.IsValid) {
                return View(model);
            }
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP"; //Todo : IP adresi alma işlemi daha güvenilir hale getirilebilir, proxy durumları vs. için ek kontroller eklenebilir.


            var recentFailures = await _context.LoginLogs
        .Where(x => x.IpAddress == clientIp && !x.IsSuccessful && x.LoginTime > DateTime.Now.AddMinutes(-15))
        .CountAsync();

            if (recentFailures >= 5)
            {
                ViewBag.ErrorMessage = "Çok fazla başarısız giriş denemesi tespit edildi. Lütfen 15 dakika sonra tekrar deneyin.";
                return View(model);
            }
            var user = await _userManager.FindByNameAsync(model.AdminName);

            bool isSuccess = false;

            if (user != null) {
                
                bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

                if (isAdmin) {
                    var result = await _signInManager.PasswordSignInAsync(user, model.Password, isPersistent: false, lockoutOnFailure: false);
                    if (result.Succeeded)
                    {
                        isSuccess = true;
                    }
                }
                else
                {
                    ViewBag.ErrorMessage = "Kullanıcı adı veya şifre hatalı.";
                }

            }
            var loginLog = new LoginLog
            {
                Email = model.AdminName ?? "Bilinmeyen Deneme",
                LoginTime = DateTime.Now,
                IsSuccessful = isSuccess,
                IpAddress = clientIp
            };
            _context.LoginLogs.Add(loginLog);

            await _context.SaveChangesAsync();

            if (isSuccess) {
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            }

            if(ViewBag.ErrorMessage == null)
            {
                ViewBag.ErrorMessage = "Kullanıcı adı veya şifre hatalı !";
            }


            return View(model);
        }
    }
}
