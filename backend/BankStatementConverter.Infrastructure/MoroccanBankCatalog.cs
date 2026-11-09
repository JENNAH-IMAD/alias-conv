using BankStatementConverter.Domain;

namespace BankStatementConverter.Infrastructure;

/// <summary>Identités bancaires et catalogue proposé des groupes marocains. Codes internes, pas des codes RIB.</summary>
public static class MoroccanBankCatalog
{
    // Banques conventionnelles et participatives adhérentes à la SGFG.
    // Source : https://www.sgfg.ma/fr/garantie-des-depots/liste-des-banques-membres
    public static IReadOnlyList<(string Name, string Code)> Entries
    {
        get;
    } = new[]
    {
        ("Attijariwafa Bank", "AWB"),
        ("Al Barid Bank", "ABB"), ("Banque Populaire", "BP"),
        ("Bank of Africa / BMCE", "BMCE"), ("BMCI", "BMCI"),
        ("Crédit Agricole du Maroc", "CA"), ("CFG Bank", "CFG"),
        ("CIH Bank", "CIH"), ("Crédit du Maroc", "CDM"), ("Saham Bank", "SB"),
        ("Arab Bank Maroc", "ARAB"), ("CaixaBank", "CAIXA"),
        ("CDG Capital", "CDG"), ("Citibank Maghreb", "CITI"),
        ("Banco Sabadell", "SABADELL"), ("Union Marocaine des Banques", "UMB"),
        ("Bank Assafa", "ASSAFA"), ("Al Akhdar Bank", "AKHDAR"),
        ("Bank Al Karam", "ALKARAM"), ("Bank Al Yousr", "ALYOUSR"),
        ("Umnia Bank", "UMNIA")
    };

    private static readonly IReadOnlyDictionary<string, string[]> Aliases = new Dictionary<string, string[]>
    {
        ["AWB"] = ["Attijariwafa"],
        ["ABB"] = ["Al Barid Bank"],
        ["BP"] = ["Banque Populaire", "Banque Centrale Populaire", "Crédit Populaire du Maroc", "BCP"],
        ["BMCE"] = ["Bank of Africa", "BMCE", "BMCE Bank", "BMCE Bank of Africa", "Banque Marocaine du Commerce Extérieur"],
        ["BMCI"] = ["BMCI", "BMCI Maroc", "Banque Marocaine pour le Commerce et l’Industrie"],
        ["CA"] = ["Crédit Agricole", "CAM"],
        ["CFG"] = ["CFG Bank"],
        ["CIH"] = ["CIH", "Crédit Immobilier et Hôtelier"],
        ["CDM"] = ["Crédit du Maroc"],
        ["SB"] = ["Saham Bank", "SAHAM", "Société Générale", "Société Générale Maroc", "SGMB", "SGMA"],
        ["ARAB"] = ["Arab Bank"],
        ["CAIXA"] = ["CaixaBank", "Caixa Bank"],
        ["CDG"] = ["CDG Capital"],
        ["CITI"] = ["Citibank", "Citi Bank"],
        ["SABADELL"] = ["Banco Sabadell", "Sabadell", "Sabadel"],
        ["UMB"] = ["Union Marocaine des Banques", "Union Marocaine de Banques"],
        ["ASSAFA"] = ["Bank Assafa", "Banque Assafa"],
        ["AKHDAR"] = ["Al Akhdar"],
        ["ALKARAM"] = ["Bank Al Karam"],
        ["ALYOUSR"] = ["Bank Al Yousr", "Al Yousr"],
        ["UMNIA"] = ["Umnia Bank"]
    };

    // User-selected scope: conventional Moroccan banks, including BMCI explicitly.
    // Keep the complete identity catalog above for historical PDF recognition.
    // Selection policy: docs/banques-marocaines.md (2026-10-22).
    private static readonly HashSet<string> SelectableCodes = new(StringComparer.Ordinal)
    {
        "AWB", "ABB", "BP", "BMCE", "BMCI", "CA", "CFG", "CIH", "CDM", "SB", "CDG"
    };
    public static IReadOnlyList<(string Name, string Code)> SelectableEntries
    {
        get;
    } = Entries
        .Where(entry => SelectableCodes.Contains(entry.Code)).ToArray();

    public static string? CatalogCode(string value)
    {
        var normalized = Normalize(value);
        // Legacy application codes, kept out of broad PDF header matching.
        if (normalized is "SG" or "ATT" or "BOA")
            return normalized switch
            {
                "SG" => "SB",
                "ATT" => "AWB",
                _ => "BMCE"
            };
        return Entries.Where(entry => new[] { entry.Name, entry.Code }.Concat(Aliases[entry.Code])
            .Any(alias => Normalize(alias) == normalized)).Select(entry => entry.Code).SingleOrDefault();
    }

    public static bool IsSelectable(string name, string code)
    {
        var byCode = CatalogCode(code);
        var byName = CatalogCode(name);
        if (byCode != null && !SelectableCodes.Contains(byCode) || byName != null && !SelectableCodes.Contains(byName))
            return false;
        return (byCode != null || byName != null) && (byCode == null || byName == null || byCode == byName);
    }

    public static IReadOnlyList<Bank> Missing(IEnumerable<Bank> existing)
    {
        var banks = existing.ToArray();
        return SelectableEntries.Where(entry => !banks.Any(bank =>
            bank.Code.Equals(entry.Code, StringComparison.OrdinalIgnoreCase) ||
            IdentityCodes(bank.Code).Contains(entry.Code) ||
            IdentityCodes(bank.Name).Contains(entry.Code)))
            .Select(entry => new Bank
            {
                Name = entry.Name,
                Code = entry.Code,
                Description = "Banque conventionnelle au Maroc."
            })
            .ToArray();
    }

    public static string[] IdentityCodes(string name)
    {
        var normalized = " " + Normalize(name) + " ";
        return Entries.Where(e => new[] { e.Code, e.Name }.Concat(Aliases[e.Code])
            .Any(alias => normalized.Contains(" " + Normalize(alias) + " ", StringComparison.Ordinal)))
            .Select(e => e.Code).ToArray();
    }

    /// <summary>Complète uniquement les logos absents ; les images personnalisées restent prioritaires.</summary>
    public static int ApplyMissingLogos(IEnumerable<Bank> banks)
    {
        var count = 0;
        foreach (var bank in banks.Where(bank => bank.Logo is null || bank.Logo.Length == 0))
        {
            var codes = IdentityCodes(bank.Code + " " + bank.Name);
            if (codes.Length != 1)
                continue;
            var file = codes[0] switch
            {
                "SB" => "SAHAM.jpg",
                "ALKARAM" => "ALKARAM.jpg",
                _ => codes[0] + ".png"
            };
            using var stream = typeof(MoroccanBankCatalog).Assembly.GetManifestResourceStream("Alias.BankLogos." + file)
                ?? throw new InvalidOperationException("Logo bancaire embarqué introuvable : " + file);
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            bank.Logo = buffer.ToArray();
            bank.LogoContentType = file.EndsWith(".jpg", StringComparison.Ordinal) ? "image/jpeg" : "image/png";
            bank.LogoVersion = Guid.NewGuid();
            count++;
        }
        return count;
    }

    private static string Normalize(string value)
    {
        var plain = new string(value.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray()).ToUpperInvariant();
        return System.Text.RegularExpressions.Regex.Replace(plain, "[^A-Z0-9]+", " ").Trim();
    }
}
