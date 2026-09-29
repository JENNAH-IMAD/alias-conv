using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankStatementConverter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StatementAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "History",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedByName",
                table: "History",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedByUserId",
                table: "History",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedAt",
                table: "History",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessedByName",
                table: "History",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcessedByUserId",
                table: "History",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "History");

            migrationBuilder.DropColumn(
                name: "ModifiedByName",
                table: "History");

            migrationBuilder.DropColumn(
                name: "ModifiedByUserId",
                table: "History");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                table: "History");

            migrationBuilder.DropColumn(
                name: "ProcessedByName",
                table: "History");

            migrationBuilder.DropColumn(
                name: "ProcessedByUserId",
                table: "History");
        }
    }
}
