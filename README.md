# Bank Statement Converter

## Administration des utilisateurs

Les administrateurs disposent de la page `/users` pour créer et modifier les comptes, gérer les rôles, réinitialiser un mot de passe et supprimer un compte sans historique. Voir [la documentation de gestion des utilisateurs](docs/gestion-utilisateurs.md). Une reconnexion est nécessaire après la mise à jour du contrôle des sessions.

## Interface Tailwind CSS et shadcn/ui

Le frontend utilise Tailwind CSS 4 et des composants shadcn/ui locaux. Le tableau de bord, l’import PDF et l’historique ont été modernisés, avec une harmonisation des formulaires et des thèmes clair/sombre.

Consulter le [rapport d’analyse frontend](docs/analyse-frontend-shadcn.md) pour le périmètre, les captures, les vérifications et les limites. Les tests navigateur se lancent avec `npm run test:e2e` depuis `frontend/Import-pdf-excel-master` (Chrome requis ; API simulée dans les tests).

## Profil automatique multibanque

Le profil partage `Extraction automatique multibanque` est cree au demarrage apres migration. Il est disponible pour tous les comptes bancaires dans la configuration d'une conversion, sans creer un profil par banque.

Le moteur `multibank-auto` lit les PDF texte avec tableaux a colonnes tracees des exemples Attijariwafa, Banque Populaire, BMCE, Credit Agricole et Societe Generale du dossier `relvee` (nom du dossier avec accent dans le depot). Il conserve les dates de valeur, les references presentes et les libelles multilignes, filtre le texte incline et controle les soldes, reports et totaux. Les soldes initiaux ne deviennent pas des operations. Un document ambigu, un scan ou une structure non reconnue est refuse sans export partiel. Les scans ne sont pas pris en charge.

Validation : 20 PDF fictifs, 547 operations, exports Sage 100 / Sage X3 / CSV et parcours API sur cinq banques. Cette couverture ne garantit pas tous les formats reels de ces banques.

PostgreSQL fonctionne localement sur le PC. Docker Compose ne lance que l'API et le frontend, et exige `LOCAL_DATABASE_CONNECTION` vers cette base locale. Les anciens volumes Docker ne sont ni utilises ni supprimes.

## Retrouver les donnees actuelles sur un autre PC

Pour les nouvelles versions, utiliser le [transfert chiffre PostgreSQL + documents](data/transfer/README.md).
Executer la sauvegarde avant chaque push, puis restaurer dans une nouvelle base
locale apres le clone. Ne pas pousser les anciennes sauvegardes en clair.

La branche `version-4` inclut une sauvegarde de la base et des documents du 26 septembre 2026. Suivre [le guide de restauration](data/snapshot/README.md) pour la restaurer dans PostgreSQL local. Docker Compose ne restaure plus de base de donnees.

Application de conversion de relevés PDF texte vers Sage 100 (TXT), Sage X3 (CSV) et CSV configurable. Le frontend Next.js existant est conservé : pages, navigation, cartes et thèmes clair/sombre. Ses données opérationnelles proviennent maintenant de l’API.

## Architecture

```text
frontend/Import-pdf-excel-master/     Next.js 16.3.5, React 19.2.8, TypeScript, Tailwind 4
backend/BankStatementConverter.API/  HTTP, JWT, autorisation, Swagger, erreurs
backend/BankStatementConverter.Application/  DTO et contrats
backend/BankStatementConverter.Domain/       Entités et transactions normalisées
backend/BankStatementConverter.Infrastructure/ EF Core, services, PDF, exporters, stockage
backend/BankStatementConverter.Tests/         Tests unitaires et intégration PostgreSQL
docker-compose.yml
```

Les contrôleurs délèguent aux services. Les définitions de référentiels centralisent validation et projections DTO. Les modèles d’extraction PDF et d’export sont distincts. La base utilise des clés étrangères restrictives et des contraintes d’unicité. L’historique préserve les références utilisées : supprimer un référentiel déjà utilisé renvoie 409.

## Prérequis

- SDK .NET 8 ou SDK compatible capable de cibler .NET 8, runtime ASP.NET Core 8.
- Node.js 24, npm (le dépôt contient `package-lock.json`).
- PostgreSQL 16 ou supérieur (validation locale avec PostgreSQL 18).
- Docker Desktop avec moteur Linux pour le lancement en conteneurs.
- Accès NuGet/npm et Google Fonts à la compilation : le frontend conserve les polices Inter et Manrope existantes.

