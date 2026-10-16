# Interface Alias

Gestion des clients, banques, comptes bancaires, documents PDF originaux et modèles d’exportation CSV/Sage.
Le parcours `/conversions` ajoute l’import PDF de relevés bancaires, la correction,
la validation, l’aperçu et le téléchargement CSV/TXT avec historique.
Le tableau des mouvements permet l’ajout, la suppression et la correction des
lignes, ainsi que le remplacement collectif d’une colonne avec aperçu avant/après.
L’espace clients affiche tous les relevés conservés, y compris les imports à
classer, avec filtres, recherche et consultation directe des mouvements et exports.
L’aperçu propose le texte complet du fichier, modifiable avant téléchargement,
et une option explicite pour télécharger avec erreurs sans valider le relevé.
Next.js 16, React 19, TypeScript et Tailwind CSS 4.

Le sidebar propose une vue standard, une vue compacte et un menu mobile.
Les pages partagent le même style clair/sombre et des animations qui respectent
la préférence « réduire les animations ». Le traitement PDF affiche les étapes
réelles, les pages lues et le temps écoulé. Le téléchargement prépare automatiquement
son aperçu après validation du relevé.

```powershell
npm ci
npm run dev
```

L’interface est disponible sur `http://localhost:3002`. Le proxy `/api` utilise
`http://127.0.0.1:5081`, configurable avec `API_PROXY_URL`. Les polices sont locales.

```powershell
npm run typecheck
npm run lint
npm run build
npm run test:e2e
```

`npm start` lance la compilation de production existante.
Voir [le README principal](../README.md) pour la base de données et le lancement de l’API.
Voir [les conventions du projet](../docs/qualite-projet.md) pour l'organisation du code et les vérifications.
