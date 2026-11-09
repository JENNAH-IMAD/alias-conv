# Analyse du frontend, du design et des animations

## Périmètre et méthode

Audit du frontend Next.js, de ses pages, composants, styles globaux et modules CSS. Le serveur Next.js sur le port 3002 et l’API sur le port 5081 étaient déjà démarrés. La page de connexion a été consultée en direct. Le navigateur d’audit ne disposait pas d’une session utilisateur authentifiée : les vues métier ont été rendues avec des réponses API de démonstration interceptées uniquement dans ce navigateur. Aucun compte, document ou paramètre de l’application n’a été modifié.

Huit vues ont été examinées à 1440 × 960 et 390 × 844 : dashboard, conversion, espace clients, banques, comptes bancaires, modèles d’exportation, modèles d’écritures et historique. Aucun débordement horizontal global n’a été mesuré dans ces états. Les formulaires remplis, tableaux volumineux, résultats OCR et réglages administrateur nécessitent des essais supplémentaires avec une session authentifiée. Les captures ont été stabilisées en désactivant les animations pendant leur prise ; leurs états animés ont été examinés séparément par les styles calculés.

## Architecture et parcours

- **Socle** : Next.js 16.3.8, React/React DOM 19.2.8, TypeScript, App Router.
- **Présentation** : Tailwind CSS 4, CSS global et CSS Modules. `clsx` et `tailwind-merge` composent les classes ; `class-variance-authority` définit les variantes des boutons.
- **Composants** : composants locaux de style shadcn/ui. Le paquet Radix UI sert notamment au mécanisme `Slot` des boutons ; ce n’est pas une généralisation des Select/Dialog Radix à toute l’interface.
- **Icônes** : Lucide React, SVG.
- **Thèmes** : `next-themes`, attribut `data-theme`, préférence système et stockage local `bank-converter-theme`.
- **PDF** : PDF.js (`pdfjs-dist`), canvas, couche de texte et éléments de sélection.

Le layout racine fournit le thème, l’authentification, le contexte de transition liste/détail et le shell commun. Le shell porte la sidebar, le bandeau supérieur, le fil d’Ariane, les actions globales et le contenu de la route. Les droits déterminent les liens et les actions disponibles.

Les pages principales sont `/dashboard`, `/client-workspace`, `/conversions`, `/entry-models`, `/clients`, `/banks`, `/bank-accounts`, `/templates`, `/history`, `/users`, `/settings` et `/login`. `/entry-model-results` est aussi une route technique disponible ; l’analyse du parcours des écritures doit donc distinguer cette route des résultats intégrés à la conversion.

Le parcours central est : import d’un relevé ou extrait, contexte bancaire/client, choix du format et éventuellement de l’écriture du client, vérification/filtrage puis export. Les vues de résultat réutilisent les derniers paramètres enregistrés. Le PDF et les données utilisent leurs propres composants ; l’animation de navigation n’attend pas explicitement la fin d’un traitement OCR.

## Système visuel actuel

Le langage visuel est cohérent dans son intention : fond clair bleuté ou bleu nuit, cartes, bordures fines, accent bleu, états vert/ambre/rouge, titres forts et textes secondaires atténués. Les variables de couleur sont définies dans `src/app/globals.css` et utilisées par les composants partagés.

Le thème clair définit notamment `--primary: #1263d8`, `--background: #f1f5fb` et des surfaces blanches. Le thème sombre utilise `--background: #09111f`, `--surface: #111e30` et `--primary: #78bfff`. Les écrans ajoutent parfois leurs propres accents ou dégradés, notamment le dashboard et les cartes bancaires.

Inter et Manrope sont chargées localement via `next/font/local`, sans requête vers Google Fonts. Les titres des en-têtes utilisent Manrope, un poids de 750, un tracking de −0,04 em et une taille adaptative de 28 à 36 px. Les contrôles partagés utilisent Inter, souvent 13 px ; plusieurs libellés secondaires descendent à 9–11 px.

`PageHeader` et son module CSS centralisent les en-têtes : grille texte/actions, illustration décorative, fond thématique et hauteur minimale de 196 px sur grand écran. Les rayons ne sont pas tous identiques : boutons/champs autour de 8 px, panneaux de 14 à 20 px, cartes du portefeuille jusqu’à 24 px. La grille et Flexbox gèrent le responsive.

