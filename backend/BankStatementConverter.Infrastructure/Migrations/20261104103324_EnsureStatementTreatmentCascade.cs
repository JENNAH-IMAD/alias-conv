using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankStatementConverter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnsureStatementTreatmentCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_StatementTreatments_StatementImports_StatementId", table: "StatementTreatments");
            migrationBuilder.AddForeignKey(name: "FK_StatementTreatments_StatementImports_StatementId", table: "StatementTreatments",
                column: "StatementId", principalTable: "StatementImports", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_StatementTreatments_StatementImports_StatementId", table: "StatementTreatments");
            migrationBuilder.AddForeignKey(name: "FK_StatementTreatments_StatementImports_StatementId", table: "StatementTreatments",
                column: "StatementId", principalTable: "StatementImports", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }
    }
}
