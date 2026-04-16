using LocalAIAgent.API.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.API.Infrastructure;

public class UserContext(DbContextOptions<UserContext> options) : DbContext(options)
{
    public required DbSet<User> Users { get; set; }
    public required DbSet<UserPreferences> UserPreferences { get; set; }
    public required DbSet<Fido2Credential> Fido2Credentials { get; set; }
    public required DbSet<AiSettings> AiSettings { get; set; }
    public required DbSet<NewsArticleFeedback> NewsFeedback { get; set; }
    public required DbSet<NewsEvaluationEntry> NewsEvaluationEntries { get; set; }

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

        modelBuilder.Entity<AiSettings>()
            .HasOne(s => s.UserPreferences)
            .WithOne()
            .HasForeignKey<AiSettings>(s => s.UserPreferencesId);

        modelBuilder.Entity<NewsArticleFeedback>()
            .HasOne(f => f.UserPreferences)
            .WithMany(p => p.FeedbackExamples)
            .HasForeignKey(f => f.UserPreferencesId);

        modelBuilder.Entity<NewsArticleFeedback>()
            .HasIndex(f => new { f.UserPreferencesId, f.ArticleLink })
            .IsUnique();

        modelBuilder.Entity<NewsEvaluationEntry>()
            .HasOne(e => e.UserPreferences)
            .WithMany(p => p.EvaluationEntries)
            .HasForeignKey(e => e.UserPreferencesId);

        modelBuilder.Entity<NewsEvaluationEntry>()
            .HasIndex(e => e.ArticleLink)
            .IsUnique();
    }
}