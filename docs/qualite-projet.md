# Architecture et maintenance d’Alias

Analyse de la copie de travail du 21 octobre 2026. Les modifications existantes
ont été conservées ; aucun historique Git ni donnée bancaire n’a été réinitialisé.

## Organisation

| Dossier | Responsabilité |
| --- | --- |
| `backend/BankStatementConverter.Domain` | Entités, relevés, utilisateurs et règles communes sur les montants |
| `backend/BankStatementConverter.Application` | Contrats HTTP/métier, permissions et interfaces |
| `backend/BankStatementConverter.Infrastructure` | PostgreSQL, stockage, extraction, contrôles et exportation |
| `backend/BankStatementConverter.API` | Contrôleurs, authentification et assemblage des services |
| `frontend/src/app` | Routes Next.js et points d’entrée |
| `frontend/src/components` | Interface, édition et primitives partagées |
| `frontend/src/lib` | Logique d’édition et fonctions réutilisables |
| `frontend/src/services` | Accès API et contrats TypeScript |
| `frontend/src/styles` | Styles et adaptations des écrans |
| `services/ocr` | Lecture locale Python et reconstruction des tableaux |
| `tools`, `scripts`, `installer` | Maintenance, lancement et livraison |

La chaîne de traitement locale sépare la reconnaissance du document, son
interprétation bancaire et la génération du fichier comptable. PaddleOCR
AI Studio reste un moteur facultatif. Les clés, poids OCR et paramètres
propres au poste ne font pas partie des sources à nettoyer.

## Audit et nettoyage

- Le graphe des imports frontend ne révèle aucun module source isolé hors des
  points d’entrée Next.js. Les exports TypeScript examinés ont des références.
- Chaque dépendance frontend de production a un usage dans les imports ou CSS.
- Les types C# à occurrence unique sont des contrôleurs découverts par ASP.NET,
  des points d’entrée, des extensions de démarrage ou la fabrique EF Core.
  Ils ne sont pas du code mort.
- Soixante-huit sélecteurs CSS de l’ancienne fiche bancaire et des anciennes
  cartes de lancement ont été supprimés après recherche dans les sources.
  Les sélecteurs partagés encore utilisés sont conservés.
- Le formatage C# a été harmonisé sans modifier les migrations générées.
- La configuration `Storage:MaxBytes`, qui n’était plus lue, a été retirée.
  Les limites propres aux photos et logos restent pertinentes et actives.
- Le lanceur desktop WinForms ne charge plus la référence WPF ajoutée par
  WebView2, qui provoquait un conflit de versions de `WindowsBase`. Les références
  Core et WinForms du navigateur sont conservées.
- Les anciens diagnostics, outils ponctuels de revue, fichiers générés et archives
  d’installation Ollama sont pris en charge par le nettoyage reproductible.
- Le README et les guides OCR ont été alignés sur la chaîne actuelle. Les anciens
  résultats de tests et les références à des scripts disparus ont été retirés.

Les migrations EF Core, les polices et leurs licences, les ressources PDF/EMA,
les logos, les modèles d’exportation et les quatre projets `tools` sont utiles.
Leur absence d’import TypeScript ne justifie pas leur suppression.

## Contrôles reproductibles

