# Base de transfert

`convertbank.dump` contient le catalogue bancaire, les références des PDF originaux
et les modèles d’exportation avec leurs colonnes et réglages.
Les fichiers correspondants sont dans `backend/BankStatementConverter.API/uploads`.
Restaurer avec `pg_restore` dans une base nouvellement créée, puis démarrer l’API
avec `Database:AutoMigrate=true`. La migration `AddNormalizedStatementExports`
ajoute `StatementImports` et `ExportJobs` sans modifier les modèles existants.
Le dump de catalogue antérieur ne contient pas les futurs imports et exports :
une sauvegarde complète de la base courante est nécessaire pour les transférer aussi.
Voir [le guide principal](../../README.md).
