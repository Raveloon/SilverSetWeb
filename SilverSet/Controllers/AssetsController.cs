using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;

namespace SilverSet.Controllers;

public class AssetsController : Controller
{
    private readonly SilverSetContext _context;

    public AssetsController(SilverSetContext context)
    {
        _context = context;
    }

   
    public async Task<IActionResult> Index()
    {
        var assets = await _context.Assets
            .Where(a => a.IsVisible)
            .OrderBy(a => a.DisplayOrder)
            .ToListAsync();
        return View(assets);
    }
}