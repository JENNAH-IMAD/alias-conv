# Audit et nettoyage du backend

Le backend a été parcouru par ses points d’entrée HTTP, ses services enregistrés en injection de dépendances, ses modèles persistés et ses appels depuis le frontend et les outils du dépôt. Le nettoyage concerne le fonctionnement actuel : relevés, extraits, formats d’export et écritures calculées à partir de modèles clients.

## Inventaire après nettoyage

| Couche | Fichiers C# | Responsabilité |
| --- | ---: | --- |
| API | 27 | Authentification, permissions, catalogues, conversions, historique, dashboard et réglages OCR |
| Application | 8 | Contrats HTTP, interfaces d’export et de stockage, permissions |
| Domain | 7 | Relevés, montants, utilisateurs, modèles d’écritures et archives |
| Infrastructure hors migrations | 68 | PostgreSQL, extraction PDF/OCR, conversion, exports, stockage et calcul des écritures |
| Migrations et modèle EF historique | 33 | Installation et mise à niveau des bases existantes |

Les classes de contrôleurs, la fabrique EF de développement, les convertisseurs JSON et le résolveur de police sont découverts par les frameworks. Une absence d’appel C# explicite ne signifie donc pas qu’ils sont inutilisés.

## Code retiré

| Élément | Motif |
| --- | --- |
| `Infrastructure/Accounting/AccountingCsv.cs` | Import de plans, taxes, tiers et journaux appartenant à l’ancien module supprimé |
| `Infrastructure/Accounting/AccountingJournalExport.cs` | Export de préparations de journaux sans appel dans le parcours actuel |
| Anciennes fonctions de `AccountingEngine` | Gestion des plans, suggestions de contreparties, rapprochements et préparations retirée ; validation monétaire et interprétation du sens bancaire conservées |
| Ancienne branche de `EntryModelEngine` | Le seul appel métier exécutait le classement et le calcul de TVA ; suppression du chemin inutilisé de génération des contreparties |
| Anciens types de plans, journaux, allocations et préparations | Consommés uniquement par les fonctions retirées ; conservation des types nécessaires aux contrats et aux archives actuels |
| `API/DocumentsController.cs` et `Infrastructure/DocumentService.cs` | Ancien dépôt manuel de PDF sans appel depuis les pages ou outils actuels ; les imports courants passent par `StatementImportsController` |
| `DocumentDto` et ancienne route `/client-workspace/{clientId}/archives` | Contrats de cet ancien dépôt uniquement |
| Route `/client-workspace/accounting-entries` et son DTO | Liste en doublon d’un onglet déjà retiré de l’espace client |
| `frontend/src/components/client-accounting-entries.tsx` | Ancien composant sans import ni montage, seul consommateur de la liste en doublon |
| Ancien scénario de journal et sa fixture JSON dans `tools/AccountingExportChecks` | Vérifications propres au module retiré ; contrôles du classement, de TVA, des exports et du nettoyage conservés |

Les archives d’écritures restent consultables et téléchargeables par leurs points d’entrée dédiés. Les migrations existantes et les données utilisateur ne sont pas supprimées. L’entité historique `Document` reste nécessaire aux contraintes et au nettoyage des anciens documents lorsqu’un catalogue est supprimé.

## Simplifications et optimisation

- Les recherches des noms de clients et de comptes lors de la journalisation de plusieurs relevés sont groupées : au maximum deux requêtes de résolution, contre deux par relevé auparavant.
- Les configurations EF écrasées puis réappliquées ont été simplifiées en conservant le modèle final.
- La liste des formats de filtrage ne charge plus tous les clients ou les règles ACH/CHE ; elle lit uniquement les catalogues de formats bancaires et d’écritures.
- Le filtre par ancien identifiant de règle d’écriture, sans appel depuis l’interface, a été retiré. Les filtres par format bancaire et par format d’écriture sont conservés.
- `build-check` est exclu des sources et de la publication, ignoré par Git et déclaré dans l’outil de nettoyage des fichiers générés. Sa suppression physique a été refusée par le contrôle automatique d’exécution ; les 101 fichiers générés restent présents sur le poste.

