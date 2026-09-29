# Copie des donnees du 26 septembre 2026

`convertbank.dump` contient le schema PostgreSQL et toutes les lignes de la base actuelle, y compris les utilisateurs (mots de passe haches), clients, comptes, logos, photos, profils, modeles et historiques. `files/` contient les PDF et exports du stockage local. Les secrets de configuration `.env`, JWT et PostgreSQL ne sont pas inclus.

## Sur un autre PC

Installer Git et PostgreSQL local (restauration verifiee avec PostgreSQL 17), puis :

```sh
git clone --branch version-4 https://github.com/JENNAH-IMAD/Alias-ConvertPDF.git
cd Alias-ConvertPDF
```

Docker Compose ne contient plus de service PostgreSQL ni de restauration automatique. Les donnees sont stockees dans PostgreSQL sur le PC.

Il s'agit d'une photographie des donnees a la date indiquee, pas d'une synchronisation entre PC. Ne pas restaurer sur une base contenant deja des donnees. Sauvegarder toute base existante avant intervention.

## Restauration locale

Creer une base vide puis restaurer (remplacer les noms d'utilisateur/base selon votre installation). Si une base vide existe deja, ignorer `createdb` :

```sh
createdb -h localhost -U postgres convertbank
pg_restore -h localhost -U postgres -d convertbank --no-owner --no-acl --exit-on-error --single-transaction data/snapshot/convertbank.dump
```

Copier le contenu de `data/snapshot/files/` dans le dossier `uploads/` a la racine du projet. Configurer `Storage__Root` avec le chemin absolu de ce dossier et `ConnectionStrings__Default` avec la connexion a la base restauree. Suivre ensuite le lancement local du README principal, avec `Seed__DemoData=false` et sans configurer de nouvel administrateur. Utiliser une nouvelle cle JWT.
