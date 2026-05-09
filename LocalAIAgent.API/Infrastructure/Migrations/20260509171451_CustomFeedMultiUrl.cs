using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalAIAgent.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CustomFeedMultiUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomFeeds_UserPreferencesId_Url",
                table: "CustomFeeds");

            migrationBuilder.RenameColumn(
                name: "Url",
                table: "CustomFeeds",
                newName: "Urls");

            // Convert any pre-existing single-URL string values into a JSON array so EF's
            // PrimitiveCollection<string> projection deserializes them correctly. SQLite's
            // json_array() wraps the scalar value into a one-element JSON array.
            migrationBuilder.Sql(
                "UPDATE CustomFeeds SET Urls = json_array(Urls) " +
                "WHERE Urls IS NOT NULL AND Urls != '' AND Urls NOT LIKE '[%]'");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFeeds_UserPreferencesId",
                table: "CustomFeeds",
                column: "UserPreferencesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomFeeds_UserPreferencesId",
                table: "CustomFeeds");

            migrationBuilder.RenameColumn(
                name: "Urls",
                table: "CustomFeeds",
                newName: "Url");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFeeds_UserPreferencesId_Url",
                table: "CustomFeeds",
                columns: new[] { "UserPreferencesId", "Url" },
                unique: true);
        }
    }
}
