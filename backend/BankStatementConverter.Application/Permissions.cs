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
        new("profiles.read", "Profils d’extraction", "Consulter", []),
        new("profiles.write", "Profils d’extraction", "Créer, modifier et supprimer", ["profiles.read", "banks.read"]),
        new("templates.read", "Modèles d’export", "Consulter", []),
        new("templates.write", "Modèles d’export", "Créer, modifier et supprimer", ["templates.read"]),
        new("documents.read", "Relevés et historique", "Consulter les relevés partagés et l’historique autorisé", ["clients.read", "accounts.read", "profiles.read", "templates.read"]),
        new("documents.write", "Relevés et historique", "Importer, traiter, modifier et supprimer", ["documents.read"]),
        new("documents.export", "Relevés et historique", "Télécharger les fichiers", ["documents.read"])
    ];
    private static void Require(bool condition, string message) { if (!condition) throw new AppException(400, message); }
    public static string[] Defaults => Catalog.Where(x => x.Key is not ("banks.write" or "profiles.write" or "templates.write")).Select(x => x.Key).ToArray();
    public static string[] Effective(string role, string[]? permissions) => role == "Admin" ? Catalog.Select(x => x.Key).ToArray() : permissions ?? Defaults;
    public static string[] Validate(string[]? permissions)
    {
        Require(permissions != null && permissions.Length <= Catalog.Length, "Liste de permissions invalide.");
        var selected = permissions!.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Require(selected.All(key => Catalog.Any(x => x.Key == key)), "Permission inconnue.");
        Require(Catalog.Where(x => selected.Contains(x.Key)).All(x => x.Requires.All(selected.Contains)), "Activez les droits de consultation requis avant d’accorder cette permission.");
        return selected;
    }
}
