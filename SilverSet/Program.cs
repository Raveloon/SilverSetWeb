using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using SilverSet.Models.Entities;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using SilverSet.Hubs;
using SilverSet.Filters;
using SilverSet.Services;

namespace SilverSet;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContext<SilverSetContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("Strict", opt =>
            {
                opt.Window = TimeSpan.FromMinutes(1);
                opt.PermitLimit = 5;
                opt.QueueLimit = 0;
            });
        });

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
        }).AddEntityFrameworkStores<SilverSetContext>().AddDefaultTokenProviders();

        builder.Services.AddHttpClient<TurnstileService>();
        builder.Services.AddMemoryCache();

        builder.Services.AddScoped<ProfanityFilterService>();
        builder.Services.AddScoped<BannedUserFilter>();
        builder.Services.AddScoped<AdminNotificationFilter>();

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
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromHours(2);
            options.SlidingExpiration = false;
        });

        var app = builder.Build();

        app.UseRateLimiter();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Errors/500");
            app.UseHsts();
        }

        app.UseStatusCodePagesWithReExecute("/Errors/{0}");

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

        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            try
            {
                var userManager = services.GetRequiredService<UserManager<AppUser>>();
                var roleManager = services.GetRequiredService<RoleManager<AppRole>>();
                var configuration = services.GetRequiredService<IConfiguration>();

                string? adminRole = configuration["AdminSetup:AdminRole"];
                string? adminName = configuration["AdminSetup:AdminName"];
                string? adminPassword = configuration["AdminSetup:Password"];
                string? adminEmail = configuration["AdminSetup:AdminEmail"];

                if (!string.IsNullOrEmpty(adminRole) && !await roleManager.RoleExistsAsync(adminRole))
                {
                    await roleManager.CreateAsync(new AppRole { Name = adminRole });
                }

                if (!string.IsNullOrEmpty(adminName) && !string.IsNullOrEmpty(adminPassword) && !string.IsNullOrEmpty(adminEmail))
                {
                    var user = await userManager.FindByNameAsync(adminName);
                    if (user == null)
                    {
                        var adminUser = new AppUser
                        {
                            UserName = adminName,
                            Email = adminEmail,
                            EmailConfirmed = true,
                            CreatedAt = DateTime.UtcNow
                        };
                        var result = await userManager.CreateAsync(adminUser, adminPassword);

                        if (result.Succeeded && !string.IsNullOrEmpty(adminRole))
                        {
                            await userManager.AddToRoleAsync(adminUser, adminRole);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "An error occurred while seeding the database.");
            }
        }

        await app.RunAsync();
    }
}