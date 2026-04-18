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
            // No-op: AddNewsEvaluationEntries now creates the correct schema directly.
            // Production databases that previously ran the original Up() are unaffected
            // because this migration is already recorded in __EFMigrationsHistory.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
