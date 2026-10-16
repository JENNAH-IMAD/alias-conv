# Alias Serveur 1.2.0 : installation automatique

Installer Alias uniquement sur le serveur. Les postes ouvrent l'adresse du serveur
dans leur navigateur ou avec le raccourci `Acces-Alias.url`. Aucun client a installer.

## Windows 10/11 x64

Lancer `Alias-Serveur-Setup-1.2.0.exe` avec les droits administrateur.
Le livrable inclut Node.js, le backend .NET autonome, PostgreSQL 18.6 et les services Windows.

1. Choisir deux ports libres pour le frontend et le backend (3002 / 5081 par defaut).
2. Conserver **Creer et configurer automatiquement la base locale (recommande)**.
   Aucune chaine de connexion ni aucun mot de passe a saisir.
3. Choisir un dossier de donnees dedie, hors du dossier du programme.
4. Renseigner `http://NOM_OU_IP_FIXE_DU_SERVEUR:PORT_FRONTEND`.
5. Apres installation, ouvrir Alias et creer le premier administrateur, puis les utilisateurs.
6. Configurer le moteur et le jeton PaddleOCR dans **Configuration OCR**, avec l'administrateur.

Le mode automatique cree un cluster PostgreSQL dedie a Alias et une base `alias`, avec
un compte applicatif `alias_app` non superutilisateur. Les mots de passe sont generes
aleatoirement. Le port PostgreSQL est choisi entre 55432 et 55452 et reste accessible
uniquement sur 127.0.0.1. Les autres installations PostgreSQL ne sont pas modifiees.

L'option **Utiliser une base PostgreSQL existante** affiche la connexion avancee et
ne cree pas de base locale. Le mode automatique ne retrouve pas les mots de passe
d'autres bases et ne transfere pas leurs donnees automatiquement.

Les services `AliasPostgreSQL`, `AliasBackend` et `AliasFrontend` demarrent automatiquement
et redemarrent apres defaillance. La base est demarree avant le backend. En mode avance,
seuls les services applicatifs sont installes. Les journaux tournants sont dans `logs`.

Le pare-feu ouvre le frontend pour le sous-reseau local, sur les profils Domaine/Prive.
En cas d'echec de configuration, consulter `installer/installation.log` dans le dossier
du programme, puis les journaux du dossier de donnees. Le journal de l'installateur masque
les mots de passe. Les services crees pendant une tentative echouee sont retires.
L'administrateur reseau doit adapter cette regle pour d'autres sous-reseaux. Le backend
et PostgreSQL ne doivent pas etre ouverts aux postes. Le serveur doit rester allume,
avec une adresse fixe et sans mise en veille.

Distribuer le fichier `Acces-Alias.url`, dans le dossier installe, sur les bureaux des
postes, manuellement ou par strategie de groupe. Il ouvre l'application dans le navigateur.
Pour une icone Alias sur les postes, copier le dossier `Acces-postes` du serveur dans un
dossier conserve du PC et double-cliquer `Creer-acces.cmd`. Il cree le raccourci
`Alias Entreprise` avec le logo et l'adresse du serveur. Ce script n'installe aucun logiciel
et ne demande pas de droits administrateur. Conserver le dossier pour son fichier icone.
Les postes doivent rejoindre le reseau du serveur et se connecter avec leur compte Alias.

## Donnees et reinstallation

L'icone **Gestion du serveur Alias** ouvre une application desktop reservee au serveur.
Windows demande les droits administrateur. Elle affiche l'etat des services, permet de
demarrer/arreter/redemarrer, d'ouvrir Alias, pgAdmin, la configuration OCR et les journaux.
Les informations PostgreSQL se consultent dans une boite dediee ; le mot de passe reste
masque tant que l'administrateur ne choisit pas de l'afficher ou de le copier.
pgAdmin doit deja etre installe sur le serveur ; il n'est pas inclus dans le gestionnaire.

Les ports frontend et backend sont modifiables avec l'adresse publique HTTP. Cette action
interrompt les utilisateurs, sauvegarde la configuration dans `logs/ports-backup-*`,
actualise les fichiers du proxy et le port de la regle de pare-feu, puis redemarre Alias.
En cas d'echec, la configuration precedente est restauree et le redemarrage est tente.
La base, son port, le jeton OCR et les PDF ne sont pas modifies. Les raccourcis des postes
doivent utiliser la nouvelle adresse apres un changement de port frontend.

