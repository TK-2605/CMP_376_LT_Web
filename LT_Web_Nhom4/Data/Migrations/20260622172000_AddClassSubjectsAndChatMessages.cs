using System;
using LT_Web_Nhom4.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LT_Web_Nhom4.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260622172000_AddClassSubjectsAndChatMessages")]
    public partial class AddClassSubjectsAndChatMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Subjects",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoomType = table.Column<string>(maxLength: 20, nullable: false),
                    RoomId = table.Column<int>(nullable: false),
                    SenderId = table.Column<string>(maxLength: 450, nullable: false),
                    SenderName = table.Column<string>(maxLength: 150, nullable: false),
                    Message = table.Column<string>(maxLength: 500, nullable: false),
                    SentAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_OwnerId",
                table: "Subjects",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_RoomType_RoomId_SentAt",
                table: "ChatMessages",
                columns: new[] { "RoomType", "RoomId", "SentAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Subjects_AspNetUsers_OwnerId",
                table: "Subjects",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subjects_AspNetUsers_OwnerId",
                table: "Subjects");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_Subjects_OwnerId",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Subjects");
        }
    }
}
