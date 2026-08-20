using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalAIAgent.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultipleLlmOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SelectedAiSettingsId",
                table: "UserPreferences",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "AiSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE \"AiSettings\" SET \"Name\" = \"ModelId\" WHERE \"Name\" = '';");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_SelectedAiSettingsId",
                table: "UserPreferences",
                column: "SelectedAiSettingsId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserPreferences_AiSettings_SelectedAiSettingsId",
                table: "UserPreferences",
                column: "SelectedAiSettingsId",
                principalTable: "AiSettings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPreferences_AiSettings_SelectedAiSettingsId",
                table: "UserPreferences");

            migrationBuilder.DropIndex(
                name: "IX_UserPreferences_SelectedAiSettingsId",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "SelectedAiSettingsId",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "AiSettings");
        }
    }
}
