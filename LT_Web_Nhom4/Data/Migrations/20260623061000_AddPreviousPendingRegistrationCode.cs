using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LT_Web_Nhom4.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPreviousPendingRegistrationCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviousConfirmationCodeHash",
                table: "PendingRegistrations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousConfirmationTokenHash",
                table: "PendingRegistrations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreviousExpiresAtUtc",
                table: "PendingRegistrations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousTokenSalt",
                table: "PendingRegistrations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviousConfirmationCodeHash",
                table: "PendingRegistrations");

            migrationBuilder.DropColumn(
                name: "PreviousConfirmationTokenHash",
                table: "PendingRegistrations");

            migrationBuilder.DropColumn(
                name: "PreviousExpiresAtUtc",
                table: "PendingRegistrations");

            migrationBuilder.DropColumn(
                name: "PreviousTokenSalt",
                table: "PendingRegistrations");
        }
    }
}
