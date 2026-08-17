using LocalAIAgent.API.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.API.Infrastructure;

public class UserContext(DbContextOptions<UserContext> options) : DbContext(options)
{
    public required DbSet<User> Users { get; set; }
    public required DbSet<UserPreferences> UserPreferences { get; set; }
    public required DbSet<Fido2Credential> Fido2Credentials { get; set; }
    public required DbSet<AiSettings> AiSettings { get; set; }
    public required DbSet<NewsEvaluationEntry> NewsEvaluationEntries { get; set; }
    public required DbSet<ArticleTranslation> ArticleTranslations { get; set; }
    public required DbSet<CustomFeed> CustomFeeds { get; set; }
    public required DbSet<Invitation> Invitations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasConversion<string>();

        modelBuilder.Entity<User>()
            .HasOne(u => u.Preferences)
            .WithOne(p => p.User)
            .HasForeignKey<UserPreferences>(p => p.UserId);

        modelBuilder.Entity<Fido2Credential>()
            .HasOne(c => c.Owner)
            .WithMany(u => u.Fido2Credentials)
            .HasForeignKey(c => c.UserId);

        modelBuilder.Entity<Invitation>()
            .HasIndex(i => i.TokenHash)
            .IsUnique();

        modelBuilder.Entity<Invitation>()
            .HasOne(i => i.CreatedByUser)
            .WithMany(u => u.CreatedInvitations)
            .HasForeignKey(i => i.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Invitation>()
            .HasOne(i => i.RedeemedByUser)
            .WithMany()
            .HasForeignKey(i => i.RedeemedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<NewsEvaluationEntry>()
            .HasOne(e => e.UserPreferences)
            .WithMany(p => p.EvaluationEntries)
            .HasForeignKey(e => e.UserPreferencesId);

        modelBuilder.Entity<NewsEvaluationEntry>()
            .HasIndex(e => new { e.UserPreferencesId, e.ArticleLink })
            .IsUnique();

        modelBuilder.Entity<ArticleTranslation>()
            .HasIndex(t => new { t.ArticleLink, t.TargetLanguage })
            .IsUnique();

        modelBuilder.Entity<CustomFeed>()
            .HasOne(f => f.UserPreferences)
            .WithMany(p => p.CustomFeeds)
            .HasForeignKey(f => f.UserPreferencesId);
    }
}
