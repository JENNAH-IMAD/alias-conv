using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure.Accounting;
using System.Text.Json;

internal static class EntryModelChecks
{
    private static void Check(bool condition, string name) { if (!condition) throw new Exception("EntryModelChecks: " + name); Console.WriteLine("PASS " + name); }
    private static void Reject(Action action, string name) { try { action(); } catch (AppException) { Console.WriteLine("PASS " + name); return; } throw new Exception("EntryModelChecks: " + name); }
    public static void Run()
    {
        var bank = new BankAccount { AccountCode = "514100", Journal = "BQ1", Currency = "MAD" };
        var movement = new StatementTransaction { Sequence = 1, Date = new(2026, 11, 1), Description = "Achat café / fournisseur", Debit = 120, Reference = "R1" };
        var source = new BankStatement { Currency = "MAD", BalanceConvention = "CREDIT_INCREASES", Transactions = [movement] };
        var model = new AccountingEntryModel(Guid.NewGuid(), "Achats", "ACH", ["ACHAT", "CAFE"], bank.Id, "514100", Guid.Empty, 20);
        var original = JsonSerializer.Serialize(source);
        Check(EntryModelEngine.Matches("Règlement CAFÉ", "reglement cafe") && !EntryModelEngine.Matches("MACHINE", "ACH"), "Matching_AccentsAndWordBoundaries");
        var twenty = EntryModelEngine.Evaluate(source, bank, [model]).Single();
        Check(twenty.Net == 100 && twenty.Tax == 20 && twenty.Total == 120, "TTC_20_Percent");
        var ten = EntryModelEngine.Evaluate(source with { Transactions = [movement with { Debit = 110 }] }, bank, [model with { TaxRate = 10 }]).Single();
        Check(ten.Net == 100 && ten.Tax == 10, "TTC_10_Percent");
        var zero = EntryModelEngine.Evaluate(source, bank, [model with { TaxRate = 0 }]).Single();
        Check(zero.Net == 120 && zero.Tax == 0, "TTC_Zero_Percent");
        var rounding = EntryModelEngine.Evaluate(source with { Transactions = [movement with { Debit = .01m }] }, bank, [model]).Single();
        Check(rounding.Net == .01m && rounding.Tax == 0, "TTC_CentRounding");
        var credit = EntryModelEngine.Evaluate(source with { Transactions = [movement with { Debit = null, Credit = 120 }] }, bank, [model]).Single();
        Check(credit.Net == 100 && credit.Tax == 20, "Incoming_CreditTaxCalculation");
        Check(AccountingEngine.BankAmounts(source, movement) == (0m, 120m)
            && AccountingEngine.BankAmounts(source with { BalanceConvention = "DEBIT_INCREASES" }, movement) == (120m, 0m), "Explicit_BankConvention");
        var other = model with { Id = Guid.NewGuid(), Name = "Autre achat", TaxRate = 10 };
        var ambiguity = EntryModelEngine.Evaluate(source, bank, [model, other]).Single();
        Check(ambiguity.Status == "AMBIGUOUS" && ambiguity.Candidates.Count == 2 && ambiguity.Net == null, "Ambiguity_NotAutomaticallyAssigned");
        var selected = EntryModelEngine.Evaluate(source, bank, [model, other], new Dictionary<Guid, Guid?> { [movement.Id] = model.Id }).Single();
        Check(selected.Status == "MATCHED" && selected.ModelId == model.Id && selected.Tax == 20, "Ambiguity_ExplicitSelection");
        var excluded = EntryModelEngine.Evaluate(source, bank, [model, other], new Dictionary<Guid, Guid?> { [movement.Id] = null }).Single();
        Check(excluded.Status == "EXCLUDED" && excluded.Net == null, "Ambiguity_ExplicitExclusion");
        Check(EntryModelEngine.Evaluate(source, bank, [model], new Dictionary<Guid, Guid?> { [movement.Id] = null }).Single().Status == "EXCLUDED", "Exclusion_OneCandidate");
        Reject(() => EntryModelEngine.Evaluate(source, bank, [model], new Dictionary<Guid, Guid?> { [movement.Id] = Guid.NewGuid() }), "Decision_MustBeCandidate");
        Reject(() => EntryModelEngine.Evaluate(source, bank, [model], new Dictionary<Guid, Guid?> { [Guid.NewGuid()] = model.Id }), "Decision_MustBelongToSource");
        Check(EntryModelEngine.Evaluate(source, bank, [model with { Active = false }]).Single().Status == "UNMATCHED"
            && EntryModelEngine.Evaluate(source, bank, [model with { BankAccountId = Guid.NewGuid() }]).Single().Status == "UNMATCHED", "InactiveAndForeignBank_NotMatched");
        var missingConvention = EntryModelEngine.Evaluate(source with { BalanceConvention = null }, bank, [model]).Single();
        Check(missingConvention.Status == "MATCHED" && missingConvention.Total == 120 && missingConvention.Net == 100 && missingConvention.Tax == 20, "MissingConvention_PreviewCalculatesTax");
        var noMatch = EntryModelEngine.Evaluate(source, bank, [model with { Keywords = ["CHEQUE"] }]).Single();
        Check(noMatch.Status == "UNMATCHED" && noMatch.Total == 120 && noMatch.Tax == null, "NoKeywordMatch_KeepsRawAmountVisible");
        var badAmount = EntryModelEngine.Evaluate(source with { Transactions = [movement with { Debit = 120, Credit = 30 }] }, bank, [model]).Single();
        Check(badAmount.Status == "INVALID" && badAmount.Total == null, "ConflictingAmounts_DoNotInventTotal");
        Check(EntryModelEngine.Evaluate(source with { Transactions = [movement with { Date = null }] }, bank, [model]).Single().Status == "INVALID", "MissingDate_BlocksResult");
        var ordered = EntryModelEngine.Evaluate(source with { Transactions = [movement with { Id = Guid.NewGuid(), Sequence = 2 }, movement] }, bank, [model]);
        Check(ordered.Select(row => row.Sequence).SequenceEqual([1, 2]), "Results_FollowSourceOrder");
        Check(JsonSerializer.Serialize(source) == original && source.Transactions.Single().Description == movement.Description, "Source_RemainsUnchanged");
    }
}
