using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankStatementConverter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStatementActivityAndRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StatementImports_BankAccountId",
                table: "StatementImports");

            migrationBuilder.AddColumn<DateTime>(
                name: "FiledAt",
                table: "StatementImports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StatementActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StatementId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    BankName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FileName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Action = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ActorName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatementActivities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StatementImports_BankAccountId_FiledAt",
                table: "StatementImports",
                columns: new[] { "BankAccountId", "FiledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StatementActivities_CreatedAt_Id",
                table: "StatementActivities",
                columns: new[] { "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StatementActivities_StatementId_CreatedAt",
                table: "StatementActivities",
                columns: new[] { "StatementId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StatementActivities");

            migrationBuilder.DropIndex(
                name: "IX_StatementImports_BankAccountId_FiledAt",
                table: "StatementImports");

            migrationBuilder.DropColumn(
                name: "FiledAt",
                table: "StatementImports");

            migrationBuilder.CreateIndex(
                name: "IX_StatementImports_BankAccountId",
                table: "StatementImports",
                column: "BankAccountId");
        }
    }
}