## Docker optionnel (API et frontend uniquement)

1. Copier `.env.example` vers `.env`.
2. Renseigner `LOCAL_DATABASE_CONNECTION` avec la connexion PostgreSQL locale, en utilisant `Host=host.docker.internal` depuis le conteneur, et `JWT_KEY` avec une cle d'au moins 32 octets. PostgreSQL doit accepter cette connexion avec authentification. Laisser `ADMIN_EMAIL` et `ADMIN_PASSWORD` vides pour conserver les comptes existants, et `DEMO_DATA=false`.
3. Démarrer le moteur Docker puis exécuter depuis la racine :

```sh
docker compose up --build -d
```

- Interface : http://localhost:3002
- Swagger : http://localhost:5081/swagger
- Santé API/base : http://localhost:5081/health

Les migrations sont appliquees au demarrage du backend dans la base locale configuree. Les documents restent dans le dossier `uploads/` du PC, monte dans le conteneur. Aucun service ni volume PostgreSQL n'est cree. `docker-compose.snapshot.yml` est un alias de cette configuration, sans restauration automatique. Les anciens volumes Docker restent intacts pour permettre une recuperation manuelle des donnees si necessaire. Arreter l'API et le frontend locaux avant ce lancement s'ils occupent deja les ports 5081 et 3002.

`NEXT_PUBLIC_API_URL` est intégré au bundle à la compilation. Recompiler le frontend si l’adresse publique change. CORS doit correspondre exactement à `FRONTEND_ORIGIN`. Les ports du compose sont limités à localhost. Pour une exposition publique : configurer domaine, HTTPS, secrets, sauvegardes et contrôle des inscriptions avant déploiement.

## Lancement local sans Docker

Créer une base PostgreSQL et un utilisateur applicatif. Exporter les variables suivantes dans le terminal qui lance .NET (PowerShell) :

```powershell
$env:ConnectionStrings__Default = 'Host=localhost;Port=5432;Database=convertbank;Username=converter;Password=VOTRE_SECRET'
$env:Jwt__Key = 'VOTRE_CLE_ALEATOIRE_DE_32_OCTETS_MINIMUM'
$env:Seed__AdminEmail = 'VOTRE_EMAIL'
$env:Seed__AdminPassword = 'VOTRE_MOT_DE_PASSE'
$env:Seed__DemoData = 'false'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Database__AutoMigrate = 'true'
dotnet restore BankStatementConverter.sln
dotnet run --project backend/BankStatementConverter.API --urls http://localhost:5081
```

Les valeurs ci-dessus sont des emplacements à remplacer, pas des identifiants fonctionnels. `.env` est chargé par Docker Compose, **pas automatiquement par .NET**. Le seed ne réinitialise pas les mots de passe existants et ne transforme pas un utilisateur existant en administrateur.

Dans un second terminal :

```powershell
cd frontend/Import-pdf-excel-master
Copy-Item .env.example .env.local
npm.cmd ci
npm.cmd run dev
```

Sous PowerShell, `npm.cmd` évite le blocage de `npm.ps1` par la stratégie d’exécution Windows. Sous Linux/macOS, utiliser `npm`.

## Base et migrations

Les banques peuvent recevoir un logo PNG ou JPEG (2 Mo maximum), avec aperçu et ajustement sans recadrage. Après la création, la fenêtre d’ajout du logo s’ouvre automatiquement et peut être fermée si aucun logo n’est souhaité. Les boutons « Ajouter un logo » ou « Modifier le logo » permettent ensuite de le remplacer ou de le supprimer. Ces actions sont réservées aux administrateurs ; les autres utilisateurs peuvent consulter les logos. La migration `BankLogo` ajoute leur stockage en base.

La migration initiale est incluse dans `Infrastructure/Migrations`. Pour gérer la base explicitement, conserver les variables de connexion ci-dessus :

