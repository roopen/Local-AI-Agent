using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalAIAgent.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StoreDatasetCollectionInLlmSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UseResultsForDataset",
                table: "AiSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UseResultsForDataset",
                table: "AiSettings");
        }
    }
}
