# Analyse et maintenance du projet Alias

Note de mise à jour : les suites de tests automatisés et leurs configurations ont été retirées pour une vérification manuelle. Les bilans de tests ci-dessous sont historiques.

Analyse du 16 octobre 2026, réalisée sur la copie de travail existante, avec ses
modifications non validées dans Git. Les résultats ci-dessous concernent cette copie.

## Architecture

La séparation actuelle est adaptée au produit et mérite d'être conservée :

| Dossier | Responsabilité |
| --- | --- |
| `backend/BankStatementConverter.Domain` | Entités, relevés et règles communes sur les montants |
| `backend/BankStatementConverter.Application` | Contrats, permissions et interfaces d'extraction/export |
| `backend/BankStatementConverter.Infrastructure` | PostgreSQL, stockage, OCR, normalisation et génération des exports |
| `backend/BankStatementConverter.API` | HTTP, authentification, autorisations et assemblage des services |
| `frontend/src/app` | Routes Next.js, layout et styles de base |
| `frontend/src/components` | Composants de l'interface, dont les primitives dans `ui` |
| `frontend/src/lib` | Fonctions réutilisables et logique d'édition côté navigateur |
| `frontend/src/services` | Accès API, contrats TypeScript et adaptation des relevés |
| `frontend/src/styles` | Styles partagés et adaptations des différents écrans |
| `services/ocr` | Service Python de reconnaissance, indépendant des règles bancaires |
| `tools`, `scripts`, `installer` | Utilitaires, lancement, fabrication et installation |

Les migrations EF Core, fichiers golden, ressources PDF/EMA, polices et licences
ont une utilité réelle. Leur absence d'import TypeScript ne justifie pas leur suppression.
Les dépendances de production du frontend ont toutes un usage identifié dans le code
ou les imports CSS ; aucune n'a été retirée arbitrairement.

## Nettoyage réalisé

Sept fichiers sans import entrant ont été retirés après recherche de leurs références :

- `frontend/src/components/client-statement-viewer.tsx`
- `frontend/src/components/dashboard-conversion-resources.tsx`
- `frontend/src/components/extraction-profiles.tsx`
- `frontend/src/components/statement-extraction.tsx`
- `frontend/src/components/statement-transaction-editor.tsx`
- `frontend/src/components/paddle-document-reading.tsx`
- `frontend/src/components/ui/table.tsx`

Une seconde analyse des imports TypeScript ne trouve plus de module isolé hors des
points d'entrée conventionnels de Next.js. Cette analyse concerne les fichiers entiers,
pas une preuve d'utilisation de chaque export ni de chaque sélecteur CSS.
Les API de profils et d'extraction restent utilisées par les services et sont conservées.

Le démarrage de l'API est réparti entre :

- `Program.cs`, qui expose l'ordre de démarrage et des middlewares ;
- `ServiceRegistration.cs`, qui regroupe les services HTTP, de sécurité, de catalogue et de conversion ;
- `ApplicationSetup.cs`, qui gère les erreurs HTTP, la protection des écritures et l'initialisation de la base.

Les durées de vie des services, l'ordre des middlewares, les règles d'accès et les
options OCR existantes ont été conservés. Les commentaires expliquent notamment la
persistance des clés, la protection des écritures et la gestion des clients OCR.

Les diagnostics du relevé sont isolés dans `statement-diagnostics.tsx`. Un état React
jamais lu et les branches rendues inaccessibles par `allowExportErrors = true` ont été
supprimés. Le téléchargement conserve son comportement existant « avec erreurs ».

Quatorze feuilles de styles sont regroupées dans `src/styles`, en conservant leur
ordre d'import pour préserver la cascade. `globals.css` reste le point d'entrée de
Tailwind et du thème de base. Aucun sélecteur n'a été supprimé sur une simple intuition.

Le formatage C# utilise quatre espaces et développe les blocs/instructions auparavant
compactés sur une ligne. Les migrations générées sont exclues de ce passage.
Les projets XML sont indentés, et le frontend suit sa configuration Prettier existante.
La casse du dossier `Fixtures` dans le projet de tests a été corrigée pour sa copie
sur les systèmes sensibles à la casse.

ESLint exclut désormais `.next-runtime` et les ressources PDF.js générées : le premier
contrôle analysait ces fichiers tiers et remontait 13 713 diagnostics. Les caches,
rapports de tests, installations locales et sauvegardes sont mieux exclus de Git
et/ou du contexte Docker. Les données locales existantes ont été conservées.

