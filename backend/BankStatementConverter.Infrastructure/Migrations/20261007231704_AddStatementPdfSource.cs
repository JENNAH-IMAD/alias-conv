using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankStatementConverter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStatementPdfSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImportDiagnosticsJson",
                table: "StatementImports",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileName",
                table: "StatementImports",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceStorageKey",
                table: "StatementImports",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImportDiagnosticsJson",
                table: "StatementImports");

            migrationBuilder.DropColumn(
                name: "SourceFileName",
                table: "StatementImports");

            migrationBuilder.DropColumn(
                name: "SourceStorageKey",
                table: "StatementImports");
        }
    }
}
