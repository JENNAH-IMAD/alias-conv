using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BankStatementConverter.Infrastructure.Exports;

// Semantic names are independent of the bank and of the selected output format.
public static class StatementFieldNames
{
    private static string Fold(string text) => Regex.Replace(new string(text.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
        .Select(char.ToUpperInvariant).ToArray()), @"[^A-Z0-9]+", " ").Trim();
    private static readonly Dictionary<string, string[]> Names = new()
    {
        // Booking/registration dates remain operation dates by default. A bank-specific
        // column mapping can override this; they are not synonyms of a value date.
        ["Date"] = ["DATE", "DATE OPERATION", "DATE OPERATIONS", "DATE D OPERATION", "DATE DE L OPERATION", "DATE DES OPERATIONS", "DATE D EXECUTION", "DATE EXECUTION", "DATE COMPTABLE", "DATE COMPTABILISATION", "DATE DE COMPTABILISATION", "DATE ECRITURE", "DATE D ECRITURE", "DATE TRANSACTION", "DATE DE TRANSACTION", "DATE MOUVT", "DATE MOUVEMENT", "DATE DU MOUVEMENT", "DATE DE SAISIE", "DATE SAISIE", "DATE D ENREGISTREMENT", "DATE ENREGISTREMENT", "DATE OP", "DATE OPE", "DATE D OPER", "DATE OPER", "DATE D OP", "DT OP", "DT OPE", "DT OPERATION", "BOOKING DATE", "POSTING DATE", "TRANSACTION DATE", "OPERATION DATE"],
        ["ValueDate"] = ["VALEUR", "DATE VALEUR", "DATE DE VALEUR", "DATE VAL", "DT VAL", "DT VALEUR", "DATE D EFFET", "DATE EFFET", "VALUE DATE", "VALUE DT"],
        ["Description"] = ["LIBELLE", "LIBELLES", "LIBELLE OPERATION", "LIBELLE DE L OPERATION", "LIBELLE DES OPERATIONS", "DESIGNATION", "DESCRIPTION", "NATURE", "NATURE OPERATION", "NATURE DE L OPERATION", "NATURE DES OPERATIONS", "OPERATION", "OPERATIONS", "TRANSACTION", "DETAIL", "DETAILS", "DETAIL OPERATION", "DETAIL DE L OPERATION", "DETAILS OPERATION", "DETAILS DE L OPERATION", "TRANSACTION DETAILS", "TRANSACTION DESCRIPTION", "STATEMENT NARRATIVE", "NARRATIVE", "MOTIF", "OBJET", "INTITULE", "LIBELLE REFERENCE", "LIBELLE REF", "REFERENCE LIBELLE", "REF LIBELLE"],
        ["Debit"] = ["DEBIT", "DEBITS", "MONTANT DEBIT", "MONTANT DEBITE", "MOUVEMENT DEBIT", "MOUVEMENTS DEBIT", "SENS DEBITEUR", "DEBITEUR", "DR", "RETRAIT", "RETRAITS", "SORTIE", "SORTIES", "MONTANT SORTANT", "PAIEMENT", "PAIEMENTS", "WITHDRAWAL", "WITHDRAWALS", "DEBIT AMOUNT", "MONEY OUT"],
        ["Credit"] = ["CREDIT", "CREDITS", "MONTANT CREDIT", "MONTANT CREDITE", "MOUVEMENT CREDIT", "MOUVEMENTS CREDIT", "SENS CREDITEUR", "CREDITEUR", "CR", "VERSEMENT", "VERSEMENTS", "ENTREE", "ENTREES", "MONTANT ENTRANT", "ENCAISSEMENT", "ENCAISSEMENTS", "DEPOSIT", "DEPOSITS", "CREDIT AMOUNT", "MONEY IN"],
        ["Reference"] = ["REFERENCE", "REF", "N REFERENCE", "NO REFERENCE", "NUMERO DE REFERENCE", "REFERENCE OPERATION", "REFERENCE DE L OPERATION", "REF OPERATION", "TRANSACTION REFERENCE"]
    };
    public static string? Column(string text)
    {
        var label = Fold(Regex.Replace(text, @"\\text\s*\{([^}]*)\}", "$1"));
        if (label is "DATES OPERATION" or "DATES OPER")
            return "Date";
        if (label is "DATES VALEUR" or "DATES DATE VALEUR")
            return "ValueDate";
        if (label == "OPERATION REFERENCE")
            return "Description";
        // Currency decorations do not change the meaning of a column.
        label = Regex.Replace(label, @"\s+(?:EN\s+)?(MAD|DH|DHS|EUR|USD|GBP|CHF|DIRHAMS|DIRHAM|EUROS)$", "");
        return Names.FirstOrDefault(pair => pair.Value.Contains(label)).Key;
    }
    public static bool IsBalance(string text) => Regex.IsMatch(Fold(text), @"\b(SOLDE|BALANCE)\b");
    // Available funds may include an overdraft facility or pending operations.
    // Keep this line out of opening/closing selection, including positional fallbacks.
    public static bool IsAvailableBalance(string text) => IsBalance(text)
        && Regex.IsMatch(Fold(text), @"\b(DISPONIBLE|AVAILABLE)\b");
    public static string? Balance(string text)
    {
        var label = Fold(text);
        if (!IsBalance(text) || IsAvailableBalance(text) || Regex.IsMatch(label, @"\bREPORT(?:E)? (?:DE )?(PAGE|INTERMEDIAIRE)\b"))
            return null;
        var opening = Regex.IsMatch(label, @"\b(INITIAL|INITIALS|ANCIEN|PRECEDENT|ANTERIEUR|REPORTE|DEPART|OUVERTURE|DEBUT|OPENING|PREVIOUS|BEGINNING|BROUGHT FORWARD)\b");
        var closing = Regex.IsMatch(label, @"\b(FINAL|FIN|CLOTURE|NOUVEAU|CLOSING|ENDING|CARRIED FORWARD)\b");
        return opening == closing ? null : opening ? "OpeningBalance" : "ClosingBalance";
    }
}
