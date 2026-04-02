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
        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(context.HttpContext.User);

            if (user != null && user.IsBanned)
            {
                await _signInManager.SignOutAsync();
                context.Result = new RedirectToActionResult("Login", "Account", new { area = "" });
                return;
            }
        }

        await next();
    }
}