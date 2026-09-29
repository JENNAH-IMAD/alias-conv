# Transfert vers un autre PC sans Docker

PostgreSQL stocke les donnees localement. Git transporte uniquement une copie
chiffree `snapshot.enc`, incluant la base et les documents de `uploads/`.
Cette copie remplacee avant chaque push n'est pas une synchronisation des PC.
Utiliser un depot prive. Garder le mot de passe de sauvegarde dans un gestionnaire
de mots de passe, jamais dans Git. Sans ce mot de passe, aucune restauration.

## Preparation (sur les deux PC)

Installer Python 3.10+ et PostgreSQL 17 (ou une version compatible plus recente).
Depuis la racine du depot :

```powershell
python -m pip install -r scripts/requirements-snapshot.txt
```

## Avant chaque push

Arreter l'application pour que les documents et la base ne changent pas pendant
la sauvegarde. Laisser le service PostgreSQL demarre.

```powershell
python scripts/snapshot.py backup --database convertbank
git add data/transfer/snapshot.enc
git commit -m "Update encrypted data snapshot"
git push
```

Le script demande un mot de passe de chiffrement (16 caracteres minimum), puis
le mot de passe PostgreSQL. Aucun mot de passe n'est ajoute a la sauvegarde.
Verifier que la sauvegarde a reussi avant le commit. Ajouter les changements
de code separement. Au-dela de 95 Mio, le script refuse la sauvegarde GitHub :
utiliser alors un stockage prive dedie. Les anciennes versions restent dans Git.

## Apres le clone sur l'autre PC

Arreter l'application. Choisir un nom de base qui n'existe PAS encore :

```powershell
python scripts/snapshot.py restore --database convertbank_import --uploads uploads_import
```

Le script demande le mot de passe de la sauvegarde et celui du PostgreSQL de ce
PC. Il cree la nouvelle base et restaure les documents. Il refuse toute base
existante et tout dossier de documents non vide. Il ne supprime aucune base.
En cas d'echec, la nouvelle base reste disponible pour diagnostic.

Configurer ensuite les valeurs locales dans
`backend/BankStatementConverter.API/appsettings.Development.json` (ignore par Git) :
`ConnectionStrings:Default` doit viser la nouvelle base sur localhost avec les
identifiants de ce PC, et `Storage:Root` le chemin absolu de `uploads_import`.
Utiliser une nouvelle cle `Jwt:Key` d'au moins 32 octets et `Seed:DemoData=false`.
Les utilisateurs et leurs mots de passe applicatifs sont ceux de la sauvegarde.
Suivre le lancement local du README principal.

Options : `--host`, `--port`, `--user`, `--pg-bin`, `--file`, `--uploads`.
Sous Windows, le chemin PostgreSQL 17 standard est utilise par defaut.

## Anciennes sauvegardes non chiffrees

`data/snapshot/convertbank.dump` et `data/snapshot/files/` sont maintenant ignores.
Si ces fichiers sont deja suivis par Git, `.gitignore` ne suffit pas : retirer
leur suivi avec `git rm --cached` (et `-r` pour le dossier), puis committer.
Cela ne les efface PAS de l'historique Git. Si des donnees sensibles ont deja
ete publiees, traiter cette exposition avant tout nouveau partage du depot.
