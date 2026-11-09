using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankStatementConverter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveJournalsAndPlansPreserveEntryModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientEntryModelSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientEntryModelSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientEntryModelSets_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientEntryModelSets_ClientId",
                table: "ClientEntryModelSets",
                column: "ClientId",
                unique: true);
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "AccountingDossiers"
                    WHERE jsonb_typeof(COALESCE(NULLIF("SettingsJson"->'entryModels', 'null'::jsonb), '[]'::jsonb)) <> 'array') THEN
                    RAISE EXCEPTION 'Entry models must be an array; journal/plan removal was cancelled.';
                  END IF;
                END $$;
                INSERT INTO "ClientEntryModelSets" ("Id", "ClientId", "ModelsJson", "Version", "CreatedAt", "UpdatedAt")
                SELECT "Id", "ClientId", COALESCE(NULLIF("SettingsJson"->'entryModels', 'null'::jsonb), '[]'::jsonb), "Version", "CreatedAt", "UpdatedAt"
                FROM "AccountingDossiers";
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "AccountingDossiers" d LEFT JOIN "ClientEntryModelSets" m ON m."ClientId"=d."ClientId"
                    WHERE m."ClientId" IS NULL OR m."Version"<>d."Version" OR m."ModelsJson" IS DISTINCT FROM
                      COALESCE(NULLIF(d."SettingsJson"->'entryModels', 'null'::jsonb), '[]'::jsonb)) THEN
                    RAISE EXCEPTION 'Entry model preservation failed; journal/plan removal was cancelled.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(name: "AccountingPreparations");
            migrationBuilder.DropTable(name: "AccountingDossiers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountingDossiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SettingsJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingDossiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingDossiers_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccountingPreparations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DraftJson = table.Column<string>(type: "jsonb", nullable: false),
                    Scope = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SourceVersion = table.Column<int>(type: "integer", nullable: false),
                    StatementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidatedBy = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ValidationFingerprint = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingPreparations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingPreparations_StatementImports_StatementId",
                        column: x => x.StatementId,
                        principalTable: "StatementImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingDossiers_ClientId",
                table: "AccountingDossiers",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPreparations_StatementId_Scope",
                table: "AccountingPreparations",
                columns: new[] { "StatementId", "Scope" },
                unique: true);
            // Journal/plan records are intentionally not recreated by a rollback.
            migrationBuilder.Sql("""
                INSERT INTO "AccountingDossiers" ("Id", "ClientId", "SettingsJson", "Version", "CreatedAt", "UpdatedAt")
                SELECT "Id", "ClientId", jsonb_build_object('entryModels', "ModelsJson"), "Version", "CreatedAt", "UpdatedAt"
                FROM "ClientEntryModelSets";
                """);
            migrationBuilder.DropTable(name: "ClientEntryModelSets");
        }
    }
}