## Fonctionnalités et dépendances vérifiées

| Ensemble | Décision et raison |
| --- | --- |
| Authentification, sessions, permissions, administration | Conservé : utilisé par le shell et les routes protégées |
| Clients, banques, comptes, photos et logos | Conservé : catalogues utilisés par les imports et les modèles clients |
| Extraction native, normaliseurs bancaires, OCR local/Paddle | Conservé : branches de lecture complémentaires selon le PDF et les réglages |
| Filtres/extraits sauvegardés et profils d’extraction | Conservé : utilisé par la conversion et la consultation des extraits |
| Exports CSV/Sage et compatibilité des anciens formats | Conservé : modèles et archives existants encore pris en charge |
| Modèles clients, formats d’écritures, calcul TTC/HT/TVA | Conservé : parcours actif des écritures comptables |
| Mémoire du dernier traitement et des paramètres | Conservé : réouverture des résultats sans refaire les choix |
| Historique, conservation et suppression différée de fichiers | Conservé : traçabilité et cohérence entre la base et le stockage |
| `HistoryCleanupService` | Conservé : appelé par `tools/HistoryMaintenance`, même s’il n’est pas un service HTTP |
| Migrations EF, fabrique de contexte, amorçage de la base | Conservé : nécessaire aux installations et mises à jour |
| PdfPig, PDFsharp, HtmlAgilityPack | Conservé : respectivement lecture native PDF, génération PDF et lecture des résultats structurés Paddle |
| EF Core/Npgsql, outils EF et Swagger | Conservé : persistance, maintenance des schémas et documentation API configurée |

Aucun autre fichier source orphelin n’a été établi par le croisement des références. Les répertoires `bin`, `obj` et les sorties temporaires ont été exclus de l’analyse du code métier.

## Vérifications

| Exigence | Preuve |
| --- | --- |
| Compiler le backend et l’outil de maintenance | `dotnet build BankStatementConverter.sln --artifacts-path .local/backend-clean-audit --warnaserror` : réussite, aucun avertissement ni erreur |
| Préserver classement, ambiguïtés et calcul de TVA | `tools/AccountingExportChecks` : 25 contrôles réussis, dont `TTC_20_Percent`, `TTC_10_Percent`, `Ambiguity_ExplicitSelection`, `Source_RemainsUnchanged` |
| Préserver les fichiers et les paramètres d’export | `IndependentTXT_OnePhysicalLinePerMovementPreservesSourceAndAmounts`, `EntryCatalog_ResultUsesSeparatorEncodingTypeAndColumns`, `Consultation_DownloadUsesDisplayModelParameters` : réussis |
| Préserver les archives et les derniers choix | `IndependentArchive_SurvivesModelAndSourceDeletion`, `Treatment_ExportKeepsLatestModelAndResult`, `Treatment_EntryPreviewSavesExactModelAndCalculatedRows` : réussis |
| Filtrer par formats d’exportation | `ClientSpace_FilterListsExportFormatsWithoutEntryRules`, `ClientSpace_FilterUsesArchivedEntryOutputFormat` : réussis |
| Préserver les migrations et les données associées | `RemovalMigration_PreservesEveryEntryModelAndVersion`, `RemovalMigration_PreservesArchivesFormatsAndBankAccounts` : réussis |
| Cohérence frontend après suppression du composant orphelin | TypeScript et ESLint ciblé : réussite |

Les contrôles d’intégration PostgreSQL s’exécutent dans des schémas temporaires distincts, supprimés à la fin. Ils ne modifient pas les données de l’application. Ils ne remplacent pas une validation de lecture sur chaque mise en page de PDF bancaire réel.
