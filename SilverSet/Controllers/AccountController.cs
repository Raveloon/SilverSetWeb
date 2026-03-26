using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using SilverSet.Models.Entities;
using SilverSet.Models.ViewModels;
using System.Security.Claims;

namespace SilverSet.Controllers;

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
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new AppUser
        {
            UserName = model.UserName.Trim(),
            Email = model.Email.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Home");
        }

        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        _context.UserLoginLogs.Add(new UserLoginLog
        {
            UserName = model.UserName,
            Email = model.Email,
            LoginTime = DateTime.UtcNow,
            IsSuccessful = false,
            IpAddress = clientIp
        });
        await _context.SaveChangesAsync();

        foreach (var error in result.Errors)
        {
            if (error.Code == "DuplicateEmail" || error.Code == "DuplicateUserName")
                ModelState.AddModelError(string.Empty, "This credential is already in use.");
            else
                ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var user = await _userManager.FindByNameAsync(model.UserName);
        bool isSuccess = false;

        if (user != null)
        {
            var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded) isSuccess = true;
            else if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Account locked due to multiple failed attempts.");
                return View(model);
            }
        }

        _context.UserLoginLogs.Add(new UserLoginLog
        {
            UserName = model.UserName,
            Email = user?.Email ?? "Not Found",
            LoginTime = DateTime.UtcNow,
            IsSuccessful = isSuccess,
            IpAddress = clientIp
        });
        await _context.SaveChangesAsync();

        if (isSuccess) return RedirectToAction("Index", "Home");

        ModelState.AddModelError(string.Empty, "Invalid login transmission.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();
        return View(user);
    }

    [Authorize]
    [HttpGet]
    public IActionResult Settings() => View();

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user == null)
        {
            _context.PasswordResetLogs.Add(new PasswordResetLog
            {
                UserName = "Unknown",
                Email = model.Email,
                ResetTime = DateTime.UtcNow,
                IsSuccessful = false,
                IpAddress = clientIp,
                Note = "Target not found"
            });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetLink = Url.Action("ResetPassword", "Account", new { token, email = user.Email }, Request.Scheme);

        _context.PasswordResetLogs.Add(new PasswordResetLog
        {
            UserName = user.UserName!,
            Email = user.Email!,
            ResetTime = DateTime.UtcNow,
            IsSuccessful = false,
            IpAddress = clientIp,
            Note = "Link generated: " + resetLink
        });
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation() => View();

    [HttpGet]
    public IActionResult ResetPassword(string token, string email)
    {
        if (token == null || email == null) return RedirectToAction("Login");
        return View(new ResetPasswordViewModel { Token = token, Email = email });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null) return RedirectToAction(nameof(ResetPasswordConfirmation));

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var log = new PasswordResetLog
        {
            UserName = user.UserName!,
            Email = user.Email!,
            ResetTime = DateTime.UtcNow,
            IpAddress = clientIp
        };

        if (result.Succeeded)
        {
            log.IsSuccessful = true;
            log.Note = "Success";
            _context.PasswordResetLogs.Add(log);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        log.IsSuccessful = false;
        log.Note = "Failed result";
        _context.PasswordResetLogs.Add(log);
        await _context.SaveChangesAsync();

        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }

    [HttpGet]
    public IActionResult ResetPasswordConfirmation() => View();
}