using Microsoft.AspNetCore.Identity.EntityFrameworkCore; 
using Microsoft.EntityFrameworkCore;
using SilverSet.Models.Entities;
namespace SilverSet.Data
{
    public class SilverSetContext : IdentityDbContext<AppUser, AppRole, int>
    {
        public SilverSetContext(DbContextOptions<SilverSetContext> options) : base(options)
        {
        }
        public DbSet<PasswordResetLog> PasswordResetLogs { get; set; }
        
        public DbSet<UserLoginLog> UserLoginLogs { get; set; }

        public DbSet<Asset> Assets { get; set; }
    }
}
