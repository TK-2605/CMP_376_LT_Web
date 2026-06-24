using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LT_Web_Nhom4.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredMediaFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoredMediaFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredMediaFiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoredMediaFiles_Category_CreatedAt",
                table: "StoredMediaFiles",
                columns: new[] { "Category", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoredMediaFiles");
        }
    }
}
