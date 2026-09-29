using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankStatementConverter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClientProfilePhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Photo",
                table: "Clients",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoContentType",
                table: "Clients",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PhotoVersion",
                table: "Clients",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Photo",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PhotoContentType",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PhotoVersion",
                table: "Clients");
        }
    }
}
