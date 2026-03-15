using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SilverSet.Models.Entities;
using SilverSet.Data;
using SilverSet.Models.ViewModels;
using LoginModel = SilverSet.Models.ViewModels.LoginViewModel;

namespace SilverSet.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly SilverSetContext _context;

        public AccountController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, SilverSetContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new AppUser
                {
                    UserName = model.UserName,
                    Email = model.Email
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Bilinmiyor";

                    var log = new UserLoginLog
                    {
                        UserName = model.UserName,
                        Email = model.Email,
                        LoginTime = DateTime.UtcNow,
                        IsSuccessful = false,
                        IpAddress = clientIp
                    };
                    _context.UserLoginLogs.Add(log);
                    await _context.SaveChangesAsync();

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor";
            bool isSuccess = false;

            var user = await _userManager.FindByNameAsync(model.UserName);
            string logEmail = user != null ? user.Email : "Hesabın Emaili Bulunamadı";

            if (user != null)
            {
                var result = await _signInManager.PasswordSignInAsync(user.UserName, model.Password, model.RememberMe, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    isSuccess = true;
                }
                else if (result.IsLockedOut)
                {
                    ModelState.AddModelError(string.Empty, "Hesabınız geçici olarak kilitlendi. Lütfen daha sonra tekrar deneyiniz.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Kullanıcı adı veya şifre hatalı");
                }
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Kullanıcı adı veya şifre hatalı");
            }

            var userLoginLog = new UserLoginLog
            {
                UserName = model.UserName,
                Email = logEmail,
                LoginTime = DateTime.UtcNow,
                IsSuccessful = isSuccess,
                IpAddress = clientIp
            };
            _context.UserLoginLogs.Add(userLoginLog);
            await _context.SaveChangesAsync();

            if (isSuccess)
                return RedirectToAction("Index", "Home");

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
                return RedirectToAction(nameof(ForgotPasswordConfirmation));

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = Url.Action("ResetPassword", "Account",
                new { token, email = user.Email }, Request.Scheme);

            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<AccountController>>();
            logger.LogInformation("ŞİFRE SIFIRLAMA LİNKİ: {Email} - {Link}", user.Email, resetLink);

            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            if (token == null || email == null)
                return RedirectToAction("Login");

            var model = new ResetPasswordViewModel { Token = token, Email = email };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return RedirectToAction(nameof(ResetPasswordConfirmation));

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);

            if (result.Succeeded)
            {
                string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor";
                var log = new UserLoginLog
                {
                    UserName = user.UserName,
                    Email = user.Email,
                    LoginTime = DateTime.UtcNow,
                    IsSuccessful = true,
                    IpAddress = clientIp
                };
                _context.UserLoginLogs.Add(log);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            foreach (var error in result.Errors)
            {
                if (error.Code == "DuplicateEmail" || error.Code == "DuplicateUserName")
                    ModelState.AddModelError(string.Empty, "Bu bilgilerle kayıt oluşturulamadı.");
                else
                    ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }
    }
}