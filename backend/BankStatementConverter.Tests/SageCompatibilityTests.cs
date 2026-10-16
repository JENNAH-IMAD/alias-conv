using System.Text;
using System.Text.Json;
using BankStatementConverter.Infrastructure.Exports;
using Xunit;
namespace BankStatementConverter.Tests;

public sealed class SageCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sage100StartsWithDataEvenWhenAnExistingModelEnablesHeaders(bool standard)
    {
        var template = ExportFixtures.Sage(standard);
        Assert.True(template.IncludeHeader);
        var preview = await ExportFixtures.Engine.PreviewAsync(ExportFixtures.Statement, template, default);
        var generated = await ExportFixtures.Engine.GenerateAsync(ExportFixtures.Statement, template, default);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var text = Encoding.GetEncoding(1252).GetString(generated.Content);
        Assert.True(generated.DownloadAllowed);
        Assert.StartsWith("01/09/2026\t01/09/2026\tVIREMENT CLIENT\t12500,00\t", text);
        Assert.DoesNotContain("Date opération", text);
        Assert.Equal(preview.Text, text);
        var edited = await ExportFixtures.Engine.GenerateAsync(ExportFixtures.Statement, template, default, new(EditedText: text));
        Assert.Equal(2, edited.RowCount);
        Assert.Equal(generated.Content, edited.Content);
        Assert.True(template.IncludeHeader);
    }
    [Fact]
    public async Task X3AdaptsDescriptionOnlyForExportAndStandardCsvKeepsTheOriginal()
    {
        const string description = "ACHAT, CARTE;\"MAGASIN\"\nOrigine : -125,50";
        var source = ExportFixtures.Statement with
        {
            Transactions = [ExportFixtures.Statement.Transactions[0], ExportFixtures.Statement.Transactions[1] with { Description = description }]
        };
        var template = ExportFixtures.X3(false);
        var before = JsonSerializer.Serialize(source);
        var model = JsonSerializer.Serialize(template);
        var preview = await ExportFixtures.Engine.PreviewAsync(source, template, default);
        Assert.Empty(preview.Errors);
        Assert.Equal("ACHAT CARTE 'MAGASIN' Origine : -125 50", preview.Rows[2][2]);
        Assert.Contains(preview.Warnings, w => w.Code == "SAGE_DESCRIPTION_NORMALIZED" && w.TransactionSequence == 2);
        var output = await ExportFixtures.Engine.GenerateAsync(source, template, default);
        Assert.Empty(output.Errors);
        Assert.Contains(preview.Rows[2][2], Encoding.UTF8.GetString(output.Content));
        var standard = Encoding.UTF8.GetString(StandardStatementCsv.Generate(source));
        Assert.Contains("ACHAT, CARTE;\"\"MAGASIN\"\"\nOrigine : -125,50", standard);
        Assert.Equal(before, JsonSerializer.Serialize(source));
        Assert.Equal(model, JsonSerializer.Serialize(template));
    }
    [Fact]
    public async Task Sage100UsesMagnitudeAndDebitCreditCodesWithoutMutatingSignedInputOrLegacyModels()
    {
        var s = ExportFixtures.Statement;
        var source = s with
        {
            TotalDebit = null,
            TotalCredit = null,
            ClosingBalance = null,
            Transactions = [s.Transactions[0], s.Transactions[1] with { Debit = null, Amount = -125.50m }]
        };
        var preview = await ExportFixtures.Engine.PreviewAsync(source, ExportFixtures.Sage(true), default);
        Assert.Empty(preview.Errors);
        Assert.Equal("125,50", preview.Rows[1][3]);
        Assert.Equal("C", preview.Rows[0][4]);
        Assert.Equal("D", preview.Rows[1][4]);
        Assert.Equal("02/09/2026", preview.Rows[1][1]);
        Assert.Equal(-125.50m, source.Transactions[1].Amount);
        var legacy = ExportFixtures.Sage(false);
        var old = await ExportFixtures.Engine.PreviewAsync(source, legacy, default);
        Assert.Contains(old.Warnings, w => w.Code == "SAGE100_SENS_AMOUNT");
        Assert.Equal("125,50", old.Rows[1][4]);
        Assert.Equal("Amount", legacy.Fields.Single(f => f.FieldName == "Sens").SourceField);
        var conflicting = await ExportFixtures.Engine.GenerateAsync(s with
        {
            Transactions = [s.Transactions[0], s.Transactions[1] with { Amount = -125.50m }]
        }, legacy, default);
        Assert.Contains(conflicting.Errors, e => e.Code == "AMOUNT_MISMATCH");
        Assert.Empty(conflicting.Content);
    }
    [Fact]
    public async Task LegacySenseDiagnosticIdentifiesTheColumnWithoutChangingTheSavedMapping()
    {
        var template = ExportFixtures.Sage(false);
        template.Fields.Single(f => f.FieldName == "Sens").FieldName = " sens ";
        var before = JsonSerializer.Serialize(template);
        var preview = await ExportFixtures.Engine.PreviewAsync(ExportFixtures.Statement, template, default);
        var diagnostic = Assert.Single(preview.Warnings, w => w.Code == "SAGE100_SENS_AMOUNT");
        Assert.Equal(" sens ", diagnostic.FieldName);
        Assert.Equal("Amount", diagnostic.SourceField);
        Assert.Null(diagnostic.TransactionSequence);
        Assert.Contains("Direction", diagnostic.Message);
        Assert.Equal("12500,00", preview.Rows[0][4]);
        Assert.Equal(before, JsonSerializer.Serialize(template));

        var field = template.Fields.Single(f => f.FieldName == " sens ");
        field.SourceField = "Direction";
        var corrected = await ExportFixtures.Engine.PreviewAsync(ExportFixtures.Statement, template, default);
        Assert.DoesNotContain(corrected.Warnings, w => w.Code == "SAGE100_SENS_AMOUNT");
        Assert.Equal(new[] { "C", "D" }, corrected.Rows.Select(row => row[4]));
    }
    [Fact]
    public async Task SenseDiagnosticDoesNotApplyToOtherAmountsOrCsvModels()
    {
        var template = ExportFixtures.Sage(false);
        template.Type = "CUSTOMCSV";
        Assert.DoesNotContain((await ExportFixtures.Engine.PreviewAsync(ExportFixtures.Statement, template, default)).Warnings, w => w.Code == "SAGE100_SENS_AMOUNT");
        template.Type = "SAGE100";
        template.Fields.Single(f => f.FieldName == "Sens").FieldName = "Montant original";
        Assert.DoesNotContain((await ExportFixtures.Engine.PreviewAsync(ExportFixtures.Statement, template, default)).Warnings, w => w.Code == "SAGE100_SENS_AMOUNT");
    }
}
