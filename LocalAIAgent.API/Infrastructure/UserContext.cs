using LocalAIAgent.API.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.API.Infrastructure;

public class UserContext(DbContextOptions<UserContext> options) : DbContext(options)
{
    public required DbSet<User> Users { get; set; }
    public required DbSet<UserPreferences> UserPreferences { get; set; }
    public required DbSet<Fido2Credential> Fido2Credentials { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasOne(u => u.Preferences)
            .WithOne(p => p.User)
            .HasForeignKey<UserPreferences>(p => p.UserId);

        modelBuilder.Entity<Fido2Credential>()
            .HasOne(c => c.Owner)
            .WithMany(u => u.Fido2Credentials)
            .HasForeignKey(c => c.UserId);
    }
}