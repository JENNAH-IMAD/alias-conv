# Thème Alias Convert PDF

Le thème est intégré au frontend existant `frontend/Import-pdf-excel-master`.

- Palette clair/sombre dans `src/app/globals.css`, avec couleurs lisibles distinctes pour les textes d'état et les liens.
- Logos originaux importés depuis `assets/white mode logo.png` et `assets/dark mode logo.png` par `BrandLogo`.
- `next-themes` conserve la clé existante `bank-converter-theme` et ajoute le choix système.
- Le bouton sous le logo replie le menu en une barre de 88 px ; un second clic rétablit le menu standard. Préférence conservée sous `alias-sidebar`.
- Sur téléphone et tablette, ouvrir le menu depuis la barre supérieure ; le tiroir propose également les deux présentations et se ferme avec Échap ou après navigation.
- Navigation et contenu conservent chacun leur défilement indépendant.

Lancement habituel : `npm run dev`, sur http://localhost:3002.

Vérifications : `npm run lint`, `npm run build`, puis `npx playwright test tests/alias-theme.spec.ts tests/frontend.spec.ts --grep "Alias :|pages principales"`.
Les tests navigateur utilisent des données simulées ; ils ne modifient pas les comptes ou les relevés réels.
