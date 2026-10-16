# Installation d’Alias Desktop 2.0.0

## Ce que contient le paquet

Application Windows 10/11, 64 bits, dans une fenêtre dédiée avec le logo Alias. Le frontend, le backend, .NET, Node.js et PostgreSQL sont inclus. Microsoft WebView2 est utilisé pour afficher l’interface ; son installateur télécharge le runtime si celui-ci manque.

L’installateur contient une copie privée de la base du projet à la date de fabrication et de ses fichiers. Les comptes et leurs mots de passe actuels sont conservés si vous choisissez cette copie. Les sessions, les clés de chiffrement et les clés API PaddleOCR ne sont pas transférées. Gardez ce paquet privé ; ne le publiez pas sur GitHub.

## Installation

1. Fermez toute ancienne fenêtre Alias Desktop. Lancez `Alias-Desktop-Setup-2.0.0.exe` et acceptez les droits administrateur.
2. Conservez le dossier programme proposé : `C:\Program Files\Alias Desktop`.
3. Conservez les ports **3010** et **5090**, ou choisissez deux ports libres. Ils restent accessibles uniquement sur votre PC.
4. Conservez le stockage proposé : `C:\ProgramData\Alias Desktop`. PostgreSQL reçoit automatiquement un port disponible entre 55432 et 55452, ainsi que de nouveaux identifiants techniques.
5. Sélectionnez **Importer la copie des données du projet** pour retrouver les données existantes. La restauration exige une base vide et ne remplace aucune base existante. L’autre choix démarre une nouvelle base sur un stockage neuf.
6. Terminez l’installation, puis double-cliquez sur **Alias Desktop** sur le bureau.

Les services `AliasDesktopPostgreSQL`, `AliasDesktopBackend` et `AliasDesktopFrontend` démarrent automatiquement avec Windows. Le logiciel n’exige pas de terminal ouvert. Le gestionnaire serveur Alias existant utilise d’autres services et reste indépendant.

## Connexion et PaddleOCR

- Copie du projet : utilisez les identifiants de votre compte actuel. Aucun mot de passe initial n’est inscrit dans ce guide.
- Base neuve : créez le premier compte administrateur au lancement.
- Dans **Configuration OCR**, sélectionnez votre moteur PaddleOCR, renseignez le jeton et enregistrez. La lecture par API PaddleOCR nécessite Internet et dépend du quota de votre compte.
- Qwen et Ollama ne sont pas inclus. Le prompt ChatGPT reste disponible sur le dashboard comme aide manuelle.

## Sauvegardes et réinstallation

Sauvegardez la base PostgreSQL avec `pg_dump` et le dossier `uploads` du stockage. Une copie à chaud du dossier `postgres` n’est pas une sauvegarde fiable. Les identifiants techniques se trouvent dans les fichiers protégés du stockage ; seul l’administrateur Windows peut les consulter.

La désinstallation retire les programmes et les services, mais conserve le dossier de données. Pour réinstaller avec une nouvelle copie du projet, choisissez un **nouveau dossier de données vide**. Ne supprimez pas les anciennes données avant d’en avoir une sauvegarde vérifiée.

## Dépannage

- Interface indisponible : ouvrez `services.msc` et vérifiez les trois services `AliasDesktop…`. Ils doivent être démarrés. Consultez le sous-dossier `logs` du stockage.
- Installation échouée : consultez `C:\Program Files\Alias Desktop\installer\desktop-installation.log`.
- WebView2 absent : exécutez `installer\MicrosoftEdgeWebview2Setup.exe /silent /install` depuis le dossier programme, avec Internet.
- Port occupé : sélectionnez un autre port lors de l’installation.
- Les PC du réseau ne peuvent pas utiliser cette version locale ; il leur faut la version serveur centralisée.

Cet installateur n’est pas signé. La compilation et les tests de restauration sont effectués sur le poste de fabrication ; un essai d’installation sur un PC Windows de validation reste nécessaire avant diffusion générale.