```sh
dotnet tool restore
dotnet ef database update --project backend/BankStatementConverter.Infrastructure --startup-project backend/BankStatementConverter.API
dotnet ef migrations add NomMigration --project backend/BankStatementConverter.Infrastructure --startup-project backend/BankStatementConverter.API
dotnet ef migrations has-pending-model-changes --project backend/BankStatementConverter.Infrastructure --startup-project backend/BankStatementConverter.API
```

`Database__AutoMigrate=false` par défaut. Pour plusieurs instances en production, appliquer les migrations séparément avant démarrage. Le seed suppose que le schéma existe.

## Configuration

| Variable .NET | Utilité |
| --- | --- |
| `ConnectionStrings__Default` | Connexion PostgreSQL |
| `Jwt__Key` | Signature JWT, 32 octets minimum |
| `Jwt__Issuer`, `Jwt__Audience` | Valeurs attendues par la validation JWT |
| `Cors__Origins__0` | Origine du frontend, localhost:3002 par défaut |
| `Storage__Root` | Répertoire privé de fichiers, `uploads` par défaut |
| `Storage__MaxBytes` | Taille PDF maximale, 20 Mo par défaut |
| `Seed__AdminEmail`, `Seed__AdminPassword` | Création initiale de l’administrateur |
| `Seed__DemoData` | Référentiels fictifs optionnels |
| `Database__AutoMigrate` | Application des migrations au démarrage |
| `Swagger__Enabled` | Swagger hors environnement Development |
| `AllowedHosts` | Hôtes HTTP autorisés |

Les fichiers sont stockés sous `uploads/input` et `uploads/output` avec des noms aléatoires. Aucun dossier de fichiers n’est exposé comme contenu statique. Pas de rétention automatique ni de chiffrement au repos intégré : configurer ces politiques au niveau du stockage. Les journaux JSON sont écrits sur la console, sans mot de passe ni jeton.

## Authentification et droits

`POST /api/auth/register` crée toujours un **User** ; le rôle ne peut pas être choisi dans le formulaire. `POST /api/auth/login` retourne `{ token, expiresAt, user }`. Les mots de passe sont hachés avec ASP.NET PasswordHasher ; les JWT expirent après 60 minutes. Le frontend conserve le jeton dans `sessionStorage` et se déconnecte sur expiration/401.

Cette version vise **un seul cabinet avec référentiels partagés** : tous les utilisateurs authentifiés peuvent gérer clients et comptes. Les mutations banques/profils/modèles d’export exigent **Admin**. Chaque utilisateur accède seulement à ses conversions et fichiers ; Admin peut consulter toutes les conversions. L’inscription est ouverte : restreindre cet endpoint si seuls les membres invités doivent avoir accès au cabinet.

## API

Dans Swagger, faire un login puis utiliser **Authorize** avec le jeton retourné.

| Routes | Méthodes |
| --- | --- |
| `/api/auth/login`, `/api/auth/register` | POST |
| `/api/clients`, `/api/banks`, `/api/bank-accounts` | GET, POST ; GET, PUT, DELETE sur `/{id}` |
| `/api/bank-statement-templates`, `/api/export-templates` | GET, POST ; GET, PUT, DELETE sur `/{id}` |
| `/api/conversions` | POST multipart |
| `/api/conversions/{id}/download` | GET authentifié |
| `/api/history`, `/api/history/{id}` | GET |
| `/api/dashboard` | GET |

Listes : `?page=1&pageSize=20`, maximum 100, résultat `{items,total,page,pageSize}`. Historique : filtres `search` et `status` (`Pending`, `Processing`, `Completed`, `Failed`). Erreurs : 400 validation, 401 session, 403 droits, 404 absent, 409 conflit, 413 taille, 429 limitation, 500 erreur inattendue sans stack trace dans le corps.

## Conversion de démonstration

### Gestion des banques

La page **Banques** permet aux administrateurs d’ajouter, modifier et supprimer les banques. Le nom et le code sont obligatoires ; le code est normalisé en majuscules et doit être unique. La description est facultative. Recherche par nom/code/description, tri et filtre « Avec profil actif / À configurer » sont exécutés par l’API avec pagination.

Le bouton **Détails** affiche les comptes et profils associés. Une banque ayant encore des comptes ou des profils ne peut pas être supprimée ; l’API renvoie 409 avec une explication. Les nombres de clients, comptes et profils sont calculés en base. Les utilisateurs non administrateurs voient un message de consultation seule. Après un changement de rôle en base, se déconnecter puis se reconnecter pour renouveler le JWT.

