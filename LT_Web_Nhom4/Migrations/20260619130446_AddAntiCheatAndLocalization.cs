using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LT_Web_Nhom4.Migrations
{
    /// <inheritdoc />
    public partial class AddAntiCheatAndLocalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AntiCheatEvents_ExamAttemptId",
                table: "AntiCheatEvents");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AntiCheatEvents",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ExamId",
                table: "AntiCheatEvents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuspicious",
                table: "AntiCheatEvents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "AntiCheatEvents",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "AntiCheatEvents",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ViolationCount",
                table: "AntiCheatEvents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AntiCheatEvents_ExamAttemptId_CreatedAt",
                table: "AntiCheatEvents",
                columns: new[] { "ExamAttemptId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AntiCheatEvents_ExamId",
                table: "AntiCheatEvents",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_AntiCheatEvents_UserId",
                table: "AntiCheatEvents",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AntiCheatEvents_AspNetUsers_UserId",
                table: "AntiCheatEvents",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AntiCheatEvents_Exams_ExamId",
                table: "AntiCheatEvents",
                column: "ExamId",
                principalTable: "Exams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AntiCheatEvents_AspNetUsers_UserId",
                table: "AntiCheatEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_AntiCheatEvents_Exams_ExamId",
                table: "AntiCheatEvents");

            migrationBuilder.DropIndex(
                name: "IX_AntiCheatEvents_ExamAttemptId_CreatedAt",
                table: "AntiCheatEvents");

            migrationBuilder.DropIndex(
                name: "IX_AntiCheatEvents_ExamId",
                table: "AntiCheatEvents");

            migrationBuilder.DropIndex(
                name: "IX_AntiCheatEvents_UserId",
                table: "AntiCheatEvents");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AntiCheatEvents");

            migrationBuilder.DropColumn(
                name: "ExamId",
                table: "AntiCheatEvents");

            migrationBuilder.DropColumn(
                name: "IsSuspicious",
                table: "AntiCheatEvents");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "AntiCheatEvents");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "AntiCheatEvents");

            migrationBuilder.DropColumn(
                name: "ViolationCount",
                table: "AntiCheatEvents");

            migrationBuilder.CreateIndex(
                name: "IX_AntiCheatEvents_ExamAttemptId",
                table: "AntiCheatEvents",
                column: "ExamAttemptId");
        }
    }
}
