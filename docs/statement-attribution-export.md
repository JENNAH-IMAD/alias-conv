# Traçabilité des relevés et enregistrement des exports

Les listes de relevés, le parcours de traitement, l'historique et les conversions récentes affichent la banque, le nom du compte, l'auteur du dernier traitement réussi et l'auteur de la dernière modification enregistrée. L'historique et les fiches détaillées incluent les dates.

Les identifiants et noms sont obtenus depuis l'utilisateur authentifié côté serveur, jamais depuis un champ fourni par le navigateur. Les noms sont conservés comme ils étaient lors de l'action. Les colonnes nullable `ProcessedByUserId`, `ProcessedByName`, `ProcessedAt`, `ModifiedByUserId`, `ModifiedByName` et `ModifiedAt` sont ajoutées à `History` par la migration `StatementAttribution`. Les anciens relevés affichent « Non renseigné » ; aucune attribution n'est inventée.

La modification d'un relevé remet le dossier en attente mais conserve la trace du dernier traitement réussi. Les corrections du tableau demeurent locales comme auparavant : leur export ne remplace pas le relevé archivé et ne constitue pas une modification enregistrée du dossier.

## Exports

- Les nouveaux exports reçoivent un nom `Banque_Compte_Date_Identifiant.ext` ; les clés internes de stockage restent indépendantes du nom de téléchargement.
- Le bouton de téléchargement ouvre une boîte permettant de modifier le nom du fichier, sans changer l'extension ni le contenu.
- « Choisir le dossier et enregistrer » utilise le sélecteur natif du navigateur compatible. Une annulation ne lance aucun téléchargement.
- « Télécharger » utilise les paramètres de téléchargement du navigateur. L'application indique que le téléchargement est lancé et ne prétend pas connaître le chemin complet.
- Le nom modifié concerne la copie enregistrée sur l'ordinateur ; la copie archivée dans l'application est conservée.
- Le même choix est proposé pour les fichiers Excel, TXT, CSV, PDF original et CSV corrigé.

La disponibilité du sélecteur dépend du navigateur et du contexte sécurisé : [documentation showSaveFilePicker](https://developer.mozilla.org/en-US/docs/Web/API/Window/showSaveFilePicker).

## Validation et activation

Migration appliquée à la base locale. Tests API avec deux utilisateurs distincts, nommage des exports et téléchargement sans en-tête réussis. Tests navigateur : affichage des auteurs, nom personnalisé, refus des chemins invalides, annulation et écriture dans un fichier choisi (sélecteur simulé), conservation exacte du contenu. Régression des trois parcours pages/traitement réussie.

Redémarrer l'API depuis son terminal pour charger le nouveau code. Depuis la racine du projet :

```powershell
dotnet run --no-build --configuration Release --project backend/BankStatementConverter.API --urls http://localhost:5081
```

Arrêter d'abord l'ancienne instance avec Ctrl+C dans son terminal.
