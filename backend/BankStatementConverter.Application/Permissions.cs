namespace BankStatementConverter.Application;

public record PermissionDefinition(string Key, string Group, string Label, string[] Requires);
public static class Permissions
{
    public static readonly PermissionDefinition[] Catalog = [
        new("dashboard.read", "Tableau de bord", "Consulter les indicateurs", []),
        new("clients.read", "Clients", "Consulter", []),
        new("clients.write", "Clients", "Créer, modifier et supprimer", ["clients.read"]),
        new("banks.read", "Banques", "Consulter", []),
        new("banks.write", "Banques", "Créer, modifier et supprimer", ["banks.read"]),
        new("accounts.read", "Comptes bancaires", "Consulter", []),
        new("accounts.write", "Comptes bancaires", "Créer, modifier et supprimer", ["accounts.read", "clients.read", "banks.read"]),
        new("templates.read", "Modèles d’exportation", "Consulter", []),
        new("templates.write", "Modèles d’exportation", "Configurer les formats", ["templates.read"]),
        new("documents.read", "Relevés et archives", "Consulter les PDF, mouvements et historiques", ["clients.read", "accounts.read", "banks.read", "templates.read"]),
        new("documents.write", "Relevés et archives", "Importer, adapter, corriger et valider", ["documents.read"]),
        new("documents.export", "Relevés et archives", "Télécharger les fichiers et exports", ["documents.read"])
    ];
    public static string[] All => Catalog.Select(p => p.Key).ToArray();
    public static string[] Reader => Catalog.Where(p => p.Key.EndsWith(".read")).Select(p => p.Key).ToArray();
    public static string[] Accountant => All.Where(p => !new[] { "banks.write", "templates.write", "clients.write", "accounts.write" }.Contains(p)).ToArray();
    public static string[] Validate(string[]? values)
    {
        if (values == null || values.Length > Catalog.Length)
            throw new AppException(400, "Liste de permissions invalide.");
        var selected = values.Distinct(StringComparer.Ordinal).OrderBy(p => p).ToArray();
        if (selected.Any(p => !All.Contains(p)))
            throw new AppException(400, "Permission inconnue.");
        if (Catalog.Where(p => selected.Contains(p.Key)).Any(p => p.Requires.Any(r => !selected.Contains(r))))
            throw new AppException(400, "Activez les droits de consultation requis avant d’accorder cette permission.");
        return selected;
    }
}
public record UserDto(Guid Id, string Name, string Email, string Role, bool IsActive, string[] Permissions, DateTime CreatedAt, DateTime? LastLoginAt, int Version);
public record UserInput(string Name, string Email, string Role, bool IsActive, string[] Permissions, string? Password, int? Version = null);
public record SetupInput(string Name, string Email, string Password);
public record LoginInput(string Email, string Password);
