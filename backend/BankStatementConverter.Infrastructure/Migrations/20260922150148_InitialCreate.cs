using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankStatementConverter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Banks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Code = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LegalName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ICE = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IF = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RC = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Email = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Phone = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Country = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

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
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Email = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Role = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatementTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ParserKey = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Delimiter = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DateFormat = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    NumberCulture = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SkipLines = table.Column<int>(type: "integer", nullable: false),
                    DateColumn = table.Column<int>(type: "integer", nullable: false),
                    DescriptionColumn = table.Column<int>(type: "integer", nullable: false),
                    DebitColumn = table.Column<int>(type: "integer", nullable: false),
                    CreditColumn = table.Column<int>(type: "integer", nullable: false),
                    BalanceColumn = table.Column<int>(type: "integer", nullable: true),
                    ReferenceColumn = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatementTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatementTemplates_Banks_BankId",
                        column: x => x.BankId,
                        principalTable: "Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Currency = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Journal = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AccountCode = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankAccounts_Banks_BankId",
                        column: x => x.BankId,
                        principalTable: "Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankAccounts_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateTable(
                name: "History",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankStatementTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExportTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputFileName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    InputStorageKey = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OutputStorageKey = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OutputFileName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TransactionCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_History", x => x.Id);
                    table.ForeignKey(
                        name: "FK_History_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_History_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_History_ExportTemplates_ExportTemplateId",
                        column: x => x.ExportTemplateId,
                        principalTable: "ExportTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_History_StatementTemplates_BankStatementTemplateId",
                        column: x => x.BankStatementTemplateId,
                        principalTable: "StatementTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_History_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_BankId_AccountNumber",
                table: "BankAccounts",
                columns: new[] { "BankId", "AccountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_ClientId",
                table: "BankAccounts",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Banks_Code",
                table: "Banks",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_ICE",
                table: "Clients",
                column: "ICE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExportTemplateField_ExportTemplateId_Position",
                table: "ExportTemplateField",
                columns: new[] { "ExportTemplateId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_History_BankAccountId",
                table: "History",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_History_BankStatementTemplateId",
                table: "History",
                column: "BankStatementTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_History_ClientId",
                table: "History",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_History_ExportTemplateId",
                table: "History",
                column: "ExportTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_History_UserId_CreatedAt",
                table: "History",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StatementTemplates_BankId",
                table: "StatementTemplates",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExportTemplateField");

            migrationBuilder.DropTable(
                name: "History");

            migrationBuilder.DropTable(
                name: "BankAccounts");

            migrationBuilder.DropTable(
                name: "ExportTemplates");

            migrationBuilder.DropTable(
                name: "StatementTemplates");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "Banks");
        }
    }
}
