using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalAIAgent.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedPreferencesAndTranslationLanguageIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ArticleTranslations_ArticleLink",
                table: "ArticleTranslations");

            migrationBuilder.AddColumn<string>(
                name: "DisabledFeedSources",
                table: "UserPreferences",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "TargetLanguage",
                table: "UserPreferences",
                type: "TEXT",
                nullable: false,
                defaultValue: "en");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleTranslations_ArticleLink_TargetLanguage",
                table: "ArticleTranslations",
                columns: new[] { "ArticleLink", "TargetLanguage" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ArticleTranslations_ArticleLink_TargetLanguage",
                table: "ArticleTranslations");

            migrationBuilder.DropColumn(
                name: "DisabledFeedSources",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "TargetLanguage",
                table: "UserPreferences");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleTranslations_ArticleLink",
                table: "ArticleTranslations",
                column: "ArticleLink",
                unique: true);
        }
    }
}
