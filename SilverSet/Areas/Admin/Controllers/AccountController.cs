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

            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP"; //Todo : IP adresi alma işlemi daha güvenilir hale getirilebilir, proxy durumları vs. için ek kontroller eklenebilir.


            var recentFailures = await _context.LoginLogs
        .Where(x => x.IpAddress == clientIp && !x.IsSuccessful && x.LoginTime > DateTime.Now.AddMinutes(-15))
        .CountAsync();

            if (recentFailures >= 5)
            {
                ViewBag.ErrorMessage = "Çok fazla başarısız giriş denemesi tespit edildi. Lütfen 15 dakika sonra tekrar deneyin.";
                return View(model);
            }
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
                IpAddress = clientIp
            };
            _context.LoginLogs.Add(loginLog);

            await _context.SaveChangesAsync();

            if (isSuccess) {
                return RedirectToAction("Index", "Home", new { area = "Admin" }); // TODO ! buraya admin paneli gelecek unutma!!!!!!!
            }
            ViewBag.ErrorMessage = "Geçersiz giriş denemesi!";
            return View(model);
        }
    }
}
