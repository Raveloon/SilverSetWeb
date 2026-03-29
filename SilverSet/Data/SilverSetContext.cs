using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SilverSet.Models.Entities;
using SilverSet.Models.Entities.Forum;

namespace SilverSet.Data;

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

    public DbSet<ForumCategory> ForumCategories { get; set; }
    public DbSet<ForumTopic> ForumTopics { get; set; }
    public DbSet<ForumPost> ForumPosts { get; set; }
    public DbSet<BadWord> BadWords { get; set; }
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<TicketMessage>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ForumTopic>()
            .HasOne(t => t.Category)
            .WithMany(c => c.Topics)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ForumTopic>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ForumPost>()
            .HasOne(p => p.Topic)
            .WithMany(t => t.Posts)
            .HasForeignKey(p => p.TopicId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ForumPost>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}