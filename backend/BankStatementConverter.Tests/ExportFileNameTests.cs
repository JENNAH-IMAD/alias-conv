using BankStatementConverter.Infrastructure;
using Xunit;

namespace BankStatementConverter.Tests;
public class ExportFileNameTests
{
    [Fact]
    public void ExportNameIncludesBankAccountDateAndIdentityWithoutPaths()
    {
        var id = Guid.Parse("12345678-1234-1234-1234-123456789012");
        var name = ExportFileName.Create("Crédit / Banque", "Compte : principal", new DateTime(2026, 9, 29), id, "txt");
        Assert.Equal("Crédit_Banque_Compte_principal_2026-09-29_12345678.txt", name);
        Assert.DoesNotContain('/', name);
        Assert.DoesNotContain(':', name);
        Assert.Throws<ArgumentException>(() => ExportFileName.Create("Bank", "Account", DateTime.UtcNow, id, "../exe"));
    }
}
