# Alias — gestion bancaire

Application locale pour gérer les clients, banques, comptes bancaires, PDF originaux,
modèles d’exportation et conversion de relevés PDF en CSV/TXT.

## Installation

Prérequis : .NET 8, Node.js et npm, PostgreSQL 17. Pour l’extraction : un jeton
PaddleOCR AI Studio (moteur VL-1.6 distant), ou Python 3.12 pour le moteur OCR local.

Sur un nouveau poste, créer une base PostgreSQL et restaurer le catalogue fourni :

```powershell
createdb -U postgres -W alias
pg_restore -U postgres -W -d alias --no-owner --no-acl --exit-on-error --single-transaction data/transfer/convertbank.dump
```

Les PDF sont dans `backend/BankStatementConverter.API/uploads` et doivent accompagner le projet.
Configurer `ConnectionStrings:Default` dans le fichier local
`backend/BankStatementConverter.API/appsettings.Development.json` pour la base restaurée.

Pour une base vide, configurer `ConnectionStrings:Default` dans
`backend/BankStatementConverter.API/appsettings.Development.json` et activer
`Database:AutoMigrate` au premier démarrage. Après restauration du catalogue fourni,
activer également cette option pour appliquer les migrations supplémentaires.
Le schéma comprend `Clients`, `Banks`, `BankAccounts`, `Documents`,
`PendingFileDeletions`, `ExportTemplates`, `ExportTemplateField`, `StatementImports`,
`ExportJobs` et les métadonnées de migration EF Core.
Les données de démonstration sont désactivées par défaut.

## Lancement

Sous Windows, **Lancer-local.cmd** démarre l’API et l’interface. Les modes serveur
et fenêtre desktop, ainsi que la préparation du livrable autonome, sont détaillés
dans [le guide serveur](installer/INSTALLATION.md) et [le guide desktop](installer/GUIDE-DESKTOP.md).

Pour utiliser **PaddleOCR-VL-1.6 comme sur le site AI Studio**, configurer
`Ocr:Provider=AI_STUDIO` et `Ocr:CloudToken` côté serveur. Les PDF sont alors
analysés par l’API officielle ; voir [la configuration PaddleOCR](services/ocr/README.md).

Pour le mode `Ocr:Provider=LOCAL`, démarrer aussi le moteur OCR local :

```powershell
powershell -ExecutionPolicy Bypass -File services/ocr/start.ps1
```

Voir [l’installation OCR](services/ocr/README.md) sur un nouveau poste.

```powershell
dotnet run --project backend/BankStatementConverter.API --urls http://localhost:5081
```

Dans un deuxième terminal :

```powershell
cd frontend
npm ci
npm run dev
```

Ouvrir `http://localhost:3002`. Le navigateur accède à l’API par la même origine.
À la première ouverture, créer le compte administrateur. La page **Utilisateurs &
permissions** permet ensuite de gérer les collaborateurs et leurs accès.
Plusieurs collaborateurs peuvent convertir en même temps : deux lectures OCR actives
par défaut, avec attente automatique pour les suivantes et suivi propre à chaque
utilisateur. Les réglages `Ocr:MaxConcurrentJobs`, `Ocr:MaxQueuedJobs` et
`Ocr:QueueWaitSeconds` permettent d'ajuster cette capacité côté serveur.
L’espace clients regroupe les relevés convertis, leurs fichiers Sage/CSV archivés
et les PDF déposés. L’import de conversion peut sélectionner le client et son compte.
Les imports existants peuvent être classés depuis **Conversion & export**.
Les autorisations sont définies dans `backend/BankStatementConverter.Application/Permissions.cs`.
Les fichiers supprimés sont nettoyés après validation de la transaction en base.
L’écran « Modèles d’exportation » permet de conserver et de modifier les formats CSV et Sage,
avec leurs colonnes, séparateurs, encodages et formats de nombres.

L’écran **Conversion & export**, accessible à `/conversions`, permet d’importer
un relevé PDF, de lire ses mouvements, de corriger les données, de les valider et de télécharger
un CSV ou TXT avec un modèle existant. L’aperçu contrôle toutes les transactions
et affiche les 50 premières. Les soldes initial et final sont nécessaires pour Sage X3.
Choisir la banque et la devise dans la fenêtre d’importation. Le PDF original reste
consultable depuis le relevé. En mode AI Studio, PaddleOCR-VL-1.6 lit les pages et
renvoie des tableaux structurés, du Markdown et le JSON complet. L’onglet
**Texte lu & adaptation** permet de les consulter, télécharger et adapter par numéro
de colonne. Le mode local utilise le texte natif en priorité, puis l’OCR PP-OCRv6.
Le parcours sépare la lecture, l’adaptation des champs bancaires et l’exportation.
Les tests PDF historiques attendent un dossier de fixtures dont le nom commence par
`relv` à la racine. Ce dossier est absent de cette copie ; les tests concernés échouent
tant que leurs PDF ne sont pas restaurés.
Le contrat JSON reste disponible dans l’API pour les intégrations techniques.
Les contrats d'import et d'export sont dans `backend/BankStatementConverter.Application/Exports`.

## Vérification

```powershell
dotnet build BankStatementConverter.sln
dotnet test backend/BankStatementConverter.Tests/BankStatementConverter.Tests.csproj
cd frontend
npm run typecheck
npm run lint
npm run build
npm run test:e2e
```

Le test d’intégration crée sa propre base temporaire `alias_export_test_<UUID>` et
la supprime à la fin. Il utilise les identifiants de `TEST_DATABASE_URL` si cette
variable est définie, sinon ceux du fichier local de développement. Le rôle PostgreSQL
doit pouvoir créer des bases ; aucune écriture de test ne cible la base de travail.
Pour ne lancer que les tests unitaires, ajouter `--filter 'Category!=Integration'`.
Voir [le guide desktop](installer/GUIDE-DESKTOP.md) pour le lanceur Windows.

## Installation serveur Windows et Linux

Voir [le guide serveur](installer/INSTALLATION.md) pour installer Alias une seule
fois et ouvrir l'application depuis les postes avec un navigateur.
Les scripts de fabrication sont `scripts/Build-Server-Installer.ps1` (Windows 10/11 x64)
et `scripts/Build-Linux-Package.ps1` (Docker Engine/Compose sous Linux).

## Architecture et maintenance

Voir [l'analyse et les conventions du projet](docs/qualite-projet.md) pour
l'organisation du code, le nettoyage réalisé et les problèmes restant à traiter.

La commande `powershell -ExecutionPolicy Bypass -File scripts/Test-Project.ps1`
vérifie la compilation, le formatage, les types, le lint et les tests unitaires.
Ajouter `-IncludeE2E` pour les tests navigateur ou `-IncludeIntegration` pour les
tests nécessitant PostgreSQL. Les contrôles s'arrêtent au premier échec.

