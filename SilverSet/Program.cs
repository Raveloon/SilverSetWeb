using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using SilverSet.Models.Entities;

namespace SilverSet
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // DB
            builder.Services.AddDbContext<SilverSetContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Identity
            builder.Services.AddIdentity<AppUser, AppRole>(options =>
            {
                options.Password.RequiredLength = 6;
                options.Password.RequireDigit = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.User.RequireUniqueEmail = true;

                // Lockout ayarlarý
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;
            }).AddEntityFrameworkStores<SilverSetContext>().AddDefaultTokenProviders();

            builder.Services.AddControllersWithViews();

            builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
            {
                options.TokenLifespan = TimeSpan.FromMinutes(15);
            });

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Admin/Account/Login";
                options.AccessDeniedPath = "/Admin/Account/Login";

                // Güvenlik ayarlarý
                options.Cookie.HttpOnly = true;      
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;  
                options.Cookie.SameSite = SameSiteMode.Strict; 
                options.ExpireTimeSpan = TimeSpan.FromHours(2); 
                options.SlidingExpiration = false;     
            });

            var app = builder.Build();

            // Pipeline
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value?.ToLower() ?? "";

                bool isSensitive = path.StartsWith("/account") ||
                                   path.StartsWith("/admin") ||
                                   path.StartsWith("/manage");

                if (isSensitive)
                {
                    context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                    context.Response.Headers["Pragma"] = "no-cache";
                    context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
                }

                await next();
            });
            app.UseHttpsRedirection();
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

            // Admin seeding
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    var userManager = services.GetRequiredService<UserManager<AppUser>>();
                    var roleManager = services.GetRequiredService<RoleManager<AppRole>>();
                    var configuration = services.GetRequiredService<IConfiguration>();

                    // Admin rolü yoksa oluþtur
                    if (!await roleManager.RoleExistsAsync("Admin"))
                    {
                        await roleManager.CreateAsync(new AppRole { Name = "Admin" });
                    }

                    // TODO: Data seeding — appsettings'ten oku, veri sýzýntýsýna dikkat!
                    string adminName = configuration["AdminSetup:AdminName"];
                    string adminPassword = configuration["AdminSetup:Password"];

                    var user = await userManager.FindByNameAsync(adminName);
                    if (user == null)
                    {
                        var adminUser = new AppUser
                        {
                            UserName = adminName,
                            EmailConfirmed = true
                        };

                        var result = await userManager.CreateAsync(adminUser, adminPassword);
                        if (result.Succeeded)
                        {
                            await userManager.AddToRoleAsync(adminUser, "Admin");
                        }
                    }
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "Seeding iþlemi sýrasýnda bir hata oluþtu.");
                }
            }

            await app.RunAsync();
        }
    }
}
