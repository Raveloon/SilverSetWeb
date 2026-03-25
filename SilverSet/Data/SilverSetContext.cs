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
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<TicketMessage> TicketMessages { get; set; }
        public DbSet<Asset> Assets { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<TicketMessage>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}