Depuis la racine, sous Windows :

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Check-Project.ps1 -Production
```

Le script s’arrête au premier échec. Il vérifie la compilation de la solution .NET,
de `ProjectSnapshot`, `DesktopApp` et `ServerManager`, le formatage C#, la syntaxe
PowerShell, les types TypeScript, ESLint sans avertissements, Prettier et, avec
`-Production`, la compilation Next.js. Les artefacts .NET sont isolés dans
`.local/check-project` pour ne pas remplacer les fichiers d’un serveur actif.
Le build Next.js utilise `.next-runtime`.

La syntaxe Python a également été vérifiée lors de cet audit. La revue visuelle
du parcours de conversion couvre ordinateur, tablette et mobile, les thèmes clair
et sombre, le PDF original/converti, le texte source et les formulaires de correction.
Cette revue utilise des réponses API contrôlées issues d’un relevé existant ; elle
ne remplace pas une conversion réelle de bout en bout.

Les anciennes suites de tests automatisés sont retirées, conformément au choix
du projet. La vérification métier se fait manuellement avec les documents originaux.
Cet audit ne valide ni tous les formats bancaires, ni tous les fournisseurs OCR,
ni l’installation complète des livrables Windows/Linux.

## Nettoyage reproductible

```powershell
# Consulter les cibles, sans suppression.
powershell -ExecutionPolicy Bypass -File scripts/Cleanup-GeneratedFiles.ps1
# Supprimer les cibles vérifiées.
powershell -ExecutionPolicy Bypass -File scripts/Cleanup-GeneratedFiles.ps1 -Execute
```

Le script ne dépend plus d’un manifeste temporaire. Il vérifie les chemins
absolus, reste dans le dépôt, conserve les fichiers suivis par Git, les dossiers
utilisés par un processus et ceux contenant une jonction ou un lien symbolique.
Les erreurs de suppression sont signalées, sans arrêter les services.

Les dossiers `.local`, `output`, `tmp` et `tools` ne doivent pas être supprimés
en bloc :

- `.local` contient notamment Python, les modèles OCR et des sauvegardes ;
- `output/runtime` est utilisé par les lanceurs serveur et desktop ;
- `output/installers` contient les livrables et les snapshots peuvent contenir des données ;
- `tmp` contient encore des sauvegardes et des documents de référence ;
- `tools` contient les programmes de livraison et de maintenance.

Le passage du 21 octobre a supprimé 32 cibles, soit 1 460,9 Mio de fichiers.
Après nettoyage, les endpoints de santé de l’API et du service OCR, ainsi que
la route de session de l’interface, répondent HTTP 200. La compilation .NET et
des outils se termine sans erreur ni avertissement ; TypeScript, ESLint,
Prettier et la compilation de production Next.js réussissent.

## Conventions et suite de la maintenance

Les règles comptables, signes, décimales, encodages et migrations ne doivent pas
être simplifiés sur la seule base d’une recherche de texte. Garder la logique
HTTP dans les contrôleurs, la conversion dans les services et les paramètres
locaux hors de Git. Vérifier les références par chemin et les découvertes par
framework avant de supprimer un fichier.

`statement-workspace.tsx` reste volumineux. Son découpage et une consolidation
plus large des styles méritent des changements dédiés, accompagnés d’une revue
des états d’édition, des exports et des écrans clair/sombre. La suppression des
anciens sélecteurs ne constitue pas une preuve que chaque sélecteur restant est utilisé.
Les dépendances n’ont pas été mises à niveau arbitrairement pendant ce nettoyage.


### Filtrage des conversions

Dans « Mouvements & filtres », la grille reprend les colonnes ordonnées du modèle d’exportation actif. Chaque colonne possède une recherche sur sa valeur formatée. Les recherches se combinent par ET, sans distinction de casse ou d’accents, sur toutes les lignes avant pagination. Les soldes d’ouverture et de clôture du modèle ne sont pas des mouvements sélectionnables.

« Enregistrer la sélection » transmet les filtres de colonnes et l’empreinte du modèle au serveur. Celui-ci refait le filtrage avec le moteur d’exportation, conserve les mouvements retenus et leurs totaux décimaux. L’aperçu, le PDF de la sélection et le téléchargement utilisent cette sélection sauvegardée. Une modification du modèle depuis la lecture des colonnes entraîne un conflit explicite avant sauvegarde. « Tout afficher » revient au document complet sans supprimer les sauvegardes.

Les anciens extraits restent lisibles. Leur ancien filtrage par libellé n’est plus présenté dans l’interface. Les corrections des valeurs source sont accessibles dans une section repliable ; elles doivent être enregistrées avant de filtrer.


### Journal d’activité

La page Historique sépare les actions sur les relevés et les modèles d’extraction dans deux onglets. Les événements sont regroupés par date, les détails restent repliés et les suppressions sont réservées aux administrateurs avec confirmation. Les dates affichées et les bornes du filtre de période utilisent UTC.

La recherche (fichier, client, banque, compte ou auteur), le type d’action et les dates s’appliquent côté serveur avant le comptage et la pagination. `HistoryActivityQuery` est partagé par la lecture et la suppression d’une sélection afin de conserver le même périmètre, y compris sur toutes les pages. La suppression d’une action conserve les relevés, PDF, modèles et fichiers exportés.

Le service de lecture renvoie `filterVersion: 1`. Si le frontend rencontre un ancien backend, il bloque l’affichage et la sélection des résultats filtrés plutôt que de présenter une liste non filtrée. Les suppressions filtrées utilisent la route distincte `delete-filtered-selection` : un ancien backend ne peut pas les exécuter en ignorant les nouveaux critères.
