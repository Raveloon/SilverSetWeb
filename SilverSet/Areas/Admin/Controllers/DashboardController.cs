using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using SilverSet.Data;
using SilverSet.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SilverSet.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly SilverSetContext _context;
    private readonly UserManager<AppUser> _userManager;

    public DashboardController(SilverSetContext context, UserManager<AppUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!User.IsInRole("Admin"))
        {
            context.Result = new RedirectToActionResult("Login", "Account", new { area = "Admin" });
        }
        base.OnActionExecuting(context);
    }
    public IActionResult Index()
    {
        ViewBag.UserCount = _userManager.Users.Count();
        ViewBag.ProjectCount = _context.FeaturedGames.Count();
        ViewBag.VisibleCount = _context.FeaturedGames.Count(g => g.IsVisible);
        return View();
    }
}