L’espace clients et les catalogues possèdent des états de chargement, d’erreur et de liste vide. `LoadState` réutilise `Skeleton` avec `animate-pulse`. Les boîtes de dialogue du catalogue reposent sur l’élément HTML natif `<dialog>` et `showModal()`.

## Moyens employés pour les animations

Le projet ne dépend pas de Framer Motion/Motion, GSAP, Rive ou Lottie. Les animations observées sont produites par **CSS**, **SVG**, **React ViewTransition**, **les View Transitions natives du navigateur** et quelques interactions JavaScript. `tw-animate-css` est importé, mais la majorité des effets personnalisés est écrite directement dans les fichiers CSS.

### Navigation entre les pages

Sources : `components/app-shell.tsx`, `components/route-detail-transition.tsx`, `styles/page-transitions.css`.

- Le contenu de la route est entouré d’un `<ViewTransition>` dont la clé est `pathname`.
- Entrée : **230 ms**, `cubic-bezier(.22,.61,.36,1)`, opacité de 0 à 1, translation verticale de **8 px à 0**, échelle de **0,995 à 1**.
- Sortie : **140 ms**, `ease-in`, opacité de 1 à 0 et translation de **0 à −3 px**.
- La navigation latérale utilise aujourd’hui ce même effet : ce n’est donc pas uniquement un fondu.
- Le snapshot racine ancien est masqué ; le nouveau root n’est pas animé. Le but est de garder le document visible pendant le passage.
- Sidebar et topbar ont des noms stables propres et leurs snapshots ne sont pas animés.
- Des éléments flottants opt-in peuvent être isolés via `view-transition-name: match-element` lorsque le navigateur le permet.

Une instrumentation limitée au navigateur d’audit a confirmé **un appel à `document.startViewTransition`** en passant du dashboard à la conversion par le lien de navigation. Cela valide le fonctionnement sur le navigateur testé, sans garantir le même comportement sur tous les navigateurs.

### Liste vers détail et changement d’étape

Sources : `route-detail-transition.tsx`, `client-conversions.tsx`, `client-extracts.tsx`, `app/conversions/page.tsx`, `app/client-workspace/page.tsx`.

- Les identités partagées sont nommées `conversion-${id}` ou `extract-${extractId}`.
- La carte de liste et l’en-tête de détail partagent ce nom ; la classe de transition `workspace-detail` dure **220 ms**.
- Un contexte React mémorise seulement le titre et le sous-titre pour afficher une identité pendant le chargement. Les données métier sont chargées séparément.
- La conversion possède une transition `conversion-stage`, dont la clé dépend de l’import, du document et de l’étape.
- L’espace clients anime le changement relevés/extraits via `client-document-section`.
- `startTransition()` accompagne certaines mises à jour d’étape. Il marque une mise à jour React comme transition ; il ne définit pas lui-même une animation CSS.

### Sidebar et menu mobile

Sources : `sidebar-navigation.css`, `interface-design.css`, `app-shell.tsx`.

La largeur du menu, la marge du contenu, la taille et l’opacité du logo, les libellés et la rotation du chevron sont animés. La durée effective mesurée est **260 ms**, avec `cubic-bezier(.22,1,.36,1)`. Le fichier dédié déclare initialement 280 ms, mais `interface-design.css`, importé après, remplace cette variable par 260 ms.

Le menu mobile entre depuis la gauche. La règle effective définie plus tard est `drawer-enter`, **260 ms**. Le fond du tiroir possède également une apparition en opacité. Le tiroir est un élément `<dialog>` ; son ouverture et sa fermeture sont pilotées par React et l’API native du dialog.

### Les deux fichiers de la conversion

Source : `styles/conversion-studio.css`, classes `conversion-file-transfer`, `conversion-file-source` et `conversion-file-result`.

Il s’agit d’une animation CSS 3D de mélange/retournement de cartes, et non d’une animation Rive :

- cycle de **6,2 secondes**, répété indéfiniment ;
- perspective du parent : **1100 px** ;
- interpolation : `cubic-bezier(.45,.04,.2,.96)` ;
- deuxième carte décalée de **90 ms** ;
- déplacement `translate3d`, rotation `rotateY` et `rotateZ`, variation d’échelle ;
- changements de `z-index` pour suggérer le croisement des cartes ;
- dos de carte tourné à 180°, `backface-visibility: hidden` et `preserve-3d` ;
- reflet par pseudo-élément `::after` ;
- flèche centrale atténuée et décalée durant le croisement ;
- variation de fond et d’ombre pendant le mouvement.