Routes complémentaires : `GET /api/banks/{id}/accounts` et `GET /api/banks/{id}/profiles`, avec `page` et `pageSize`. La liste des banques accepte `search`, `status=ready|missing` et `sort=name|name-desc|code`.

### Essayer une conversion

1. Activer le seed fictif et se connecter.
2. Dans Conversions, choisir Client fictif → Compte fictif → Fixture délimitée fictive.
3. Choisir l’un des trois exports.
4. Charger `backend/BankStatementConverter.Tests/Fixtures/fictional-statement.pdf`.
5. Cliquer Convertir : deux transactions s’affichent, puis télécharger le fichier. L’historique permet de le retélécharger.

```text
PDF → PdfPig → parser configurable → BankTransaction[] → validation → stratégie d’export → fichier
```

L’API vérifie taille effective, extension, MIME, signature PDF et cohérence client/compte/banque/profil. Une conversion validée est historisée, y compris lorsqu’une erreur de parsing survient ensuite. Les fichiers rejetés avant traitement ne créent pas d’historique. Une ligne invalide bloque tout l’export ; aucune ligne n’est silencieusement abandonnée.

## Profils PDF : limites explicites

Le parser livré lit des **lignes délimitées** (`;`, tabulation ou `|`) dans le texte extrait. Il ne reconstitue pas universellement les tableaux visuels bancaires. Configurer les indices de colonnes (base 0), la date, la culture des nombres et le nombre de lignes initiales à ignorer. Les en-têtes répétés, libellés sur plusieurs lignes, mises en page par coordonnées ou formats propres à une banque demandent un parser supplémentaire implémentant `IBankStatementParser` avec une nouvelle clé. Aucun profil de banque réelle n’est inventé.

PDF image/scanné ou page sans texte : erreur explicite ; le contrat `IOcrService` est prévu, aucun moteur OCR n’est livré. Maximum 200 pages et 2 millions de caractères extraits. Il faut tester et valider un relevé réel avant son utilisation comptable.

Montants : `decimal`, espaces normaux/insécables acceptés, `1 250,50`, `1,250.50`, `1250.50`, valeurs négatives. Un séparateur suivi de trois chiffres est interprété selon la culture configurée ; les groupements mal formés et les montants de plus de deux décimales sont rejetés. Un montant négatif dans une colonne débit/crédit inverse le sens. Deux colonnes simultanément non nulles sont rejetées.

## Formats d’export

| Format | Colonnes initiales |
| --- | --- |
| Sage 100 TXT | Date, Journal, Compte, Libellé, Débit, Crédit, Référence |
| Sage X3 CSV | Date, Compte, Libellé, Montant, Sens, Analytique |
| CSV personnalisé | Date, Libellé, Débit, Crédit, Solde |

Ces formats suivent les champs du cahier des charges, **sans certification d’un format Sage non fourni**. Vérifier le modèle d’import de votre installation Sage. Paramètres modifiables : champs et ordre, champs obligatoires, formats date/montant, séparateur, culture, en-tête, UTF-8/Windows-1252. Le compte exporté est le **compte comptable**, distinct du numéro bancaire. `Amount = Credit - Debit`, `Direction = D/C` ; l’analytique reste vide tant qu’aucune règle métier ne l’alimente. Pas d’écritures de contrepartie inventées, pas de validation de rapprochement de solde revendiquée. CsvHelper assure l’échappement des séparateurs et guillemets.

## Vérifications

```sh
dotnet build BankStatementConverter.sln
dotnet test BankStatementConverter.sln --filter 'Category!=Integration'
```

Tests d’intégration (base **dédiée**, jamais une base de production) :

```powershell
$env:TEST_DATABASE_URL = 'Host=localhost;Database=bankconverter_tests;Username=converter;Password=VOTRE_SECRET'
dotnet test BankStatementConverter.sln
```

Les tests appliquent les migrations, créent des identifiants aléatoires et vérifient login valide/invalide, CRUD clients, contraintes, droits Admin/User, conversions réelles avec la fixture PDF pour les trois formats, historique et téléchargement. Les données de test restent dans la base dédiée.

