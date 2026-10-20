# Extraction des factures et des relevés : comparaison et adaptations

Analyse du code local du 20 octobre 2026. Projet de référence :
`C:/Users/hp/Desktop/invoice-ai-maroc-mvp/invoice-ai-maroc`.
Ce projet a été consulté sans modification. Sa base de factures et ses documents clients
n'ont pas été utilisés pour alimenter les relevés.

## Ce que fait le projet de factures

| Mécanisme | Code de référence | Intérêt pour les relevés |
| --- | --- | --- |
| Lecture native et OCR | `backend/app/ocr/document.py` | Lire le texte sélectionnable avant de rasteriser une page. Conserver les positions, la page et la confiance OCR. |
| Reconstruction géométrique | `backend/app/services/layout.py` : `rows_of`, `layout_text`, `extract_layout` | Regrouper les cellules par ligne et abscisse, sans perdre la séparation entre colonnes. |
| Contrôle local avant IA | `backend/app/services/extraction.py` : `AIExtractionService.run` | Éviter un appel externe si les lignes, les champs et les contrôles sont déjà cohérents. |
| Texte positionné pour l'IA | `layout_text` et `AIExtractionService.run` | Transmettre des lignes avec leurs positions plutôt qu'une liste de mots sans structure. |
| Provenance | `locate_fields` et sources du moteur de mise en page | Une position est une preuve seulement lorsque le rapprochement est non ambigu. |
| Vérification des montants | `unsupported_amounts` | Signaler un montant IA absent du texte OCR ; ne pas le remplacer par une supposition. |
| Comparaison OCR/vision | `AIExtractionService.run`, mode `hybrid` | Détecter les contradictions entre deux lectures. Le projet conserve la première lecture et les écarts. |
| Cache par empreinte | `backend/app/ocr/cache.py` | Réutiliser une lecture du même fichier. Le cache du projet de factures est sur disque. |
| Profils vérifiés | `backend/app/services/learning.py` | Les repères de mise en page peuvent être réutilisés ; les valeurs d'un document ne doivent pas compléter arbitrairement un autre document. |

Le contrôle « complet » des factures exige notamment des lignes complètes, des montants
HT/TVA/TTC imprimés et une égalité arithmétique. Ces règles ne sont pas directement
transposables à un compte bancaire : il faut contrôler les débits, crédits, dates,
reports et soldes, et conserver tous les mouvements, y compris ceux qui se compensent.

Le projet de référence a aussi ses limites : `read_document` privilégie le texte natif
dès qu'il trouve au moins 40 caractères sur une page. Une page contenant un en-tête
texte et un tableau image peut donc nécessiter une détection complémentaire.
Il impose lui-même des plafonds de pages, de texte et de durée ; recopier son pipeline
ne supprimerait pas ces plafonds et ne garantirait pas une extraction universelle.

## Adaptations effectuées dans Alias

1. **Pages mixtes.** La lecture native repère les zones d'image importantes. Elles
   déclenchent une lecture OCR même si du texte ou des mouvements natifs sont présents.
   Une surface d'image cumulée d'au moins 3 % de la page est retenue parmi les images
   suffisamment larges et hautes ; les petits logos restent ignorés. Ce repère est
   une heuristique : il ne comprend pas le contenu de l'image et peut manquer une
   très petite zone scannée ou demander une lecture inutile d'une grande illustration.
2. **Lecture sélective.** PaddleOCR reçoit les pages suspectes ; l'assistance gratuite
   reçoit les pages qui restent non résolues. La pagination d'origine est conservée.
3. **Contexte géométrique.** OpenRouter reçoit le PDF de la page et le texte local
   regroupé par ligne et position. La lecture Paddle est transmise quand elle est
   disponible, même si son interprétation bancaire était incomplète. Le contexte est
   traité comme une donnée non fiable. Un contexte dépassant 64 000 caractères est
   explicitement signalé comme tronqué ; la page PDF complète reste jointe.
4. **Cache cohérent.** L'empreinte du contexte local entre dans la clé du cache IA.
   Une nouvelle lecture OCR ne réutilise pas une réponse calculée avec un ancien
   contexte. Le cache IA reste borné en mémoire et isolé par clé OpenRouter. Le service
   Paddle conserve aussi ses pages réussies pendant dix minutes, dans un cache mémoire
   de 32 Mo indexé par le PDF exact, la page et le modèle. Aucune nouvelle mémoire de
   relevés sur disque n'a été ajoutée.
5. **Provenance des mouvements.** Les parseurs natif et structuré conservent les pages
   sources, y compris les libellés poursuivis sur la page suivante dans les tableaux.
   Des coordonnées de cellules ne sont pas inventées pour une transcription IA.
6. **Montants étayés.** Les montants IA sont comparés à la lecture locale indépendante.
   Une absence de correspondance crée une alerte, sans réécriture automatique. Une
   correspondance textuelle seule ne prouve ni la bonne ligne ni la bonne colonne.
7. **Protection de l'exhaustivité.** Une image importante non résolue, une page illisible
   ou une omission détectée empêche la validation automatique du relevé. Les contrôles
   comptables et la comparaison au PDF original restent nécessaires.
