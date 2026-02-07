using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalAIAgent.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fix3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Fido2Credentials_Fido2Id",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Fido2Id",
                table: "Users");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Fido2Id",
                table: "Users",
                type: "BLOB",
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "BLOB",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "Fido2Id",
                table: "Users",
                type: "BLOB",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "BLOB");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Fido2Id",
                table: "Users",
                column: "Fido2Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Fido2Credentials_Fido2Id",
                table: "Users",
                column: "Fido2Id",
                principalTable: "Fido2Credentials",
                principalColumn: "Id");
        }
    }
}
