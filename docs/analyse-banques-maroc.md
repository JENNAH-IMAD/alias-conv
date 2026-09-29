# Analyse des relevés bancaires — Maroc

Analyse du 23 septembre 2026. Périmètre confirmé par l’utilisateur : Maroc uniquement. Inventaire lu via l’API locale ; aucune donnée bancaire ni configuration modifiée.

## État réel de l’application

| Banque enregistrée | Code interne actuel | Comptes | Profils actifs |
|---|---|---:|---:|
| Attijariwafa Bank | 13556 | 0 | 0 |
| Banque Populaire | 112458 | 0 | 0 |
| BMCE Bank | 662354 | 0 | 0 |
| BNP Paribas | 224455 | 1 | 1 |
| Crédit Agricole | 121212 | 0 | 0 |
| Société Génerale | 55775 | 0 | 0 |

Ces codes sont les valeurs saisies dans le projet, sans validation comme codes bancaires officiels. Ils ne doivent pas servir à détecter la banque dans un RIB sans vérification.

Le profil BNP actif s’appelle « Fixture délimitée fictive », moteur `delimited`, séparateur point-virgule. Son activation ne démontre pas la prise en charge d’un relevé réel BNP/BMCI. Les moteurs BMCE sont présents dans le code mais aucun profil n’est actuellement rattaché à la banque BMCE restante. Les tests précédents concernent les deux exemples fournis, dont le PDF texte explicitement fictif, et ne constituent pas une certification générale BMCE.

## Recherche officielle et conséquences

- **Attijariwafa bank** : le portail entreprises décrit la consultation et le téléchargement des documents bancaires. La FAQ Attijarinet particuliers distingue documents et export d’opérations, avec PDF, CSV, Excel et TXT. Ne pas supposer que le PDF d’historique et le relevé mensuel ont la même structure, ni que tous les formats particuliers sont disponibles sur chaque contrat entreprise. Sources : [Documents entreprises](https://attijarientreprises.com/en/bank-documents), [FAQ Attijarinet](https://attijarinet.attijariwafa.com/particulier/static/faq).
- **Banque Populaire Maroc** : Chaabi Net donne accès aux opérations et aux archives de relevés. Les informations publiques consultées ne fournissent pas une spécification exploitable des colonnes et positions PDF. Prévoir un échantillon du canal réellement utilisé par l’entreprise. Source : [Chaabi Net](https://particulier.groupebcp.com/fr/Pages/chaabi-net.aspx).
- **BANK OF AFRICA / BMCE** : BMCE Direct permet le téléchargement des relevés bancaires et avis d’opérations. Conserver des variantes pour les présentations anciennes et nouvelles si les documents le justifient. Les lecteurs existants doivent être vérifiés sur de vrais relevés de comptes professionnels. Source : [BMCE Direct](https://www.bankofafrica.ma/fr/particuliers/banque-distance/bmce-direct).
- **Entrée BNP Paribas** : établissement exact à confirmer ; pour un compte BMCI au Maroc, utiliser une identité et des profils BMCI, sans appliquer les modèles BNP France. BMCI Connect documente le téléchargement PDF ; les offres professionnelles et entreprises doivent être distinguées. Sources : [FAQ BMCI Connect](https://www.bmci.ma/aide-assistance/faq-bmci-connect/), [BMCI Connect Pro](https://www.bmci.ma/professionnels/applications-mobiles-bmci-connect-pro/). La page Pro contient une annonce prospective : elle ne suffit pas à identifier le canal actuellement utilisé par un client.
- **Crédit Agricole du Maroc** : la banque décrit les relevés mensuels pour les comptes particuliers et présente ses services entreprises. Les pages consultées ne permettent pas de déduire la disposition des PDF entreprises. Sources : [Gestion des comptes](https://www.creditagricole.ma/fr/particulier/gestion-de-vos-comptes), [Entreprises](https://www.creditagricole.ma/fr/entreprise).
- **Société Générale Maroc / Saham Bank** : le rapport annuel officiel situe le changement de nom en 2025. L’application Saham permet de télécharger les relevés. Prévoir la reconnaissance des deux marques pour les archives, sans supposer que leurs tableaux sont identiques. Sources : [Rapport annuel 2025](https://www.sahambank.com/rapport-annuel-2025/Rapport-Annuel-SB-2025.pdf), [Application Saham](https://www.sahambank.com/appli-mobile/).

Les sources confirment des services et familles de documents, pas des contrats de format PDF stables. Aucun ordre de colonnes propre à une banque ne doit être inventé à partir de son nom ou logo.

## Architecture proposée, à développer

1. Identifier banque, pays, canal, type de compte et version de présentation. Séparer relevé périodique, historique d’opérations, avis d’opération et RIB ; ces deux derniers ne sont pas des relevés à convertir.
2. Détecter texte natif, scan ou pages mixtes. Un PDF téléchargé peut contenir du texte, des images ou les deux.
3. Utiliser un moteur de tableau texte par positions/ancres, un moteur OCR avec coordonnées et les adaptateurs spécialisés nécessaires. Le moteur texte délimité actuel ne remplace pas un lecteur générique de tableaux PDF.
4. Stocker dans chaque profil les en-têtes, zones, colonnes, règles de dates/montants, continuation des libellés, lignes à ignorer et règles de soldes. Versionner ces règles.
5. Produire le modèle standard, contrôler les données puis appliquer les exports indépendants Sage 100, Sage X3 ou CSV personnalisé.

La sélection automatique d’un profil doit proposer une correspondance explicable et refuser une ambiguïté. Prévoir les états Brouillon, À tester et Validé sur exemples, distincts du simple booléen Actif.

## Échantillons et validation

Pour chaque présentation : idéalement trois relevés de mois différents, dont un multipage avec libellés longs. Garder un document hors des réglages initiaux pour vérifier la généralisation. Si les scans sont utilisés, ajouter un scan représentatif. Les données d’identité peuvent être masquées sans modifier les colonnes, dates, montants, soldes ou sauts de page nécessaires au test.

Vérifier le nombre d’opérations, les totaux débit/crédit, les reports et soldes lorsque fournis, les signes, les dates de valeur, les libellés multilignes et les doublons légitimes. Tester les trois exports et leur acceptation dans les configurations Sage effectivement utilisées. Sans solde, afficher une vérification partielle ; une ligne non reconnue doit être signalée, pas ignorée silencieusement.

Priorité proposée : identifier les établissements, qualifier les profils de démonstration, valider BMCE sur des relevés réels, puis intégrer les autres banques au rythme des échantillons et des besoins du cabinet. Ne pas activer six profils supposés compatibles uniquement sur la base de cette recherche.
