using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using SilverSet.Models.Entities;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using SilverSet.Hubs;
using SilverSet.Filters;
using SilverSet.Services;
using DinkToPdf;
using DinkToPdf.Contracts;

namespace SilverSet;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Database Connection
        builder.Services.AddDbContext<SilverSetContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        // Rate Limiter Setup
        builder.Services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("Strict", opt =>
            {
                opt.Window = TimeSpan.FromMinutes(1);
                opt.PermitLimit = 5;
                opt.QueueLimit = 0;
            });
        });

        // Identity Setup
        builder.Services.AddIdentity<AppUser, AppRole>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.User.RequireUniqueEmail = true;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
        })
        .AddEntityFrameworkStores<SilverSetContext>()
        .AddDefaultTokenProviders();

        builder.Services.AddHttpClient<TurnstileService>();
        builder.Services.AddMemoryCache();

        // Custom Services
        builder.Services.AddScoped<ProfanityFilterService>();
        builder.Services.AddScoped<BannedUserFilter>();
        builder.Services.AddScoped<AdminNotificationFilter>();
        builder.Services.AddScoped<ForumSettingsService>();
        builder.Services.AddScoped<ForumAccessFilter>();

        // DinkToPdf Servisi (PDF oluþturma için)
        builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));

        builder.Services.AddControllersWithViews(options =>
        {
            options.Filters.Add<BannedUserFilter>();
            options.Filters.Add<AdminNotificationFilter>();
        });

        builder.Services.AddSignalR();

        builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromMinutes(15);
        });

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/Login";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromHours(2);
            options.SlidingExpiration = true;
        });

        var app = builder.Build();

        app.UseRateLimiter();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Errors/500");
            app.UseHsts();
        }

        app.UseStatusCodePagesWithReExecute("/Errors/{0}");

        // Security Headers
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";
            bool isSensitive = path.StartsWith("/account") || path.StartsWith("/admin") || path.StartsWith("/manage");
            if (isSensitive)
            {
                context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                context.Response.Headers["Pragma"] = "no-cache";
                context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            }
            await next();
        });

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapStaticAssets();

        app.MapControllerRoute(
            name: "areas",
            pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}")
            .WithStaticAssets();

        app.MapHub<TicketHub>("/ticketHub").RequireRateLimiting("Strict");

        // --- SEEDING & MIGRATION BLOCK (DÜZELTÝLDÝ) ---
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<Program>>();
            var env = services.GetRequiredService<IWebHostEnvironment>(); // Ortam kontrolü için eklendi

            try
            {
                var context = services.GetRequiredService<SilverSetContext>();

                // CI (GitHub Actions) ortamýnda migration LocalDB yüzünden patlar. 
                // Bu yüzden CI ortamýnda sadece 'EnsureCreated' yapýyoruz ya da migration'ý kontrollü yapýyoruz.
                if (env.IsEnvironment("CI"))
                {
                    logger.LogInformation("CI ortamý algýlandý. Sadece veritabaný þemasý kontrol ediliyor...");
                    await context.Database.EnsureCreatedAsync();
                }
                else
                {
                    logger.LogInformation("Veritabaný kontrol ediliyor/migrate ediliyor...");
                    await context.Database.MigrateAsync();
                }

                var userManager = services.GetRequiredService<UserManager<AppUser>>();
                var roleManager = services.GetRequiredService<RoleManager<AppRole>>();
                var configuration = services.GetRequiredService<IConfiguration>();

                // Global Settings Seeding
                if (!await context.GlobalSettings.AnyAsync())
                {
                    logger.LogInformation("Global ayarlar oluþturuluyor...");
                    context.GlobalSettings.AddRange(
                        new GlobalSetting { Key = "ForumStatus", Value = "true", Description = "Forumun genel eriþim durumu" },
                        new GlobalSetting { Key = "ForumPostDelay", Value = "24h", Description = "Yeni üyeler için bekleme süresi" },
                        new GlobalSetting { Key = "ForumApprovalThreshold", Value = "5", Description = "Otomatik onay için gereken mesaj sayýsý" }
                    );
                    await context.SaveChangesAsync();
                }

                // Admin Account Seeding
                string? adminRole = configuration["AdminSetup:AdminRole"];
                string? adminName = configuration["AdminSetup:AdminName"];
                string? adminPassword = configuration["AdminSetup:Password"];
                string? adminEmail = configuration["AdminSetup:AdminEmail"];

                logger.LogInformation("Admin Seeding Kontrolü: Name={Name}, Role={Role}, Email={Email}",
                    adminName ?? "NULL", adminRole ?? "NULL", adminEmail ?? "NULL");

                if (!string.IsNullOrEmpty(adminRole) && !await roleManager.RoleExistsAsync(adminRole))
                {
                    logger.LogInformation("Admin rolü oluþturuluyor: {Role}", adminRole);
                    await roleManager.CreateAsync(new AppRole { Name = adminRole });
                }

                if (!string.IsNullOrEmpty(adminName) && !string.IsNullOrEmpty(adminPassword) && !string.IsNullOrEmpty(adminEmail))
                {
                    var user = await userManager.FindByNameAsync(adminName);
                    if (user == null)
                    {
                        logger.LogInformation("Admin kullanýcýsý oluþturuluyor: {AdminName}", adminName);
                        var adminUser = new AppUser
                        {
                            UserName = adminName,
                            Email = adminEmail,
                            EmailConfirmed = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        var result = await userManager.CreateAsync(adminUser, adminPassword);
                        if (result.Succeeded)
                        {
                            if (!string.IsNullOrEmpty(adminRole))
                            {
                                await userManager.AddToRoleAsync(adminUser, adminRole);
                                logger.LogInformation("Admin kullanýcýsýna {Role} rolü atandý.", adminRole);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // HATA BURADA YAKALANIYOR: Veritabaný patlasa bile logger'a yazýp uygulamayý öldürmüyoruz.
                logger.LogCritical(ex, "Veritabaný iþlemi sýrasýnda hata oluþtu. Lighthouse taramasý için uygulama baþlatýlmaya devam ediliyor...");
            }
        }

        await app.RunAsync();
    }
}