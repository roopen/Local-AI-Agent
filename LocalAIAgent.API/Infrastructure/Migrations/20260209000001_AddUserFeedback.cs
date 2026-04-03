using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalAIAgent.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NewsFeedback",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ArticleLink = table.Column<string>(type: "TEXT", nullable: false),
                    ArticleTitle = table.Column<string>(type: "TEXT", nullable: false),
                    ArticleSummary = table.Column<string>(type: "TEXT", nullable: false),
                    IsLiked = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserPreferencesId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsFeedback_UserPreferences_UserPreferencesId",
                        column: x => x.UserPreferencesId,
                        principalTable: "UserPreferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NewsFeedback_UserPreferencesId_ArticleLink",
                table: "NewsFeedback",
                columns: new[] { "UserPreferencesId", "ArticleLink" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewsFeedback");
        }
    }
}
