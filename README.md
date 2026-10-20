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

Le mode **Automatique** lit d'abord le texte natif, puis utilise PaddleOCR uniquement
sur les pages suspectes et OpenRouter gratuit pour les pages encore non résolues.
Il conserve les données obtenues avec leurs alertes de contrôle. Une page illisible
ou une omission détectée bloque la validation ; le PDF original reste disponible.
Il n'y a plus de plafond applicatif par défaut de taille de PDF, de transactions,
de pages ni de durée de conversion. Les anciennes valeurs `Storage:MaxBytes` ne
limitent plus les imports PDF. La file OCR multiutilisateur reste active.

Le PDF structuré des imports reprend la transcription et les champs du document
source, dans leur disposition lorsqu'elle est disponible. Il conserve les valeurs
imprimées, sans ajouter de colonnes, de soldes calculés ni de statut de validation.
Les transcriptions sans coordonnées sont remises en page en conservant l'ordre
des textes et les cellules des tableaux. Les corrections de mouvements et les
formats du modèle s'appliquent au fichier d'export CSV/TXT ; les sélections filtrées
sauvegardées disposent de leur propre rapport PDF de sélection.
Démarrer `services/ocr/start.ps1` pour utiliser PaddleOCR local.

La comparaison avec le projet de factures et les adaptations réalisées sont détaillées
dans [l'analyse de la stratégie d'extraction](docs/analyse-strategie-factures-releves.md).

OpenRouter est également disponible dans **Configuration OCR → Moteur de lecture**.
Il transcrit les PDF page par page en conservant les colonnes et lignes dans un texte
source, puis les modèles Sage/CSV existants préparent l'export. Voir
[la configuration et les tests OpenRouter](services/openrouter/README.md).

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
Le contrat JSON reste disponible dans l’API pour les intégrations techniques.
Les contrats d'import et d'export sont dans `backend/BankStatementConverter.Application/Exports`.

## Vérification

```powershell
dotnet build BankStatementConverter.sln
cd frontend
npm run typecheck
npm run lint
npm run build
```

Pour vérifier manuellement la conversion, ouvrir `http://localhost:3002/conversions`,
importer votre PDF, choisir le modèle d’exportation et comparer chaque mouvement,
libellé, débit et crédit au relevé original avant de télécharger le résultat.
Les suites de tests automatisés et leurs réponses API simulées ont été retirées.
Voir [le guide desktop](installer/GUIDE-DESKTOP.md) pour le lanceur Windows.

## Installation serveur Windows et Linux

Voir [le guide serveur](installer/INSTALLATION.md) pour installer Alias une seule
fois et ouvrir l'application depuis les postes avec un navigateur.
Les scripts de fabrication sont `scripts/Build-Server-Installer.ps1` (Windows 10/11 x64)
et `scripts/Build-Linux-Package.ps1` (Docker Engine/Compose sous Linux).

## Architecture et maintenance

Voir [l'analyse et les conventions du projet](docs/qualite-projet.md) pour
l'organisation du code, le nettoyage réalisé et les problèmes restant à traiter.

Les commandes de compilation, de formatage, de vérification TypeScript et de lint
restent disponibles. La vérification fonctionnelle s’effectue manuellement avec vos PDF.

