using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankStatementConverter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClientArchives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExportLabel",
                table: "History",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StandardStorageKey",
                table: "History",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PendingFileDeletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingFileDeletions", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingFileDeletions");

            migrationBuilder.DropColumn(
                name: "ExportLabel",
                table: "History");

            migrationBuilder.DropColumn(
                name: "StandardStorageKey",
                table: "History");
        }
    }
}
