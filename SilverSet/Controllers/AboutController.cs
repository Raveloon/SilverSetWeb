using Microsoft.AspNetCore.Mvc;

namespace SilverSet.Controllers;

public class AboutController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}