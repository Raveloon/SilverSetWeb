using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SilverSet.Data;
using SilverSet.Models.Entities;

namespace SilverSet
{
    public class Program
    {
        // Main metodu await kullanabilmek için async Task yapýldý
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
            }).AddEntityFrameworkStores<SilverSetContext>();

            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            // To do : pipeline check et 
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

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

            
            
            
            using (var scope = app.Services.CreateScope()) 
            {
                var services = scope.ServiceProvider;
                try
                {
                    var userManager = services.GetRequiredService<UserManager<AppUser>>();
                    var roleManager = services.GetRequiredService<RoleManager<AppRole>>(); 
                    var configuration = services.GetRequiredService<IConfiguration>();

                    
                    if (!await roleManager.RoleExistsAsync("Admin"))
                    {
                        await roleManager.CreateAsync(new AppRole { Name = "Admin" });
                    }

                    // To do : Data seeding kontrol et veri sýzýntýsý !
                    string adminName = configuration["AdminSetup:AdminName"] ;
                    string adminPassword = configuration["AdminSetup:Password"] ;

                    
                    var user = await userManager.FindByNameAsync(adminName);
                    if (user == null)
                    {
                        var adminUser = new AppUser
                        {
                            UserName = adminName,
                            FirstName = "Starter Admin",
                            LastName = "001",
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