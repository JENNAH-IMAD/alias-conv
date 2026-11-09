using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankStatementConverter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryOutputTemplateCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntryOutputTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OutputJson = table.Column<string>(type: "jsonb", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryOutputTemplates", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "EntryOutputTemplates",
                columns: new[] { "Id", "Active", "CreatedAt", "Description", "Name", "OutputJson", "UpdatedAt", "Version" },
                values: new object[] { new Guid("9d2e4a78-fb36-4820-9c04-87b370743915"), true, new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "N° compte, Intitulé, Libellé, Banque et Montant HT calculé après TVA.", "Écritures comptables — 5 colonnes", "{\"fileExtension\":\"txt\",\"delimiter\":\";\",\"encoding\":\"utf-8\",\"numberCulture\":\"fr-FR\",\"includeHeader\":true,\"columns\":[{\"field\":\"ACCOUNT\",\"label\":\"N\\u00B0 compte\",\"display\":true,\"export\":true},{\"field\":\"TITLE\",\"label\":\"Intitul\\u00E9\",\"display\":true,\"export\":true},{\"field\":\"DESCRIPTION\",\"label\":\"Libell\\u00E9\",\"display\":true,\"export\":true},{\"field\":\"BANK\",\"label\":\"Banque\",\"display\":true,\"export\":true},{\"field\":\"NET\",\"label\":\"Montant HT\",\"display\":true,\"export\":true}]}", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntryOutputTemplates");
        }
    }
}
