using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalAIAgent.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixFKs2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Fido2Credentials_Fido2Id",
                table: "Users");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Fido2Id",
                table: "Users",
                type: "BLOB",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "BLOB");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Fido2Credentials_Fido2Id",
                table: "Users",
                column: "Fido2Id",
                principalTable: "Fido2Credentials",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Fido2Credentials_Fido2Id",
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

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Fido2Credentials_Fido2Id",
                table: "Users",
                column: "Fido2Id",
                principalTable: "Fido2Credentials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
