using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using SilverSet.Models;
using SilverSet.Models.Entities;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using System.Threading.Tasks;


namespace SilverSet.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly UserManager<AppUser> _userManager;
    private readonly SilverSetContext _context;
    public HomeController(ILogger<HomeController> logger, UserManager<AppUser> userManager,SilverSetContext context)
    {
        _logger = logger;
        _userManager = userManager;
        _context = context; 
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.UserCount = _userManager.Users.Count();
         
        var games = await _context.Assets.Where(g => g.IsVisible).OrderBy(g => g.DisplayOrder).ToListAsync();    

        return View(games);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