Les deux noms de keyframes et leur durée de 6,2 s ont été vérifiés par `getComputedStyle` dans le navigateur.

### Dashboard

Sources : `components/dashboard-charts.tsx`, `styles/dashboard-overview.css`.

Les graphiques sont des **SVG personnalisés**, pas des composants Recharts/Chart.js. React calcule les tracés et les données survolées ; CSS produit leur apparition.

| Effet | Paramètres déclarés |
| --- | --- |
| En-tête du dashboard | `tessly-rise`, 460 ms |
| Blocs d’indicateurs/analyse | 540 ms, délai 140 ms |
| Cartes KPI | `dashboard-card-enter`, 420 ms, décalages 45/90/135 ms |
| Contenu secondaire | `dashboard-content-in`, 220 ms |
| Tracé des courbes | `stroke-dasharray`/`stroke-dashoffset`, 950 ms ; délais 120/240 ms |
| Aire sous la courbe | règle finale `dashboard-area-in`, 900 ms |
| Ligne de cible pointillée | `dashboard-target-draw`, 1,6 s, délai 240 ms |
| Anneau de validation | `dashboard-progress-ring`, 1,15 s, délai 120 ms |

Certaines déclarations précédentes du même sélecteur sont remplacées plus bas. Par exemple, l’aire du graphique annonce d’abord 700 ms puis 900 ms. L’anneau utilise `pathLength=100` et une variable CSS pour animer sa proportion. Les survols et sélections du graphique affichent les valeurs d’une journée.

### Portefeuille des comptes bancaires

Sources : `bank-account-wallet.tsx`, `bank-account-wallet.module.css`.

- Apparition du portefeuille : **800 ms**, opacité et translation horizontale de 36 px.
- Déplacement des cartes : **650 ms**, `cubic-bezier(.2,.75,.15,1)` ; fondu **500 ms**.
- Retournement : **700 ms**, `rotateY(180deg)`, `cubic-bezier(.45,0,.2,1)`.
- Apparition du panneau de détail : **380 ms**, opacité et translation de 14 px.
- Perspective : **1400 px**.
- Gestes : Pointer Events, capture du pointeur, détection à partir de 8 px et seuil de changement de carte de 18 % de l’espacement.
- Pendant le glissement, la transition CSS des cartes est coupée : leur position suit le geste. Au relâchement, le compte choisi est recalé avec la transition.
- Les flèches du clavier changent la sélection et la carte active est un bouton accessible au clavier.

### Connexion, fenêtres, historique et réglages

| Zone | Technique et durée |
| --- | --- |
| Connexion | Opacité + translation de 10 px, 550 ms ; entrée décalée des groupes de 60 à 320 ms |
| Illustrations de connexion | Flottement vertical de 3–4 px, cycles de 6 et 7 s |
| Dialogues | `dialog-enter`, 240 ms, translation de 12 px et échelle de 0,985 à 1 |
| Historique | `history-panel-in`, 180 ms, translation de 4 px |
| Configuration OCR | `settings-panel-in`, 220 ms, translation de 4 px |
| Boutons et contrôles | Couleur, bordure et ombre, généralement 150–180 ms |
| Squelettes | Classe Tailwind `animate-pulse` |
| Chargements | Icônes Lucide avec `animate-spin`, rotation CSS |

Les dialogues sont démontés à la fermeture : l’animation d’entrée ne constitue pas automatiquement une animation de sortie symétrique.

### Traitement OCR et PDF

Sources : `processing-progress.tsx`, `interface-design.css`, `conversion-experience.css`, `statement-pdf-viewer.tsx`.

La progression interroge le backend toutes les **1,5 seconde**. Ce polling n’est pas une animation ; il fournit l’état réel du traitement. Les changements visuels sont lissés par la transition de largeur de la barre (**450 ms**), un balayage indéterminé (**1,6 s**), un reflet (**2,8 s**) et un halo respirant (**2,4 s**).

`requestAnimationFrame` est utilisé pour regrouper certains changements de taille ou de position. Dans le lecteur PDF, il accompagne `ResizeObserver` pour recalculer la largeur disponible. Le zoom, le rendu canvas et les transformations PDF.js ne doivent pas être confondus avec une animation décorative de navigation.