8. **Texte source lisible.** Les mots natifs sont regroupés en lignes avec des tabulations
   entre positions ; un montant identique dans deux colonnes reste présent deux fois.

Les règles de reconnaissance des factures, l'identité des fournisseurs et les montants
de leur base ne sont pas repris dans les relevés. Les exports Sage/CSV restent une
étape distincte, après normalisation et revue des mouvements.

## Limites retirées et limites conservées

Conformément au choix de l'utilisateur :

- retrait du plafond de 200 pages dans le lecteur PDF et le service OCR local ;
- retrait du plafond de 8 pages transmises à l'assistance IA ;
- suppression des délais automatiques de conversion par défaut, dans les moteurs
  locaux, AI Studio, OpenRouter et le parcours hybride ;
- relais HTTP dédié à l'import PDF, sans minuterie de réponse du proxy de réécriture ;
- maintien de l'annulation utilisateur, propagée au relais et aux moteurs ;
- suppression du plafond PDF de 20 Mo, maintien de la file multiutilisateur et des protections de
  mémoire, de structure et de taille des réponses ;
- maintien des quotas, de la disponibilité et des tailles de contexte imposés par
  les fournisseurs. Aucun modèle payant n'est ajouté.

Les paramètres de durée et `Hybrid:MaxAiPages` acceptent `0` pour désactiver le plafond.
Une valeur positive reste possible pour un déploiement qui souhaite en fixer un.
Le délai d'admission dans la file (`Ocr:QueueWaitSeconds`) concerne l'attente avant
conversion et reste distinct de la durée de conversion. Un proxy ou hébergeur externe
peut imposer ses propres délais : ils ne sont pas contrôlés par le code d'Alias.
Les conversions longues nécessitent de garder la connexion ouverte. Un traitement
persistant en arrière-plan serait nécessaire pour survivre à une fermeture du navigateur
ou au redémarrage du serveur ; cela ne fait pas partie de cette modification.

## Vérifications

Les tests ajoutés couvrent les PDF entièrement scannés, une page mixte avec mouvements
natifs déjà équilibrés, les images non lues, les petits logos, 201 pages, plus de huit
pages assistées, l'annulation sans délai, le contexte OCR envoyé à l'IA, l'invalidation
du cache après changement de contexte, les montants non étayés et le relais HTTP.
Les tests des moteurs externes utilisent des réponses simulées : ils ne constituent
pas une mesure de performance ni une preuve d'exactitude sur tous les relevés réels.

## Cas réel : extrait web BMCE

Le PDF de deux pages fourni le 20 octobre contient une police Type3 dont la couche
texte ne permet pas de retrouver les caractères affichés : le lecteur natif rencontre
111 caractères nuls. La détection de texte interne non fiable déclenche désormais
la lecture du rendu visuel, même sans image raster intégrée au PDF. Une page dont le
texte n'est pas interprétable ne doit plus être considérée comme résolue uniquement
parce qu'elle contient beaucoup de mots.

La lecture locale PP-OCRv6 small a retrouvé 50 mouvements (36 et 14), sans libellé vide
ni montant absent/ambigu, en 67 973 ms hors démarrage du moteur. Les 50 débits/crédits
ont été comparés aux deux pages rendues, avec vérification agrandie pour deux montants
qui prêtaient à confusion à faible résolution. Aucun appel OpenRouter n'a été nécessaire.
Le compte et son titulaire sont reconnus dans la carte de compte en haut de la page.
Les signes et suffixes monétaires sont interprétés selon les colonnes débit/crédit.
Une seconde mesure après activation du cache a donné 66 668 ms à froid, puis 476 ms
pour la relance du même fichier avec ses deux pages déjà en mémoire.

Cet extrait ne fournit pas un solde initial et un solde final encadrant tous les
mouvements : les cartes « Solde » et « Opérations à venir » ne sont pas utilisées
comme ces deux soldes. L'absence de seconde colonne de date conserve la règle existante
de copie de la date d'opération pour l'export, avec l'avertissement
`PDF_DATES_COMPLETED` : cette date de valeur complétée n'est pas une donnée imprimée.
La période absente reste à vérifier. Les résultats restent non validés.

Le moteur local utilise désormais `OCR_MODEL_SIZE=small` par défaut ; `tiny` et
`medium` restent configurables. Ce choix a été testé sur ce document et ne constitue
pas une garantie de précision sur des scans dégradés. Les tailles disponibles sont
décrites dans la [documentation officielle PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR/blob/main/docs/version3.x/pipeline_usage/OCR.md).

Vérification finale : 89 tests .NET ciblés, six tests Python et sept scénarios
Playwright réussis ; types, lint, formatage et compilation Next.js vérifiés.
La suite unitaire complète conserve 265 réussites et les mêmes 70 échecs antérieurs,
comparés par nom avec l'exécution de référence. L'API et le service OCR small répondent
à leurs contrôles de santé. Les rendus et JSON de diagnostic du PDF ont été supprimés
après vérification ; le fichier original fourni reste inchangé.
