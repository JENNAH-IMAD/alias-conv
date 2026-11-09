using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankStatementConverter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIndependentEntryArchives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountingEntryArchives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatementId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    BankName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SourceFileName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ModelName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SourceVersion = table.Column<int>(type: "integer", nullable: false),
                    DossierVersion = table.Column<int>(type: "integer", nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Encoding = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Delimiter = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PreviewFingerprint = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    GeneratedByName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingEntryArchives", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntryArchives_ClientId_CreatedAt",
                table: "AccountingEntryArchives",
                columns: new[] { "ClientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntryArchives_ClientId_StatementId_PreviewFingerp~",
                table: "AccountingEntryArchives",
                columns: new[] { "ClientId", "StatementId", "PreviewFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntryArchives_StatementId_CreatedAt",
                table: "AccountingEntryArchives",
                columns: new[] { "StatementId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingEntryArchives");
        }
    }
}
