using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalAIAgent.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsEvaluationEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NewsEvaluationEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ArticleTitle = table.Column<string>(type: "TEXT", nullable: false),
                    ArticleSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ArticleLink = table.Column<string>(type: "TEXT", nullable: false),
                    ArticleSource = table.Column<string>(type: "TEXT", nullable: false),
                    ArticleTopic = table.Column<string>(type: "TEXT", nullable: true),
                    Relevancy = table.Column<string>(type: "TEXT", nullable: false),
                    Reasoning = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserPreferencesId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsEvaluationEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsEvaluationEntries_UserPreferences_UserPreferencesId",
                        column: x => x.UserPreferencesId,
                        principalTable: "UserPreferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
            migrationBuilder.DropTable(
                name: "NewsEvaluationEntries");
        }
    }
}
