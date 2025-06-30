using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.API.Infrastructure;

public class UserContext(DbContextOptions<UserContext> options) : DbContext(options)
{
    public required DbSet<User> Users { get; set; }
    public required DbSet<UserPreference> UserPreferences { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasOne(u => u.Preferences)
            .WithOne(p => p.User)
            .HasForeignKey<UserPreference>(p => p.UserId);
    }
}