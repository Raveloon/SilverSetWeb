using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using SilverSet.Models.Entities;
namespace SilverSet.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class GamesController : Controller
{
    private readonly SilverSetContext _context;

    private readonly IWebHostEnvironment _env;

    public GamesController(SilverSetContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!User.IsInRole("Admin"))
        {
            context.Result = new RedirectToActionResult("Login", "Account", new { area = "Admin" });
        }
        base.OnActionExecuting(context);
    }
    public async Task<IActionResult> Index()
    {
        var games = await _context.FeaturedGames
            .OrderBy(g => g.DisplayOrder)
            .ToListAsync();
        return View(games);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FeaturedGame model, IFormFile? imageFile)
    {
        if (imageFile != null && imageFile.Length > 0)
        {
            // Güvenlik: dosya türü kontrolü
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                ModelState.AddModelError("ImagePath", "Sadece jpg, jpeg, png, webp dosyaları yüklenebilir.");
                return View(model);
            }
            // Güvenlik : dosya türü kontrolü 2
            var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedMimeTypes.Contains(imageFile.ContentType.ToLowerInvariant()))
            {
                ModelState.AddModelError("ImagePath", "Geçersiz dosya türü.");
                return View(model);
            }

            // Max 5MB
            if (imageFile.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError("ImagePath", "Dosya boyutu 5MB'dan küçük olmalı.");
                return View(model);
            }

            var fileName = Guid.NewGuid().ToString() + ext;
            var folderPath = Path.Combine(_env.WebRootPath, "images", "projects");
            Directory.CreateDirectory(folderPath);
            var filePath = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            model.ImagePath = "/images/projects/" + fileName;
        }

        if (ModelState.IsValid)
        {
            _context.FeaturedGames.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Proje başarıyla eklendi.";
            return RedirectToAction(nameof(Index));
        }
        return View(model);
    }
    public async Task<IActionResult> Edit(int id)
    {
        var game = await _context.FeaturedGames.FindAsync(id);
        if (game == null) return NotFound();
        return View(game);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FeaturedGame model, IFormFile? imageFile)
    {
        if (id != model.Id) return NotFound();
        if (imageFile == null || imageFile.Length == 0)
        {
            var existing = await _context.FeaturedGames.AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == id);
            model.ImagePath = existing?.ImagePath;
        }
        if (imageFile != null && imageFile.Length > 0)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                ModelState.AddModelError("ImagePath", "Sadece jpg, jpeg, png, webp dosyaları yüklenebilir.");
                return View(model);
            }
            var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedMimeTypes.Contains(imageFile.ContentType.ToLowerInvariant()))
            {
                ModelState.AddModelError("ImagePath", "Geçersiz dosya türü.");
                return View(model);
            }

            if (imageFile.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError("ImagePath", "Dosya boyutu 5MB'dan küçük olmalı.");
                return View(model);
            }

            if (!string.IsNullOrEmpty(model.ImagePath))
            {
                var oldPath = Path.Combine(_env.WebRootPath, model.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(oldPath))
                    System.IO.File.Delete(oldPath);
            }

            var fileName = Guid.NewGuid().ToString() + ext;
            var folderPath = Path.Combine(_env.WebRootPath, "images", "projects");
            Directory.CreateDirectory(folderPath);
            var filePath = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            model.ImagePath = "/images/projects/" + fileName;
        }

        if (ModelState.IsValid)
        {
            _context.Update(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Proje başarıyla güncellendi.";
            return RedirectToAction(nameof(Index));
        }
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var game = await _context.FeaturedGames.FindAsync(id);
        if (game != null)
        {
            _context.FeaturedGames.Remove(game);
            await _context.SaveChangesAsync();
        }
        TempData["Success"] = "Proje başarıyla silindi.";
        return RedirectToAction(nameof(Index));
    }
}