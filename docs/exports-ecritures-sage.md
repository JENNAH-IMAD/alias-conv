# Préparation comptable et exports Sage

## Parcours actuel

Les pages Journaux de saisie et Plans, taxes & comptes tiers, leurs API et leurs tables sont retirées. La migration transfère les modèles d’écritures vers un stockage dédié avant de supprimer les anciens référentiels.

Le traitement courant passe par **Modèles d’écritures**, **Modèles d’exportation → Écritures comptables** et **Conversion & export**. Les règles du client reconnaissent les libellés et calculent le HT et la TVA sur TTC. Les résultats, les fichiers et les formats utilisés sont conservés dans l’espace client. Voir [Modèles d’écritures et résultats TXT](modeles-ecritures-txt.md).

## Les 11 colonnes du modèle Sage 100

Le modèle standard `SAGE100ENTRIES` reprend les colonnes de saisie du journal dans cet ordre : **Jour, N° pièce, N° facture, Référence, N° compte général, N° compte tiers, Libellé écriture, Date échéance, Pointage, Débit, Crédit**.

Le jour est extrait de la date comptable sur deux chiffres (`01` à `31`). Le compte général correspond à chaque ligne, bancaire ou de contrepartie ; le compte tiers est séparé. Facture, référence, tiers, échéance et pointage restent vides s'ils ne sont pas renseignés. L'échéance utilise `dd/MM/yyyy`. Les montants utilisent deux décimales et la culture `fr-FR`. Le modèle reste sans en-tête, séparé par `;`, encodé en Windows-1252 par défaut.

Ce format suppose un journal, un mois et une devise uniques, à choisir dans le dossier d'import Sage. L'export refuse de mélanger plusieurs journaux, mois ou devises sans les colonnes correspondantes. Un modèle personnalisé peut conserver une date complète et les colonnes Journal/Devise pour des exports plus larges.

Au démarrage de l'API, les anciens modèles standards reconnus exactement sont actualisés en conservant leur identifiant. Les modèles personnalisés et les exports archivés sont conservés. Dans l'éditeur, **Appliquer les 11 colonnes Sage 100** prépare ce remplacement dans le formulaire ; **Enregistrer** l'applique au modèle.

## Profils existants et Sage X3

Les profils de relevé `SAGE100` et `SAGEX3` restent disponibles ; le profil X3 conserve ses lignes de soldes. `SAGEX3ENTRIES` reste un export paramétrable des mouvements bancaires avec constantes (site, type de pièce), journal et compte issus de la banque. Le nouveau parcours de pièces équilibrées décrit ci-dessus concerne Sage 100. Il ne constitue pas une intégration automatique Sage X3.

Le format natif PNM et un modèle GAS universel ne sont pas générés. La compatibilité finale dépend des colonnes du modèle d'import réellement installé dans le dossier Sage : faire un import d'essai sur un dossier de test avant utilisation en production.

## Stockage et vérifications

La migration `RemoveJournalsAndPlansPreserveEntryModels` remplace les anciennes tables de référentiels par `ClientEntryModelSets`. Les catalogues de formats et les archives restent indépendants. `tools/EntryModelIntegrationChecks` vérifie le transfert exact des modèles et versions, la suppression des tables retirées, la TVA, les aperçus, les exports et les archives. Les formats Sage et leurs colonnes restent dans le catalogue des modèles d’exportation.
