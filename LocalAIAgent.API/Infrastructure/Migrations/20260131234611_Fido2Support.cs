using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalAIAgent.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fido2Support : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Fido2Id",
                table: "Users",
                type: "BLOB",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "Fido2User",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fido2User", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Fido2Credentials",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BLOB", nullable: false),
                    RegDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    PublicKey = table.Column<byte[]>(type: "BLOB", nullable: true),
                    Transports = table.Column<string>(type: "TEXT", nullable: true),
                    SignCount = table.Column<uint>(type: "INTEGER", nullable: false),
                    IsBackupEligible = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsBackedUp = table.Column<bool>(type: "INTEGER", nullable: false),
                    AaGuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId1 = table.Column<byte[]>(type: "BLOB", nullable: true),
                    AttestationFormat = table.Column<string>(type: "TEXT", nullable: true),
                    AttestationObject = table.Column<byte[]>(type: "BLOB", nullable: true),
                    AttestationClientDataJson = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fido2Credentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fido2Credentials_Fido2User_UserId1",
                        column: x => x.UserId1,
                        principalTable: "Fido2User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Fido2Credentials_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fido2Credentials_UserId",
                table: "Fido2Credentials",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Fido2Credentials_UserId1",
                table: "Fido2Credentials",
                column: "UserId1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Fido2Credentials");

            migrationBuilder.DropTable(
                name: "Fido2User");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Fido2Id",
                table: "Users");
        }
    }
}
