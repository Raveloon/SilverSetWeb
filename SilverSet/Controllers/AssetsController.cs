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


    public async Task<IActionResult> Index(int page = 1)
    {
        int pageSize = 9;
        var query = _context.Assets.AsNoTracking().Where(a => a.IsVisible);

        int totalItems = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var assets = await query
            .OrderBy(a => a.DisplayOrder)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;

        return View(assets);
    }
}