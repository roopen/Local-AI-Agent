using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalAIAgent.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LocalServerInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiSettings_UserPreferences_UserPreferencesId",
                table: "AiSettings");

            migrationBuilder.DropIndex(
                name: "IX_NewsEvaluationEntries_ArticleLink",
                table: "NewsEvaluationEntries");

            migrationBuilder.DropIndex(
                name: "IX_NewsEvaluationEntries_UserPreferencesId",
                table: "NewsEvaluationEntries");

            migrationBuilder.DropIndex(
                name: "IX_AiSettings_UserPreferencesId",
                table: "AiSettings");

            // The runtime was already process-wide. Preserve the owner's row when possible,
            // otherwise keep the earliest configured row as the new server-level setting.
            migrationBuilder.Sql(
                """
                DELETE FROM AiSettings
                WHERE Id NOT IN (
                    SELECT a.Id
                    FROM AiSettings AS a
                    INNER JOIN UserPreferences AS p ON p.Id = a.UserPreferencesId
                    ORDER BY CASE WHEN p.UserId = (SELECT MIN(Id) FROM Users) THEN 0 ELSE 1 END, a.Id
                    LIMIT 1
                );
                """);

            migrationBuilder.DropColumn(
                name: "UserPreferencesId",
                table: "AiSettings");

            migrationBuilder.AddColumn<bool>(
                name: "IsDisabled",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "Member");

            migrationBuilder.Sql(
                "UPDATE Users SET Role = 'Owner' WHERE Id = (SELECT MIN(Id) FROM Users);");

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TokenHash = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RedeemedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RedeemedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invitations_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitations_Users_RedeemedByUserId",
                        column: x => x.RedeemedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NewsEvaluationEntries_UserPreferencesId_ArticleLink",
                table: "NewsEvaluationEntries",
                columns: new[] { "UserPreferencesId", "ArticleLink" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_CreatedByUserId",
                table: "Invitations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_RedeemedByUserId",
                table: "Invitations",
                column: "RedeemedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TokenHash",
                table: "Invitations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_NewsEvaluationEntries_UserPreferencesId_ArticleLink",
                table: "NewsEvaluationEntries");

            migrationBuilder.DropColumn(
                name: "IsDisabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            migrationBuilder.AddColumn<int>(
                name: "UserPreferencesId",
                table: "AiSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE AiSettings
                SET UserPreferencesId = COALESCE(
                    (SELECT Id FROM UserPreferences ORDER BY UserId LIMIT 1),
                    0
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_NewsEvaluationEntries_ArticleLink",
                table: "NewsEvaluationEntries",
                column: "ArticleLink",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsEvaluationEntries_UserPreferencesId",
                table: "NewsEvaluationEntries",
                column: "UserPreferencesId");

            migrationBuilder.CreateIndex(
                name: "IX_AiSettings_UserPreferencesId",
                table: "AiSettings",
                column: "UserPreferencesId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AiSettings_UserPreferences_UserPreferencesId",
                table: "AiSettings",
                column: "UserPreferencesId",
                principalTable: "UserPreferences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
