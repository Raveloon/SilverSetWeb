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
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";

            var recentFailures = await _context.UserLoginLogs
                .AsNoTracking()
                .CountAsync(x => x.IpAddress == clientIp && !x.IsSuccessful && x.LoginTime > DateTime.UtcNow.AddMinutes(-15));

            if (recentFailures >= 5)
            {
                ViewBag.ErrorMessage = "Çok fazla başarısız giriş denemesi tespit edildi. Lütfen 15 dakika sonra tekrar deneyin.";
                return View(model);
            }

            var user = await _userManager.FindByNameAsync(model.AdminName);
            bool isSuccess = false;

            if (user != null)
            {
                bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                if (isAdmin)
                {
                    var result = await _signInManager.PasswordSignInAsync(user, model.Password, isPersistent: false, lockoutOnFailure: false);
                    if (result.Succeeded)
                        isSuccess = true;
                }
                else
                {
                    ViewBag.ErrorMessage = "Kullanıcı adı veya şifre hatalı.";
                }
            }

            var loginLog = new UserLoginLog
            {
                UserName = model.AdminName ?? "Bilinmeyen Deneme",
                Email = user?.Email ?? string.Empty,
                LoginTime = DateTime.UtcNow,
                IsSuccessful = isSuccess,
                IpAddress = clientIp
            };
            _context.UserLoginLogs.Add(loginLog);
            await _context.SaveChangesAsync();

            if (isSuccess)
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });

            if (ViewBag.ErrorMessage == null)
                ViewBag.ErrorMessage = "Kullanıcı adı veya şifre hatalı!";

            return View(model);
        }
    }
}