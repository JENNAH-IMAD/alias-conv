# Frontend Bank Statement Converter

## Utilisation locale sans compilation pendant la navigation

Garder le backend actif sur le port 5081. Depuis ce dossier :

```powershell
npm.cmd run build
npm.cmd start
```

Ou compiler puis demarrer en une commande : `npm.cmd run serve`.
Arreter d'abord le serveur de developpement avec Ctrl+C pour liberer le port 3002.
Pour les lancements suivants sans modification, `npm.cmd start` suffit.

### Mesurer les temps de reponse

Dans un autre terminal, serveur actif :

```powershell
npm.cmd run perf:local
# Comparer avec un autre port :
npm.cmd run perf:local -- http://localhost:3005
```

La mesure distingue le premier appel et la mediane de cinq appels suivants.
Elle mesure la reponse HTTP complete, pas le rendu navigateur ni le ping reseau.
Le message `Slow filesystem detected` concerne les acces disque pendant le
developpement. Le mode production evite la compilation des pages pendant la
navigation ; il ne repare pas les performances du disque. Ne pas supprimer
`.next` a chaque lancement : cela force une nouvelle compilation.

Ouvrir http://localhost:3002. Arreter `npm run dev` avec Ctrl+C avant
`npm start` pour liberer le port. Le lancement copie les fichiers statiques dans
le serveur standalone genere par Next.js. Refaire `npm run build` apres une
modification du code ou de `NEXT_PUBLIC_API_URL`.

`npm run dev` reste disponible pour modifier le code avec rechargement automatique ;
il compile les pages a la demande et ne mesure pas les performances de production.

Frontend existant Next.js 16 / React 19 / TypeScript / Tailwind 4, relié à l’API ASP.NET Core.

Depuis ce dossier : copier `.env.example` vers `.env.local`, puis `npm ci` et `npm run dev`.

API par défaut : http://localhost:5081/api. Variable publique : `NEXT_PUBLIC_API_URL`. Aucun secret backend ne doit être ajouté ici.

Vérifications : `npm run lint` et `npm run build`. La compilation télécharge les polices Google Inter et Manrope déjà utilisées par l’interface.

Voir [le README principal](../../README.md) pour PostgreSQL, les migrations, les rôles, Docker et la conversion de démonstration.
