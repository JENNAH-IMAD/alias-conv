using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankStatementConverter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientExportsAndWorkspaceAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BankAccountId",
                table: "StatementImports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "StatementImports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportedByName",
                table: "StatementImports",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedByName",
                table: "StatementImports",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeneratedByName",
                table: "ExportJobs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StandardStorageKey",
                table: "ExportJobs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                table: "ExportJobs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkspaceUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Email = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Role = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PermissionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SessionVersion = table.Column<int>(type: "integer", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSessionVersion = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkspaceSessions_WorkspaceUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "WorkspaceUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StatementImports_BankAccountId",
                table: "StatementImports",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_StatementImports_ClientId_CreatedAt",
                table: "StatementImports",
                columns: new[] { "ClientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceSessions_ExpiresAt",
                table: "WorkspaceSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceSessions_UserId",
                table: "WorkspaceSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceUsers_Email",
                table: "WorkspaceUsers",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StatementImports_BankAccounts_BankAccountId",
                table: "StatementImports",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StatementImports_Clients_ClientId",
                table: "StatementImports",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StatementImports_BankAccounts_BankAccountId",
                table: "StatementImports");

            migrationBuilder.DropForeignKey(
                name: "FK_StatementImports_Clients_ClientId",
                table: "StatementImports");

            migrationBuilder.DropTable(
                name: "WorkspaceSessions");

            migrationBuilder.DropTable(
                name: "WorkspaceUsers");

            migrationBuilder.DropIndex(
                name: "IX_StatementImports_BankAccountId",
                table: "StatementImports");

            migrationBuilder.DropIndex(
                name: "IX_StatementImports_ClientId_CreatedAt",
                table: "StatementImports");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "StatementImports");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "StatementImports");

            migrationBuilder.DropColumn(
                name: "ImportedByName",
                table: "StatementImports");

            migrationBuilder.DropColumn(
                name: "ModifiedByName",
                table: "StatementImports");

            migrationBuilder.DropColumn(
                name: "GeneratedByName",
                table: "ExportJobs");

            migrationBuilder.DropColumn(
                name: "StandardStorageKey",
                table: "ExportJobs");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "ExportJobs");
        }
    }
}
