# Interface SaaS : thèmes, actions et performances

## Palette

| Usage | Clair | Sombre |
| --- | --- | --- |
| Fond | #F6F8FB | #101722 |
| Carte | #FFFFFF | #182231 |
| Surface secondaire | #F0F3F8 | #202D3F |
| Texte | #172B4D | #E7EDF5 |
| Texte secondaire | #596B83 | #A8B7CB |
| Bordure | #DFE5EE | #2C3C52 |
| Action principale | #146BC4 | #76B7FF |
| Suppression | #C52F3D | #FB7185 |
| Succès | #15803D | #4ADE80 |

Toutes les couleurs sont définies dans globals.css. Les actions principales utilisent du bleu uni, les modifications un fond bleu discret et les suppressions du rouge. Les fonds des cartes fichiers sont neutres. Les composants partagés diffusent le thème aux pages existantes.

## Actions des fichiers

Consulter ouvre l'aperçu dans la même page et déplace immédiatement la vue et le focus dessus. La carte affiche « Aperçu ouvert ». Une erreur propose Réessayer. Télécharger et Excel ouvrent la boîte de nommage/enregistrement ; les permissions restent contrôlées.

Les tests navigateur utilisent des fichiers simulés. L'accès aux fichiers réels d'une session utilisateur n'a pas été vérifié : aucun identifiant local valide n'était disponible pour ces contrôles. Une erreur de fichier absent ou de permission côté API demeure affichée et ne doit pas être confondue avec un bouton inactif.

## Optimisations

- Chargement du panneau de traitement et de l'analyse à la demande.
- Affichage initial limité à 20 relevés ; bouton pour afficher les suivants. Cette limite réduit le rendu, pas le volume renvoyé par l'API.
- Annulation des requêtes devenues inutiles lors de changements de pages ou filtres.
- Logos PNG optimisés par Next Image, remplaçant le téléchargement des grands SVG contenant des PNG encodés.

Mesure Chrome locale sur la page de connexion de la version production : 8791 ms au premier chargement, puis 215 ms et 166 ms. Ce contrôle inclut la navigation jusqu'au bouton de connexion visible ; ce n'est ni un score Lighthouse ni une mesure du temps de conversion PDF.

## Lancement production

Dans frontend/Import-pdf-excel-master :

```powershell
npm run build
npm start
```

Arrêter d'abord le serveur de développement utilisant le port 3002. La version compilée évite la compilation des routes à la demande du serveur de développement. Un aperçu production a également été lancé sur le port 3005 pour les vérifications.
