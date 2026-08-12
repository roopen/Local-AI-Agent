using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalAIAgent.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EncryptAiSettingsApiKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ApiKey",
                table: "AiSettings",
                newName: "ApiKeyCiphertext");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ApiKeyCiphertext",
                table: "AiSettings",
                newName: "ApiKey");
        }
    }
}
