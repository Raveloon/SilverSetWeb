using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Identity;
using SilverSet.Models.Entities;

namespace SilverSet.Filters;

public class BannedUserFilter : IAsyncActionFilter
{
    private readonly SignInManager<AppUser> _signInManager;
    private readonly UserManager<AppUser> _userManager;

    public BannedUserFilter(SignInManager<AppUser> signInManager, UserManager<AppUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Eğer bir kullanıcı sisteme giriş yapmışsa...
        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(context.HttpContext.User);

            // Veritabanından adamı bulup banlı olup olmadığına bakıyoruz.
            if (user != null && user.IsBanned)
            {
                // Banlıysa anında oturumunu (Cookie) imha et
                await _signInManager.SignOutAsync();

                // Ve onu Login sayfasına şutla
                context.Result = new RedirectToActionResult("Login", "Account", new { area = "" });
                return; // Kodun aşağıya inmesini ve sayfayı yüklemesini engelle
            }
        }

        // Temizse devam etsin
        await next();
    }
}