Le dossier choisi (par defaut `C:\ProgramData\Alias Serveur`) contient :

- `Alias.Runtime.json` : connexion applicative privee.
- `Server.json` : ports et adresse d'acces.
- `uploads` : PDF, exports, cles et configuration OCR chiffree.
- `postgres` : donnees de la base automatique.
- `Postgres.Admin.json` : identifiants de maintenance reserves aux administrateurs
  et au compte d'installation ; aucun compte applicatif ne doit les recevoir.
- `logs` : journaux de demarrage et d'execution.

La desinstallation arrete et retire les services, le programme et la regle de pare-feu.
Elle conserve la base et le dossier de donnees. Reinstaller en mode automatique avec
le meme dossier reutilise la base et ses identifiants sans effacer les tables.
Une installation avec des services existants doit etre desinstallee avant reinstallation.
Une tentative echouee dont les services ont ete retires peut etre reprise directement,
meme si des fichiers du programme restent presents. Le dossier de donnees est conserve.

Sauvegarder PostgreSQL avec `pg_dump` et sauvegarder aussi `uploads` et la configuration
de maintenance. Ne pas copier a chaud le dossier `postgres` comme seule sauvegarde.
Pour importer une base existante, restaurer une sauvegarde dans la base cible et copier
le stockage complet, notamment `.keys` et `.settings`, avant l'ouverture aux postes.

## Linux

Extraire `Alias-Serveur-Linux.zip` dans un dossier conserve du serveur. Installer Docker
Engine, le plugin Compose et curl, puis executer :

```bash
cd /opt/alias/installer
bash install-linux.sh
```

Le script demande les ports et le nom/IP du serveur. PostgreSQL est deja cree
automatiquement en conteneur, avec un mot de passe aleatoire. Les donnees restent dans
les volumes Docker `alias-server_database` et `alias-server_documents`, avec une
configuration `.env` privee a conserver. La construction initiale necessite Internet.
Autoriser uniquement le port frontend depuis les postes dans le pare-feu Linux.
Les conteneurs redemarrent lorsque Docker demarre ; activer Docker au demarrage.

```bash
docker compose --env-file .env -f compose.linux.yml ps
docker compose --env-file .env -f compose.linux.yml logs --tail 100
docker compose --env-file .env -f compose.linux.yml down
docker compose --env-file .env -f compose.linux.yml up -d --build
docker compose --env-file .env -f compose.linux.yml exec -T db pg_dump -U alias -d alias -Fc > alias.dump
```

Ne pas utiliser `down -v` : cela supprimerait les volumes. Conserver le dossier du
deploiement et le fichier `.env` lors des mises a jour.

## Fabrication et verification

Depuis les sources : `scripts/Build-Server-Installer.ps1` fabrique l'installateur Windows,
et `scripts/Build-Linux-Package.ps1` fabrique l'archive Linux. Le poste de fabrication
Windows doit avoir le SDK .NET, Node.js et Inno Setup 6. Aucun secret ni PDF du poste
ne doit etre inclus. Les empreintes Node, WinSW et de l'archive PostgreSQL sont verifiees ;
le runtime Visual C++ doit porter une signature Microsoft valide.
Les conditions de fabrication commerciale d'Inno Setup sont sur
[le site de l'editeur](https://jrsoftware.org/isdl.php).

Les tests realises couvrent la creation d'une vraie base PostgreSQL, le compte applicatif
non superutilisateur, les ACL de maintenance, la conservation des donnees, les migrations
et le demarrage du backend publie. Le proxy publie avec des ports personnalises, les cookies
et le transfert de fichier ont egalement ete testes. Le script Linux est teste avec
Docker simule ; les conteneurs reels et l'installation des services Windows avec elevation
restent a valider sur les machines cibles.

Les livrables ouvrent HTTP dans le reseau local. Prevoir HTTPS avec un reverse proxy et
un certificat approuve par les postes pour l'exploitation, puis adapter le raccourci.
DNS, certificat et HTTPS ne sont pas automatises. L'installateur Windows n'est pas signe.
Prevoir les sauvegardes et la mise a niveau de .NET 8 pour un deploiement durable.
# Lecture PDF, PaddleOCR et PDF structuré

Le traitement utilise la lecture PDF et PaddleOCR. Les conversions peuvent produire un PDF structuré à cinq colonnes et un rapport de contrôle depuis l'aperçu, sans modèle de langage supplémentaire à installer.
