using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankStatementConverter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RetainExportModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExportTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Type = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FileExtension = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Delimiter = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Encoding = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    NumberCulture = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IncludeHeader = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExportTemplateField",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExportTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SourceField = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Format = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportTemplateField", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportTemplateField_ExportTemplates_ExportTemplateId",
                        column: x => x.ExportTemplateId,
                        principalTable: "ExportTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExportTemplateField_ExportTemplateId_Position",
                table: "ExportTemplateField",
                columns: new[] { "ExportTemplateId", "Position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExportTemplateField");

            migrationBuilder.DropTable(
                name: "ExportTemplates");
        }
    }
}
