using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankStatementConverter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEntryS100OutputColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep each user's file settings and existing column visibility while replacing NET.
            migrationBuilder.Sql("""
                UPDATE "EntryOutputTemplates"
                SET "Name" = 'Écriture comptable S100',
                    "Description" = 'N° compte, Intitulé, Libellé, Banque, Date, Débit HT et Crédit HT après calcul de la TVA.',
                    "OutputJson" = jsonb_set("OutputJson", '{columns}', (
                        SELECT jsonb_agg(column_json ORDER BY position)
                        FROM (
                            SELECT CASE c.value->>'field'
                                WHEN 'DATE' THEN jsonb_set(c.value, '{label}', '"Date"'::jsonb)
                                ELSE c.value
                            END AS column_json,
                            CASE c.value->>'field'
                                WHEN 'ACCOUNT' THEN 1 WHEN 'TITLE' THEN 2 WHEN 'DESCRIPTION' THEN 3
                                WHEN 'BANK' THEN 4 WHEN 'DATE' THEN 5 ELSE 99 END AS position
                            FROM jsonb_array_elements(COALESCE("OutputJson"->'columns', '[]'::jsonb)) AS c(value)
                            WHERE c.value->>'field' IN ('ACCOUNT','TITLE','DESCRIPTION','BANK','DATE')
                            UNION ALL
                            SELECT jsonb_build_object('field','DATE','label','Date','display',true,'export',true), 5
                            WHERE NOT EXISTS (SELECT 1 FROM jsonb_array_elements(COALESCE("OutputJson"->'columns', '[]'::jsonb)) x WHERE x->>'field'='DATE')
                            UNION ALL
                            SELECT jsonb_build_object('field','DEBIT_HT','label','Débit HT','display',true,'export',true), 6
                            UNION ALL
                            SELECT jsonb_build_object('field','CREDIT_HT','label','Crédit HT','display',true,'export',true), 7
                        ) columns
                    ), true)
                WHERE "Id" = '9d2e4a78-fb36-4820-9c04-87b370743915';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "EntryOutputTemplates"
                SET "Name" = 'Écritures comptables — 5 colonnes',
                    "Description" = 'N° compte, Intitulé, Libellé, Banque et Montant HT calculé après TVA.',
                    "OutputJson" = jsonb_set("OutputJson", '{columns}', '[
                      {"field":"ACCOUNT","label":"N° compte","display":true,"export":true},
                      {"field":"TITLE","label":"Intitulé","display":true,"export":true},
                      {"field":"DESCRIPTION","label":"Libellé","display":true,"export":true},
                      {"field":"BANK","label":"Banque","display":true,"export":true},
                      {"field":"NET","label":"Montant HT","display":true,"export":true}
                    ]'::jsonb, true)
                WHERE "Id" = '9d2e4a78-fb36-4820-9c04-87b370743915';
                """);
        }
    }
}
