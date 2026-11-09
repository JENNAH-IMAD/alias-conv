# Modèles d’écritures et résultats TXT

Les modèles d’écritures utilisent leur propre configuration `EntryModelOutput`. Ils ne dépendent pas d’un `ExportTemplate` Sage : `ExportTemplateId` est conservé à vide dans le contrat pour la compatibilité des anciens clients.

Les pages Journaux de saisie et Plans, taxes & comptes tiers sont retirées. Leurs anciennes tables `AccountingDossiers` et `AccountingPreparations` sont supprimées par la migration `RemoveJournalsAndPlansPreserveEntryModels`. Avant cette suppression, les modèles d’écritures sont transférés à l’identique dans `ClientEntryModelSets`, avec leur client et leur version. `EntryModelStore` assure ensuite leur lecture et leur enregistrement. Les formats, les fichiers et les résultats archivés restent dans leurs stockages indépendants.

**Modèles d’exportation → Écritures comptables** propose le format réutilisable « Écritures comptables — 5 colonnes », installé par la migration `AddEntryOutputTemplateCatalog`. Son catalogue indépendant (`EntryOutputTemplates`) permet de créer, modifier, dupliquer et supprimer des formats. Le champ **Format du résultat** d’une écriture enregistre son `OutputTemplateId`. Le traitement résout les réglages actuels de ce format ; les résultats archivés gardent un instantané des réglages utilisés. Une modification du format exige un nouvel aperçu avant l’export.

Les types disponibles sont TXT, CSV et TSV. Le séparateur, l’encodage UTF-8 ou Windows-1252, les décimales, les en-têtes et les colonnes sont configurables. Le fichier du résultat et son archivage utilisent l’extension sélectionnée.

Depuis la consultation d’un relevé, la liste **Modèle d’exportation** propose également ces formats dans le groupe **Écritures comptables — affichage et export**. Le choix ouvre le traitement des écritures avec ce format présélectionné. Après sélection de l’écriture du client, l’aperçu utilise ses colonnes d’affichage et le téléchargement ses paramètres d’export. Le champ facultatif `EntryModelRun.OutputTemplateId` applique ce choix uniquement au traitement courant : il exige un `ModelId` précis et ne modifie pas la règle enregistrée. Le format réellement appliqué est conservé dans l’instantané du résultat archivé.

Dans **Espace clients → Écritures comptables**, deux vues donnent accès aux modèles du client et aux résultats enregistrés. La page **Modèles d’écritures** permet également leur gestion.

La table `StatementTreatments` conserve automatiquement le dernier modèle, les paramètres et l’aperçu calculé côté serveur pour chaque document et chaque extrait sauvegardé, dès la préparation ou l’aperçu et lors d’un export réussi. La consultation restaure directement ce résultat sans refaire le choix du modèle ; les mots ajustés et les exclusions des écritures sont conservés. Si la source change de version, les choix sont repris et le résultat est recalculé. Un changement de client ou de compte empêche de reprendre les paramètres de l’ancien dossier. Les anciens documents retrouvent leur dernier modèle depuis les archives existantes, puis recalculent un aperçu. Les migrations `AddStatementTreatmentMemory` et `EnsureStatementTreatmentCascade` installent ce stockage ; il est supprimé avec le document source, tandis que les archives de traçabilité restent indépendantes.

Le parcours des écritures reprend les quatre étapes de conversion : importer, choisir le modèle d’écritures et son format, vérifier le résultat, télécharger. La vérification présente les résultats à gauche et le PDF à droite ; sur mobile, le document se place sous les résultats. Le PDF peut être réduit pour agrandir les résultats. Les filtres sont repliables et proposent les abréviations enregistrées. Le bouton **Analyse** affiche les totaux TTC, HT et TVA des mouvements retenus et leurs correspondances. La barre finale propose **Retour** et **Terminer et télécharger** : cette action valide la source si nécessaire, génère et archive le fichier, puis le télécharge. Le bloc de correction imbriqué est supprimé. La sélection du journal n’est pas une étape de ce parcours. Les anciens liens `type=accounting&step=journal` ouvrent le choix du modèle avec les paramètres conservés.

Chaque modèle définit le compte de la banque du client, l’intitulé, l’abréviation, les expressions recherchées et le taux de TVA sur TTC (0, 7, 10, 14 ou 20 %). Le choix d’une écriture dans le traitement applique ce modèle précis. Les ajustements des mots pendant le traitement restent propres à ce traitement.

Les colonnes par défaut sont : N° compte, Intitulé, Libellé, Banque et Montant HT. Leur ordre, leur titre, leur visibilité et leur présence dans le TXT sont configurables. Les champs TTC, TVA, taux, date, référence, abréviation et devise peuvent être ajoutés. Le modèle choisit aussi le séparateur, l’encodage, les décimales et la présence des en-têtes.

Le bouton **Modèle TXT** télécharge la structure des colonnes exportables. Le téléchargement du résultat contient les mouvements retenus et suit les paramètres du modèle. La source doit être vérifiée et validée avant cet export.

`AccountingEntryArchives` conserve le fichier, son empreinte SHA-256, les identités du client et de la banque, les versions, l’auteur, le modèle utilisé, les paramètres, la source et les lignes calculées. Cette archive reste accessible après suppression du modèle ou du relevé. Une nouvelle tentative du même export retrouve le fichier existant ; un résultat modifié crée une nouvelle archive.

Le résultat TXT est produit par `IndependentEntryModelService` et `EntryModelTextExport`. Il ne crée pas les lignes équilibrées des anciens exports Sage et ne modifie pas leurs compteurs de pièces. Les anciens exports restent consultables dans l’espace client.

La migration additive `20261103093537_AddIndependentEntryArchives` crée uniquement la table d’archives et ses index.

Vérifications :

```powershell
dotnet run --project tools/EntryModelIntegrationChecks -- backend/BankStatementConverter.API/appsettings.Development.json
```

Cette vérification utilise un schéma PostgreSQL temporaire supprimé à la fin ; elle conserve les données de l’application. Les contrôles couvrent aussi la configuration TXT, la TVA, les empreintes, les téléchargements répétés et la consultation après suppression de la source.