```sh
cd frontend/Import-pdf-excel-master
npm run lint
npm run build
```

Références des bibliothèques : [CsvHelper](https://www.nuget.org/packages/CsvHelper/33.1.0), [Npgsql EF Core](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/8.0.11), [PdfPig](https://github.com/UglyToad/PdfPig).

Le [rapport de vérification locale](docs/verification.md) distingue les contrôles réalisés et les limites de l’environnement. Un test du client TypeScript réel est disponible dans `scripts/smoke-api.mjs` : renseigner `SMOKE_EMAIL` et `SMOKE_PASSWORD`, activer le seed fictif, puis exécuter `node scripts/smoke-api.mjs` avec Node 24 et l’API démarrée. Ce test crée une conversion fictive persistée.

## Interface ReleveFlow

L’interface utilise une palette bleue et bleu nuit, avec thèmes clair et sombre mémorisés dans le navigateur. Le thème peut être changé depuis la barre supérieure ou la page Paramètres. La navigation devient un menu latéral modal sur les petits écrans.

Les clients disposent d’un avatar calculé à partir de leurs initiales et d’une couleur stable. Un clic sur l’avatar permet d’importer, remplacer ou supprimer une photo ou un logo (PNG/JPEG, 2 Mo maximum), avec aperçu avant enregistrement. Le navigateur ajuste l’image à 768 pixels maximum sans recadrage. La photo est conservée dans PostgreSQL et accessible uniquement via l’API authentifiée. Les coordonnées email et téléphone sont cliquables ; les actions de création, modification et suppression utilisent les API existantes.

Les icônes proviennent de `lucide-react`. Les transitions utilisent CSS et respectent `prefers-reduced-motion`. Les formulaires modaux utilisent le dialogue natif du navigateur pour gérer le focus et la touche Échap.

Vérification du 23 septembre 2026 : build Next.js de production, TypeScript et ESLint réussis. Réponses HTTP 200 pour connexion, inscription, tableau de bord, clients, banques, paramètres et conversions. Authentification administrateur et lectures API dashboard/clients/banques vérifiées. Le contrôle visuel et les interactions dans un navigateur automatisé n’ont pas pu être réalisés dans cette session.

Pour tester manuellement : ouvrir http://localhost:3002, se connecter, basculer les deux thèmes, tester le menu avec une fenêtre étroite, puis ouvrir les formulaires clients et banques. Vérifier les opérations de gestion avec des données de test.

Photos clients : migration `ClientProfilePhoto`, endpoints authentifiés `GET/PUT/DELETE /api/clients/{id}/photo`. Le PUT reçoit le champ multipart `file`. La suppression du client supprime également sa photo. Les listes ne contiennent que la version de photo ; les images sont chargées séparément.

Vérification complémentaire du 23 septembre 2026 : 22 tests backend réussis, y compris import/remplacement/lecture/suppression de photo, refus des fichiers de format non autorisé et des fichiers trop volumineux, et accès sans authentification. Build Next.js, TypeScript et ESLint réussis. Le rendu et les interactions restent à vérifier manuellement dans un navigateur.

## Espace clients et archivage

La page **Espace clients**, accessible dans le menu après connexion, regroupe les traitements réussis par client. Cet espace est partagé entre les opérateurs authentifiés de l’application, comme le répertoire clients. L’historique personnel existant conserve ses propres règles d’accès.

Chaque nouvelle conversion réussie conserve trois fichiers : le PDF original, un CSV standard UTF-8 séparé par des points-virgules, et l’export comptable choisi. Le CSV standard contient toutes les colonnes actuellement disponibles dans le modèle des opérations : Date, Reference, Description, Debit, Credit, Balance, Amount, Direction, Account, Journal et Analytic. Les dates suivent yyyy-MM-dd et les nombres utilisent le point décimal. Le format Sage 100 reste TXT lorsque le modèle le prévoit ; les exports Sage X3 et personnalisés suivent leur configuration existante.

La rétention est de **5 traitements réussis au total par client**, tous comptes et utilisateurs confondus, ordonnés par date de réussite (pas par mois indiqué dans le relevé). À la prochaine réussite, les traitements au-delà de cinq sont supprimés de la base. Leurs fichiers sont placés dans une file de suppression persistante puis effacés par un service exécuté toutes les cinq secondes. Une erreur de suppression est réessayée, y compris après redémarrage. Un verrou transactionnel par client protège la limite lors d’imports simultanés. Un échec n’évince aucune archive existante ; les sorties intermédiaires sont nettoyées et le PDF original est conservé pour une nouvelle tentative.

Les anciennes conversions restent consultables avec les fichiers déjà disponibles : aucun CSV standard n’est inventé rétroactivement. La page signale son absence. Pour les clients ayant déjà plus de cinq réussites, la purge des anciennes conversions intervient à la prochaine conversion réussie ; la page ne présente que les cinq plus récentes.

API authentifiée : `GET /api/client-workspace/{clientId}/archives` et `GET /api/client-workspace/{clientId}/archives/{id}/files/{kind}` avec `kind` égal à `original`, `standard` ou `export`. La migration EF `ClientArchives` ajoute les références de fichiers standards, le libellé du modèle utilisé et la file de nettoyage.

Test manuel : choisir un client dans Espace clients, cliquer sur Importer un relevé (client présélectionné), réaliser une conversion et revenir aux archives. Télécharger les trois fichiers. Sur un client de test, répéter jusqu’au sixième succès pour constater le remplacement du plus ancien. La suppression est définitive.

## Importation et traitement en deux étapes

La page Conversions importe uniquement le PDF avec le client et son compte bancaire (`POST /api/documents`). Le document est enregistré en état Pending, sans profil ni modèle d’export requis. Dans Espace clients, « Configurer et traiter » demande ensuite le profil actif de la banque et le format d’export (`POST /api/documents/{id}/process`). Le statut passe à Processing puis Completed ou Failed. Une tentative échouée conserve le PDF ; une nouvelle tentative réutilise le même dossier. Un traitement déjà terminé ou simultané est refusé. La limite de cinq concerne uniquement les réussites ; les documents en attente ou en échec sont conservés séparément.

« Consulter » affiche le PDF via le lecteur du navigateur, les CSV dans un tableau et les TXT dans un aperçu texte. Les aperçus CSV sont limités à 500 lignes, les TXT à 100 000 caractères ; les téléchargements restent complets. Le séparateur et l’encodage utilisés sont mémorisés pour les nouveaux exports. Pour les anciens exports sans ces métadonnées, l’aperçu utilise UTF-8 et le point-virgule. Les fichiers restent accessibles uniquement après authentification.

Validation : tests d’intégration du parcours import sans profil → consultation PDF → échec de lecture → consultation du PDF conservé → nouvelle tentative réussie sur le même ID → aperçu standard/export, refus sans connexion et double traitement, et maintien de cinq réussites. Build frontend, TypeScript et ESLint vérifiés. Le rendu du lecteur PDF reste à valider manuellement selon le navigateur.

### Modification et suppression manuelles des relevés

Dans Espace clients, « Modifier » est disponible pour les dossiers Pending/Failed : changement de client et de compte bancaire, remplacement facultatif du PDF. Le dossier conserve son identifiant et revient en attente de traitement ; l’ancien PDF remplacé est supprimé par la file de nettoyage. Le compte doit appartenir au client choisi. Les exports terminés ne sont pas modifiables, afin de préserver leur cohérence avec le PDF.

« Supprimer » demande une confirmation puis supprime le dossier et programme la suppression de tous ses fichiers. Cette action est possible pour les dossiers en attente, en échec et terminés. Une suppression en cours de traitement est refusée. Les endpoints `PUT /api/documents/{id}` (multipart clientId, bankAccountId, file facultatif) et `DELETE /api/documents/{id}` nécessitent une connexion. Les modifications et suppressions utilisent un verrou sur le dossier ; le démarrage d’un traitement vérifie aussi sa version pour ne pas utiliser un ancien PDF après une modification concurrente.

Vérifications : 22 tests backend réussis, incluant remplacement PDF, compte invalide, refus de modification d’un traitement terminé, suppression complète avec nettoyage des fichiers et refus sans authentification. Build frontend et ESLint réussis. Les interactions visuelles restent à vérifier manuellement.

## Conversion BMCE scannée et formats demandés

Les anciens moteurs BMCE ont ete retires. Les profils existants passent au moteur automatique au demarrage ; seuls les PDF texte compatibles sont pris en charge.

Les nouveaux modèles par défaut ont été corrigés : Sage 100 TXT, espace, Windows-1252, Date/Compte/Libellé/Montant/Sens/Analytique (montant positif et D/C) ; Sage X3 CSV, point-virgule, UTF-8, Date/Journal/Compte/Libellé/Débit/Crédit/Référence. Les libellés comportant des espaces sont entourés de guillemets dans le TXT délimité. L’en-tête est activé par défaut et reste configurable. Cela correspond aux paramètres demandés, sans présumer la compatibilité avec tous les paramétrages d’import Sage ni un format Sage à largeur fixe. Les modèles déjà personnalisés ne sont pas écrasés ; de nouveaux modèles explicites ont été créés pour le test BMCE.

Validation du relevé fourni : 45 opérations, débits 39 227,31 MAD, crédits 31 542,00 MAD, solde initial 29 282,67 MAD et solde final 21 597,36 MAD. Les répétitions légitimes de frais sont conservées. 24 tests backend réussis avec le PDF local activé, build frontend et ESLint réussis. Les trois conversions sont testées via l’API locale, avec téléchargement et aperçu. Aucun navigateur contrôlable n’était disponible pour vérifier les interactions visuelles ; l’acceptation des fichiers dans Sage reste à tester dans Sage.

## PDF BMCE contenant du texte

Les anciens moteurs BMCE ont ete retires. Les profils existants passent au moteur automatique au demarrage ; seuls les PDF texte compatibles sont pris en charge.

Validation sur `releve_bancaire_1.pdf` : 48 opérations, débits 6 520 900,00 MAD, crédits 4 225 000,00 MAD. Les soldes et dates de valeur absents restent vides. Les opérations d’octobre sont conservées malgré l’en-tête annonçant septembre. Les tests locaux peuvent être activés avec `BMCE_TEXT_TEST_PDF` pour le texte et `BMCE_TEST_PDF` / `BMCE_OCR_SCRIPT` pour le scan. Vérification : 29 tests backend réussis avec les deux PDF, build frontend et ESLint réussis. Les trois exports du PDF texte ont aussi été traités via l’API locale, avec téléchargement et aperçu, dans le client TEST conversion BMCE texte. Les interactions visuelles et l’import dans Sage restent à vérifier manuellement.
## Affichage guidé des conversions

Le frontend présente trois étapes : document importé, lecture et mapping, résultat et export. La configuration affiche les correspondances réelles du modèle sélectionné (ordre, format, champs obligatoires, séparateur et encodage). Le traitement reste atomique : il génère les fichiers puis affiche le résultat et permet de télécharger l’export.

Le résultat affiche les crédits, débits, soldes disponibles et les transactions avec recherche, filtre débit/crédit et pagination de 20 lignes. Les mêmes contrôles sont accessibles depuis Espace clients → CSV standard → Consulter ; un volet conserve toutes les colonnes originales. Pour un aperçu tronqué, les totaux sont explicitement limités aux lignes affichées. Le solde initial reconstitué ne vaut pas validation comptable indépendante ; les soldes absents restent signalés. Présentation responsive compatible avec les thèmes clair et sombre, sans dépendance supplémentaire.
## Suppression des banques et clients avec leurs dépendances

Le bouton Supprimer ouvre un récapitulatif réel des comptes, profils, traitements et fichiers concernés. La suppression complète nécessite une case de confirmation explicite. Supprimer une banque conserve les clients ; supprimer un client conserve les banques, profils bancaires et modèles d’export. Les historiques liés sont supprimés, puis les comptes et profils concernés, dans une transaction. Les PDF et exports sont placés dans la file de nettoyage durable après validation de la transaction.

Les endpoints authentifiés GET /api/catalog-deletions/{banks|clients}/{id} et POST au même chemin (corps JSON : version) portent ce parcours. Les banques nécessitent le rôle Admin. La version du récapitulatif doit rester identique et aucun traitement ne doit être en cours. Un verrou transactionnel court protège la suppression contre les changements simultanés. Les anciens DELETE restent conservateurs. Pour éviter de recréer la banque fictive après une suppression, désactiver Seed:DemoData une fois la démonstration installée ; le lanceur local a été ajusté en ce sens.
