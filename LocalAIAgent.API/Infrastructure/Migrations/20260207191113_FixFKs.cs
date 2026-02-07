using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalAIAgent.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixFKs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Fido2Credentials_Fido2User_UserId1",
                table: "Fido2Credentials");

            migrationBuilder.DropForeignKey(
                name: "FK_Fido2Credentials_Users_UserId",
                table: "Fido2Credentials");

            migrationBuilder.DropTable(
                name: "Fido2User");

            migrationBuilder.DropIndex(
                name: "IX_Fido2Credentials_UserId1",
                table: "Fido2Credentials");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "Fido2Credentials");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Fido2Credentials",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "UserFido2Id",
                table: "Fido2Credentials",
                type: "BLOB",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Fido2Id",
                table: "Users",
                column: "Fido2Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Fido2Credentials_Users_UserId",
                table: "Fido2Credentials",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Fido2Credentials_Fido2Id",
                table: "Users",
                column: "Fido2Id",
                principalTable: "Fido2Credentials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Fido2Credentials_Users_UserId",
                table: "Fido2Credentials");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Fido2Credentials_Fido2Id",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Fido2Id",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UserFido2Id",
                table: "Fido2Credentials");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Fido2Credentials",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<byte[]>(
                name: "UserId1",
                table: "Fido2Credentials",
                type: "BLOB",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Fido2User",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BLOB", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fido2User", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Fido2Credentials_UserId1",
                table: "Fido2Credentials",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Fido2Credentials_Fido2User_UserId1",
                table: "Fido2Credentials",
                column: "UserId1",
                principalTable: "Fido2User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Fido2Credentials_Users_UserId",
                table: "Fido2Credentials",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
