# Alias — gestion et conversion bancaire

Application pour gérer les clients, banques, comptes, PDF originaux et modèles
d’exportation. Les relevés et extraits bancaires sont lus, contrôlés et corrigés
avant la préparation des fichiers CSV, Sage 100 ou Sage X3.

## Architecture

- **Backend** : ASP.NET Core / .NET 8, Entity Framework Core et PostgreSQL.
- **Interface** : Next.js 16, React 19, TypeScript et Tailwind CSS 4.
- **Lecture locale** : texte natif PDF, PaddleOCR PP-OCRv6 et PP-StructureV3.
- **Lecture distante facultative** : PaddleOCR AI Studio.
- **Livraison** : Docker, scripts Windows et utilitaires desktop/serveur.

Voir [l’analyse et les conventions du projet](docs/qualite-projet.md).

Le catalogue initial reprend les 21 banques conventionnelles et participatives
de la [liste SGFG](https://www.sgfg.ma/fr/garantie-des-depots/liste-des-banques-membres).
Les banques absentes et leurs logos embarqués sont ajoutés au démarrage. Les noms,
codes et logos personnalisés des banques existantes restent conservés ; les anciens
noms et codes reconnus évitent de réintroduire la même banque sous un nouveau nom.

## Installation locale

Prérequis : .NET 8, Node.js compatible avec Next.js 16, npm et PostgreSQL 17.
Python 3.12 est nécessaire pour le service OCR local.

Configurer la connexion PostgreSQL dans le fichier local non versionné
`backend/BankStatementConverter.API/appsettings.Development.json`.
Pour créer une installation vide, activer `Database:AutoMigrate` au premier
démarrage. Les données de démonstration sont désactivées par défaut.

Pour transférer une installation existante, restaurer sa sauvegarde PostgreSQL et
copier son stockage `backend/BankStatementConverter.API/uploads`. Le stockage et
les sauvegardes ne sont pas inclus dans Git. Voir [le guide de transfert](data/transfer/README.md).
Les clés de protection et les réglages chiffrés doivent accompagner le transfert
pour conserver les réglages OCR enregistrés.

```powershell
cd frontend
npm ci
```

Le fichier [.env.example](.env.example) décrit les réglages Docker et serveur.
Le fichier [frontend/.env.example](frontend/.env.example) décrit le proxy de l’interface.
Ne jamais mettre de véritables clés ou mots de passe dans ces exemples.

## Lancement

Sous Windows, `Lancer-local.cmd` démarre l’API et l’interface.
Pour démarrer les services séparément :

```powershell
dotnet run --project backend/BankStatementConverter.API --urls http://localhost:5081
```

Dans un autre terminal :

```powershell
cd frontend
npm run dev
```

Pour l’OCR local :

```powershell
# Premier lancement sur un nouveau poste avec Python 3.12 installé :
powershell -ExecutionPolicy Bypass -File services/ocr/start.ps1 -Install
# Lancements suivants :
powershell -ExecutionPolicy Bypass -File services/ocr/start.ps1
```

Ouvrir `http://localhost:3002` et créer le compte administrateur à la première
ouverture. L’interface accède à l’API par `/api`, sur la même origine. Le service
OCR local écoute par défaut sur `127.0.0.1:8091`.

## Traitement des relevés et extraits

1. Importer le PDF et choisir la banque, la devise et éventuellement son classement client.
2. Choisir un modèle d’exportation existant.
3. Comparer les mouvements reconnus au PDF original et corriger les informations nécessaires.
4. Vérifier les contrôles, puis télécharger le fichier au format du modèle choisi.

Le mode `AUTO` utilise d’abord le texte natif. Les pages suspectes passent à
PaddleOCR et PP-StructureV3. Les contrôles comparent les cellules, les positions,
les dates, les montants, les lignes reconnues et les soldes. Chaque nouvelle
lecture repart du PDF original ; aucun ancien résultat d’extraction n’est substitué.
Les poids des modèles OCR restent réutilisés pour éviter leur téléchargement à chaque import.

La chaîne locale ne nécessite aucun jeton. La page des paramètres propose Automatique,
PaddleOCR local et PaddleOCR AI Studio. Le mode Automatique utilise le moteur PaddleOCR
choisi ; aucun autre moteur distant de secours n’est appelé.
Voir [la configuration OCR](services/ocr/README.md).

L’application n’impose pas de plafond de taille PDF, de transactions, de pages ni
de durée de conversion par défaut. La file OCR reste partagée entre utilisateurs,
avec deux traitements actifs par défaut. Les contraintes matérielles et les
quotas éventuels des fournisseurs distants continuent de s’appliquer.

Le panneau **Documents** affiche le PDF original, le PDF converti et le texte source.
Il peut être replié pour donner toute la largeur au tableau. Le PDF converti reprend
les champs du document source ; les dates abrégées sont complétées lorsque la
période, l’en-tête ou les dates des soldes permettent de les déterminer.
Les corrections des mouvements et les formats du modèle s’appliquent à l’export
CSV/TXT. Les extraits filtrés sauvegardés possèdent leur propre aperçu PDF.

Le tableau affiche toutes les lignes dans une zone défilante. Les soldes initial
et final sont nécessaires pour Sage X3. Une page illisible, des lignes manquantes
ou une divergence financière doivent être examinées avant validation. Les contrôles
ne garantissent pas à eux seuls l’exactitude d’un document de format inconnu.

## Vérification

Sous Windows, cette commande compile le backend et les outils de livraison, vérifie
les scripts PowerShell, TypeScript, ESLint, le formatage et le build Next.js :

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Check-Project.ps1 -Production
```

Sans `-Production`, le build Next.js est omis. Le build de production utilise
`.next-runtime` pour préserver le serveur de développement dans `.next`.
Les vérifications individuelles sont également disponibles :

```powershell
dotnet build BankStatementConverter.sln
cd frontend
npm run typecheck
npm run lint
npm run format:check
npm run build
```

La validation fonctionnelle se fait manuellement avec vos PDF réels, en comparant
les dates, libellés, débits, crédits et soldes au document original. Les anciennes
suites de tests et leurs réponses API simulées ont été retirées.

## Maintenance et livraison

Le nettoyage propose une liste limitée de diagnostics et fichiers générés connus :

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Cleanup-GeneratedFiles.ps1
powershell -ExecutionPolicy Bypass -File scripts/Cleanup-GeneratedFiles.ps1 -Execute
```

Il conserve les sources, configurations locales, PDF importés, sauvegardes,
installateurs, runtime de lancement et modèles OCR. Les cibles utilisées par un
processus sont ignorées ; les jonctions et liens symboliques ne sont pas traversés.

Voir [le guide serveur](installer/INSTALLATION.md) et [le guide desktop](installer/GUIDE-DESKTOP.md).
Les scripts `Build-Server-Installer.ps1`, `Build-Desktop-Installer.ps1` et
`Build-Linux-Package.ps1` restent nécessaires à la fabrication des livrables.
