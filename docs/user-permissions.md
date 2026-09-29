# Permissions des utilisateurs

Dans Utilisateurs, un administrateur ouvre le bouton **Permissions** du compte concerné. Les cases couvrent la consultation et la modification des clients, banques, comptes, modèles et profils, ainsi que le traitement et le téléchargement des relevés.

Les prérequis sont sélectionnés automatiquement ; retirer un prérequis retire les permissions dépendantes. Les administrateurs conservent tous les droits, dont la gestion des utilisateurs, qui ne peut pas être déléguée. Une liste vide retire tout accès métier. Les comptes existants conservent leurs droits historiques tant qu'aucune personnalisation n'est enregistrée.

Les permissions sont enregistrées dans `Users.AccessPermissions` (tableau PostgreSQL nullable). La colonne préexistante `Users.Permissions` est conservée sans modification. La migration `20260929130654_UserPermissions` a été appliquée à la base locale.

Le serveur vérifie les droits sur chaque contrôleur, y compris les accès directs, téléchargements, photos et suppressions liées. Les contraintes existantes d'accès aux conversions restent applicables. Les droits de consultation permettent de lire les informations ; ils ne constituent pas une protection contre la copie manuelle des données affichées.

Toute modification des permissions invalide la session de l'utilisateur à sa prochaine requête API. Une nouvelle connexion charge les nouveaux droits.

## Activation locale

Le redémarrage automatique a été bloqué par le contrôle d'exécution. Arrêter l'ancienne API depuis son terminal, puis lancer depuis la racine du projet :

```powershell
dotnet run --no-build --configuration Release --project backend/BankStatementConverter.API --urls http://localhost:5081
```

Le frontend reste sur http://localhost:3002.

## Validation

- 74 tests unitaires .NET réussis.
- Tests d'intégration utilisateurs/permissions réussis sur la base dédiée `alias_permissions_tests` : interdiction aux non-administrateurs, validation des prérequis, refus des téléchargements, attribution d'un droit et révocation des sessions.
- Deux tests Playwright réussis : édition des permissions et refus des pages non autorisées.
- Compilation frontend et ESLint réussis.
