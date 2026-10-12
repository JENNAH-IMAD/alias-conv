using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankStatementConverter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReusableExtractionProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExtractionProfileActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Action = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ActorName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractionProfileActivities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExtractionProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    BankCode = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Currency = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    MappingJson = table.Column<string>(type: "jsonb", nullable: false),
                    SampleJson = table.Column<string>(type: "jsonb", nullable: false),
                    AliasesJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ModifiedByName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractionProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractionProfileActivities_ProfileId_CreatedAt",
                table: "ExtractionProfileActivities",
                columns: new[] { "ProfileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractionProfiles_BankCode_Currency",
                table: "ExtractionProfiles",
                columns: new[] { "BankCode", "Currency" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExtractionProfileActivities");

            migrationBuilder.DropTable(
                name: "ExtractionProfiles");
        }
    }
}
