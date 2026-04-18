using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalAIAgent.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UniqueArticleLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NewsEvaluationEntries_UserPreferencesId_ArticleLink",
                table: "NewsEvaluationEntries");

            migrationBuilder.CreateIndex(
                name: "IX_NewsEvaluationEntries_ArticleLink",
                table: "NewsEvaluationEntries",
                column: "ArticleLink",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsEvaluationEntries_UserPreferencesId",
                table: "NewsEvaluationEntries",
                column: "UserPreferencesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NewsEvaluationEntries_ArticleLink",
                table: "NewsEvaluationEntries");

            migrationBuilder.DropIndex(
                name: "IX_NewsEvaluationEntries_UserPreferencesId",
                table: "NewsEvaluationEntries");

            migrationBuilder.CreateIndex(
                name: "IX_NewsEvaluationEntries_UserPreferencesId_ArticleLink",
                table: "NewsEvaluationEntries",
                columns: new[] { "UserPreferencesId", "ArticleLink" },
                unique: true);
        }
    }
}