## Accessibilité et réduction des mouvements

Le projet possède des protections globales et locales via `prefers-reduced-motion`. Elles arrêtent les animations, limitent les transitions, désactivent les View Transitions et fixent les graphiques dans leur état final. Dans le test navigateur, les deux fichiers passent bien à `animation-name: none` avec la préférence de réduction des mouvements.

Les noms de transition des détails sont liés aux identifiants, ce qui évite un nom partagé identique pour toutes les cartes d’une page. Le code comporte des labels ARIA, focus visibles, navigation clavier du portefeuille et squelettes annoncés avec `role=status`.

Limites constatées dans le code : `correctStatement()` dans `statement-workspace.tsx` demande directement `scrollIntoView({ behavior: 'smooth' })`, alors que le bouton d’analyse des écritures vérifie explicitement la préférence utilisateur. La gestion du mouvement réduit n’est donc pas uniforme pour les défilements JavaScript. Aucun audit automatisé complet des contrastes ou lecteur d’écran n’a été réalisé.

Le changement de thème utilise `disableTransitionOnChange` de next-themes : il coupe volontairement les transitions pendant la bascule. Il n’existe pas actuellement de fondu global dédié au changement clair/sombre.

## Diagnostic et améliorations recommandées

| Priorité | Constat vérifié ou risque identifié | Amélioration proposée |
| --- | --- | --- |
| Haute | 22 feuilles sous `src/styles`, environ 14 500 lignes, en plus de `globals.css` et des modules ; beaucoup de sélecteurs redéfinis | Réunir les variables dans une source, puis isoler les styles par composant et supprimer les règles remplacées après comparaison visuelle |
| Haute | Navigation latérale avec translation/échelle, malgré l’objectif initial de fondu simple | Donner une classe de transition spécifique à la navigation latérale ; conserver les mouvements pour l’ouverture d’un détail |
| Haute | Select natifs présents dans l’import, les modèles et les filtres | Standardiser un composant accessible commun pour uniformiser le menu, les états de focus, le chargement et les options longues |
| Moyenne | Microtextes de 9 à 11 px, particulièrement sur certaines cartes mobiles | Réserver ces tailles aux métadonnées secondaires ; utiliser 12–14 px pour les données et consignes nécessaires |
| Moyenne | La boucle des fichiers anime aussi `left`, `background` et `box-shadow` et garde plusieurs `will-change` | Déplacer le reflet avec `transform`, réduire les propriétés animées et mettre en pause les animations hors écran ou lorsque l’onglet est masqué |
| Moyenne | Superposition des animations de route, panneau et graphique | Définir une hiérarchie : navigation courte, une seule entrée du conteneur, dessin du graphique seulement au chargement pertinent |
| Moyenne | Défilement fluide JavaScript non homogène avec le mouvement réduit | Utiliser un helper qui choisit `auto` ou `smooth` selon `matchMedia` |
| Moyenne | Le portefeuille recalcule les cartes via un état React à chaque mouvement du pointeur | Mesurer avec beaucoup de comptes avant optimisation ; envisager un regroupement par frame ou une variable CSS directe si le coût devient visible |
| Basse | Plusieurs durées annoncées ne sont pas les valeurs finales de la cascade | Centraliser une échelle de motion : interaction 150 ms, navigation 200–230 ms, dialogue 220–240 ms, effets de données ciblés |

L’application dispose déjà d’un langage visuel commun et d’effets techniquement adaptés à un SaaS. La prochaine étape utile est de consolider les styles et de rendre les interactions plus prévisibles, plutôt que d’ajouter une nouvelle bibliothèque d’animation.

## Résultats de l’examen

- 14 fichiers `page.tsx` recensés, comprenant les routes d’entrée et routes techniques.
- 34 définitions `@keyframes` recensées dans `src` ; aucun nom de keyframes dupliqué trouvé par l’inventaire.
- Huit vues rendues dans les deux largeurs de test, sans débordement horizontal global dans les états de démonstration.
- Appel natif de transition confirmé sur une navigation client.
- Durée effective de la sidebar et cycle des fichiers vérifiés par styles calculés.
- Préférence de réduction des mouvements vérifiée pour les fichiers de conversion.
- Captures et scripts de contrôle conservés dans `.local/frontend-design-audit/`, hors des sources livrées.
- Aucun code applicatif modifié pendant cet audit ; seul ce rapport a été ajouté.
