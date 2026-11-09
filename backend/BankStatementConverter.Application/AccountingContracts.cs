namespace BankStatementConverter.Application;

public sealed record AccountingIssue(string Code, string Message, Guid? TransactionId = null);
