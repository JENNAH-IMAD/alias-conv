using BankStatementConverter.Application;
using BankStatementConverter.Domain;

namespace BankStatementConverter.Infrastructure;

public static class HistoryActivityQuery
{
    private static readonly HashSet<string> Actions = new(StringComparer.Ordinal)
    {
        "IMPORTED", "MODIFIED", "FILED", "VALIDATED", "EXPORTED", "DELETED",
        "DOWNLOAD_COMPLETED", "SOURCE_SAVED", "MODEL_APPLIED"
    };

    // Shared by the activity list and all-pages deletion so their scopes stay identical.
    public static IQueryable<StatementActivity> Apply(IQueryable<StatementActivity> query,
        string? search, string? action, DateOnly? from, DateOnly? to)
    {
        Validation.Require(search == null || search.Length <= 150, "Recherche : 150 caractères maximum.");
        Validation.Require(string.IsNullOrEmpty(action) || Actions.Contains(action), "Type d’action invalide.");
        Validation.Require(!from.HasValue || !to.HasValue || from <= to, "La date de début doit précéder la date de fin.");
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.FileName.ToLower().Contains(term) || x.BankName.ToLower().Contains(term)
                || x.ClientName.ToLower().Contains(term) || x.AccountName.ToLower().Contains(term) || x.ActorName.ToLower().Contains(term));
        }
        if (!string.IsNullOrEmpty(action))
            query = query.Where(x => x.Action == action);
        if (from.HasValue)
        {
            var start = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(x => x.CreatedAt >= start);
        }
        if (to.HasValue)
        {
            var end = to.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(x => x.CreatedAt <= end);
        }
        return query;
    }
}
