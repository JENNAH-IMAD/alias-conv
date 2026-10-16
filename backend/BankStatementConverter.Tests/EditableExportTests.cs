using System.Text;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using Xunit;
namespace BankStatementConverter.Tests;

public class EditableExportTests
{
    [Fact]
    public async Task PreviewTextContainsEveryRecordAndExactlyMatchesTheGeneratedFile()
    {
        var statement = ExportFixtures.Statement with
        {
            Transactions = Enumerable.Range(1, 75).Select(i => ExportFixtures.Statement.Transactions[0] with { Id = Guid.NewGuid(), Sequence = i, Description = $"LIGNE {i};\nSUITE" }).ToList(),
            TotalDebit = null,
            TotalCredit = null,
            OpeningBalance = null,
            ClosingBalance = null
        };
        var preview = await ExportFixtures.Engine.PreviewAsync(statement, ExportFixtures.Csv(), default);
        var file = await ExportFixtures.Engine.GenerateAsync(statement, ExportFixtures.Csv(), default);
        Assert.True(file.DownloadAllowed);
        Assert.Equal(50, preview.Rows.Count);
        Assert.Equal(75, preview.TotalRows);
        Assert.Equal(preview.Text, Encoding.UTF8.GetString(file.Content));
        Assert.Contains("LIGNE 75", preview.Text);
        Assert.False(preview.TextTruncated);
    }
    [Fact]
    public async Task ExplicitForcedDownloadPreservesErrorsAndCharactersWithoutValidatingTheStatement()
    {
        var statement = ExportFixtures.Statement with
        {
            IsValidated = false,
            Transactions = [ExportFixtures.Statement.Transactions[0] with { Date = null, Description = "عملية تجريبية" }, ExportFixtures.Statement.Transactions[1]]
        };
        var model = ExportFixtures.X3(true);
        var preview = await ExportFixtures.Engine.PreviewAsync(statement, model, default);
        var strict = await ExportFixtures.Engine.GenerateAsync(statement, model, default);
        Assert.False(strict.DownloadAllowed);
        Assert.Empty(strict.Content);
        var forced = await ExportFixtures.Engine.GenerateAsync(statement, model, default, new(AllowErrors: true));
        Assert.True(forced.DownloadAllowed);
        Assert.Equal("utf-8", forced.Encoding);
        Assert.EndsWith("_a_verifier.txt", forced.FileName);
        Assert.Contains(forced.Errors, e => e.Code == "ENCODING_ERROR");
        Assert.Contains(forced.Errors, e => e.Code == "STATEMENT_NOT_VALIDATED");
        Assert.Contains(forced.Warnings, e => e.Code == "FORCED_UTF8_ENCODING");
        Assert.Equal(preview.Text, Encoding.UTF8.GetString(forced.Content));
        Assert.False(statement.IsValidated);
        Assert.Null(statement.Transactions[0].Date);
        model.IsActive = false;
        Assert.False((await ExportFixtures.Engine.GenerateAsync(statement, model, default, new(true))).DownloadAllowed);
    }
    [Fact]
    public async Task ManualTextIsExportedExactlyAndCountsQuotedMultilineRecords()
    {
        const string text = "Date;Libellé;Débit;Crédit;Solde\r\n01/09/2026;\"MODIFIÉ;\nSUITE\";;12345678901234567890.12345678;\r\n";
        var file = await ExportFixtures.Engine.GenerateAsync(ExportFixtures.Statement, ExportFixtures.Csv(), default, new(EditedText: text));
        Assert.True(file.DownloadAllowed);
        Assert.True(file.TextEdited);
        Assert.Equal(1, file.RowCount);
        Assert.Equal(text, Encoding.UTF8.GetString(file.Content));
        Assert.EndsWith("_modifie.csv", file.FileName);
        Assert.Equal("VIREMENT CLIENT", ExportFixtures.Statement.Transactions[0].Description);
        await Assert.ThrowsAsync<AppException>(() => ExportFixtures.Engine.GenerateAsync(ExportFixtures.Statement, ExportFixtures.Csv(), default, new(EditedText: new string('x', 2000001))));
    }
    [Fact]
    public async Task ManualTextEncodingErrorsRequireTheExplicitOverride()
    {
        const string text = "20260901;عملية تجريبية;42.10\n";
        var strict = await ExportFixtures.Engine.GenerateAsync(ExportFixtures.Statement, ExportFixtures.X3(true), default, new(EditedText: text));
        Assert.False(strict.DownloadAllowed);
        Assert.Contains(strict.Errors, e => e.Code == "ENCODING_ERROR");
        var forced = await ExportFixtures.Engine.GenerateAsync(ExportFixtures.Statement, ExportFixtures.X3(true), default, new(true, text));
        Assert.True(forced.DownloadAllowed);
        Assert.Equal(text, Encoding.UTF8.GetString(forced.Content));
        Assert.Equal(1, forced.RowCount);
    }
}
