using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankStatementConverter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BankLogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Logo",
                table: "Banks",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoContentType",
                table: "Banks",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LogoVersion",
                table: "Banks",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Logo",
                table: "Banks");

            migrationBuilder.DropColumn(
                name: "LogoContentType",
                table: "Banks");

            migrationBuilder.DropColumn(
                name: "LogoVersion",
                table: "Banks");
        }
    }
}
