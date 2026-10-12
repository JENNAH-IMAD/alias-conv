using BankStatementConverter.Domain;

namespace BankStatementConverter.Infrastructure;

/// <summary>Catalogue initial, extensible par la gestion des banques. Codes internes, pas des codes RIB.</summary>
public static class MoroccanBankCatalog
{
    // Périmètre choisi par l'utilisateur ; les logos sont gérés manuellement.
    public static IReadOnlyList<(string Name, string Code)> Entries { get; } = new[]
    {
        ("Attijariwafa Bank", "AWB"),
        ("Al Barid Bank", "ABB"), ("Banque Populaire", "BP"),
        ("Bank of Africa / BMCE", "BMCE"), ("BMCI", "BMCI"),
        ("Crédit Agricole du Maroc", "CA"), ("CFG Bank", "CFG"),
        ("CIH Bank", "CIH"), ("Crédit du Maroc", "CDM"), ("Saham Bank", "SB")
    };

    public static IReadOnlyList<Bank> Missing(IEnumerable<Bank> existing)
    {
        var banks = existing.ToArray();
        return Entries.Where(entry => !banks.Any(bank =>
            bank.Code.Equals(entry.Code, StringComparison.OrdinalIgnoreCase) ||
            IdentityCodes(bank.Name).Contains(entry.Code)))
            .Select(entry => new Bank { Name = entry.Name, Code = entry.Code,
                Description = "Établissement bancaire marocain." })
            .ToArray();
    }

    public static string[] IdentityCodes(string name)
    {
        var normalized = Normalize(name);
        return Entries.Where(e => normalized.Contains(Normalize(e.Code switch
            {
                "CA" => "Crédit Agricole", "AWB" => "Attijariwafa", "BMCE" => "Bank of Africa","BP"=> "Banque Populaire", "ABB" => "Al Barid Bank", "CIH" => "CIH Bank", "CDM" => "Crédit du Maroc",
                "SB" => "Saham Bank", "CFG" => "CFG", _ => e.Name
            }), StringComparison.Ordinal) || System.Text.RegularExpressions.Regex.IsMatch(normalized, @"\b" + e.Code + @"\b"))
            .Select(e => e.Code).ToArray();
    }
    private static string Normalize(string value) => new string(value.Normalize(System.Text.NormalizationForm.FormD)
        .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray()).ToUpperInvariant();
}