Les liens de documentation pointant vers des fichiers absents ont été corrigés.
`scripts/Test-Project.ps1` regroupe les contrôles reproductibles et s'arrête au premier
échec. Il ne masque pas les échecs déjà présents.

## Vérifications et limites

| Contrôle | Résultat observé |
| --- | --- |
| Compilation de la solution .NET | Réussie, sans avertissement ni erreur |
| Types TypeScript | Réussite |
| ESLint avec `--max-warnings=0` | Réussite après correction des exclusions |
| Prettier | Réussite |
| Compilation de production Next.js | Réussite |
| Tests .NET hors intégration | 213 réussites, 70 échecs avant et après nettoyage ; mêmes noms de tests en échec |
| Sélection de tests navigateur | 6 réussites sur 7 |
| Démarrage HTTP de l'API sans base | Démarrage réussi en développement ; réponses 503 pour la santé, 401 sans session et 400 sans en-tête d'écriture |

Les six parcours navigateur réussis couvrent l'édition des mouvements, les tableaux
sur ordinateur/mobile, l'édition des utilisateurs, les détails d'historique, l'historique
vide et le dashboard sombre/mobile.
Le test navigateur restant attend un bouton « Télécharger » et une case d'activation
qui ne correspondent plus au parcours existant « Télécharger avec erreurs ».
Le téléchargement actuel redirige également vers l'espace client : les assertions
historiques de ce parcours doivent être réexaminées ensemble.

Les tests d'intégration PostgreSQL, les appels OCR réels et la fabrication complète
des installateurs n'ont pas été exécutés. Les tests navigateur utilisent des réponses
API simulées. Ils ne valident pas une conversion bancaire réelle de bout en bout.

## Points restant à traiter, par priorité

1. **Rendre les tests bancaires fiables.** Parmi les 70 échecs initiaux, 60 signalent
   l'absence des PDF attendus dans un dossier racine commençant par `relv`. Les dix
   autres concernent les signes des soldes Sage X3, la représentation de zéro, les
   fichiers golden, les métadonnées ou les limites de l'aperçu. Il faut confronter les
   attentes au contrat métier avant de modifier les montants, signes ou références.
   Le nettoyage ne change pas ces règles pour faire artificiellement passer les tests.
2. **Actualiser les scénarios navigateur obsolètes.** La suite contient 100 tests ;
   seule une sélection de sept a été exécutée pendant ce nettoyage. L'ancien parcours
   de téléchargement ne représente plus l'interface actuelle.
3. **Poursuivre le découpage de `statement-workspace.tsx`.** Après extraction des
   diagnostics, ce composant reste volumineux et gère encore l'édition, la sélection,
   l'aperçu et le téléchargement. Séparer ces responsabilités par étapes avec des
   scénarios de non-régression dédiés.
4. **Réduire les superpositions CSS.** Le regroupement rend les styles plus faciles à
   retrouver, mais les feuilles de retouches se superposent encore. Une consolidation
   nécessite des comparaisons visuelles des états clair/sombre, mobile et édition.
5. **Aligner les outils de livraison sur les contrôles communs.** La solution contient
   `HistoryMaintenance`, tandis que les autres outils sont construits par les scripts
   d'installation. Leur compilation et leurs tests d'installation doivent faire partie
   d'une validation de livraison distincte.

## Conventions pour les prochaines modifications

- Garder les contrôleurs centrés sur HTTP et l'autorisation ; placer la logique de
  conversion et de persistance dans les services existants.
- Utiliser des noms qui expriment l'intention et extraire une responsabilité cohérente
  quand une fonction ou un composant devient difficile à parcourir.
- Commenter les raisons, contraintes comptables et cas ambigus, sans paraphraser le code.
- Préserver la précision décimale, les signes, les formats d'encodage et les octets
  des exports ; vérifier leurs tests métier avant toute simplification.
- Vérifier les imports, les points d'entrée des frameworks, les scripts de génération
  et les ressources chargées par chemin avant de supprimer un fichier.
- Exclure les résultats générés des outils de qualité et garder les secrets/configurations
  propres au poste hors du dépôt.
- Compiler la solution, contrôler les types TypeScript et vérifier manuellement les conversions avec des PDF réels avant livraison.
