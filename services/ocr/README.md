# Lecture PaddleOCR pour Alias

## Chaîne locale de traitement

Le mode `AUTO` avec `LOCAL` utilise uniquement les moteurs de lecture locaux. Chaque nouvelle
lecture repart des octets du PDF original. Seuls les poids des modèles sont réutilisés.

1. Le backend lit le texte natif, ses positions et les zones en image. Une lecture
   native complète et cohérente n’est pas envoyée inutilement à l’OCR.
2. Les pages non résolues passent au service Python : rendu des pixels, détection de
   mise en page `PP-DocLayout-M`, OCR `PP-OCRv6` et reconstruction `PP-StructureV3`.
3. Le service renvoie les blocs OCR, leurs positions et scores, ainsi que des grilles
   de cellules et les limites des tableaux. Les cellules fusionnées sont développées
   sans dupliquer les valeurs. Les modules bancaires ne reçoivent pas le HTML brut.
4. Les cellules financières douteuses sont relues depuis leurs pixels. Une correction
   reconnue est propagée à la grille seulement si sa source est non ambiguë.
5. Le backend compare l’interprétation des cellules et celle des positions du même
   document. Il retient la lecture la plus complète, avec contrôle des dates, montants,
   couverture des pages et équation des soldes. Les divergences financières entre
   deux lectures cohérentes demandent une confirmation explicite.
6. Le PDF converti reconstruit les champs source en tableau. Les exports appliquent
   ensuite le modèle choisi aux mouvements reconnus et corrigés.

`OCR_PIPELINE=structure` est le défaut ; `OCR_PIPELINE=text` permet de diagnostiquer
le seul OCR. `OCR_LAYOUT_MODEL` et `OCR_MODEL_SIZE` choisissent les modèles locaux.
Le premier lancement télécharge leurs poids. `/health` expose `layout_ready` et la
version de chaîne `local-layout-v1`. Si la structure échoue, l’OCR positionnel reste
actif et une alerte indique le repli : aucun résultat ancien n’est substitué.

Le mode automatique utilise PaddleOCR local ou PaddleOCR AI Studio selon
`Hybrid__AutoPaddleProvider`. Aucun autre moteur de secours distant n’est appelé.
La chaîne locale n’utilise aucun jeton et conserve les contrôles sur les valeurs
illisibles ou contradictoires.

Le compte **Admin** peut saisir ou remplacer le jeton PaddleOCR AI Studio depuis
**Configuration OCR** dans l’application, sans redémarrage. Le jeton n’est jamais
renvoyé au navigateur et les réglages sont enregistrés chiffrés côté serveur.
Ces réglages prennent priorité sur les fichiers de configuration décrits ci-dessous.

## API PP-OCRv6

Le modèle cloud est configurable avec `Ocr:CloudModel` (`Ocr__CloudModel` dans Docker).
La configuration locale utilise maintenant `PP-OCRv6`, avec le jeton uniquement dans
`appsettings.Development.json`, exclu de Git. Le modèle `PaddleOCR-VL-1.6` reste pris en charge.

PP-OCRv6 utilise les options `useDocOrientationClassify`, `useDocUnwarping` et
`useTextlineOrientation` désactivées, puis le même protocole de soumission et de suivi
décrit ci-dessous. Ses résultats `result.ocrResults[]` contiennent `rec_texts`,
`rec_scores` et `rec_polys` dans `prunedResult`. Les polygones sont convertis en points
PDF à partir des dimensions fournies par le service. Les textes, coordonnées, scores
et JSON brut sont conservés pour l’adaptation bancaire et la revue. Les images de
visualisation ne sont pas nécessaires à l’import. Ce modèle ne produit pas les tableaux
HTML de PaddleOCR-VL ; l’adaptation utilise les positions du texte reconnu.

### Erreur HTTPS constatée le 9 octobre 2026

Le diagnostic local reçoit pour `paddleocr.aistudio-app.com` le certificat
`CN=Fortiguard SDNS Blocked Page, O=Fortinet`, avec un nom de serveur incorrect et une
racine non approuvée. Cela indique une page de blocage du filtre réseau FortiGuard.
L’administrateur du réseau doit vérifier la catégorie de filtrage et autoriser le
domaine de l’API pour cet usage. Les domaines HTTPS des résultats devront également
être accessibles après la lecture. Le client conserve la vérification des certificats ;
il affiche une erreur HTTPS distincte des erreurs de fichier, de quota et de jeton.

La configuration de PP-OCRv6 peut être vérifiée manuellement dans l’application locale.
Le test réel complet reste impossible tant que ce blocage réseau persiste.

Source : [API officielle PP-OCRv6](https://ai.baidu.com/ai-doc/AISTUDIO/7mfz6dgx9).

## Relevés scannés avec colonnes étroites

Le parcours conseillé est `Ocr:Provider=AUTO` avec `AutoPaddleProvider=LOCAL` dans
Configuration OCR. Le texte natif fiable est conservé ; les pages scannées ou
incomplètes passent à PaddleOCR local et à PP-StructureV3. Les pages restant
illisibles sont signalées pour contrôle ; aucune lecture externe supplémentaire
n’est déclenchée.

Les deux lectures et les différences de dates, libellés, références et montants
sont conservées. Une divergence n’écrase pas une première lecture saine : elle
apparaît dans « Lecture du document ». L’utilisateur corrige les valeurs puis
confirme explicitement sa vérification. Les dates manquantes/hors période, montants
manquants et désaccords non vérifiés bloquent les exports comptables, même avec
l’option de téléchargement avec erreurs. Les extraits suivent les mêmes règles.

La reconnaissance des tableaux rapproche les intitulés de colonnes sur plusieurs
niveaux, y compris les en-têtes bilingues et les intitulés arabes reconnus. Elle
vérifie la présence de lignes datées sous les colonnes. Le sens des montants suit
les titres débit/crédit, même lorsque leur ordre est inversé. Ces règles sont
communes aux relevés et aux extraits ; aucun modèle propre à une banque n’est requis.

Le moteur local relit également les cellules financières dont le séparateur décimal
a disparu dans le texte OCR : la correction doit être reconnue sur l’image de la
cellule avec une confiance suffisante, jamais déduite de l’équilibre des soldes.
Les données illisibles, les pages manquantes et les désaccords restent signalés.

Le moteur local relit les dates sous l’en-tête compact `OPER VALEUR` lorsque les
deux colonnes se touchent. Il retire les traits verticaux du recadrage et reconnaît
chaque paire de dates avant l’adaptation bancaire. Une décimale tronquée par une
bordure est relue directement dans son image, sans être complétée par calcul.
Ces reprises ciblées concernent le fournisseur `LOCAL` (et sa sélection en mode
`AUTO`) ; elles ne modifient pas les réponses des API OCR distantes.

Le lecteur bancaire reconnaît ces intitulés CIH, les dates des soldes et le compte
dans la grille RIB. Il garde les deux dates séparées et n’utilise jamais le montant
du premier mouvement comme solde initial illisible. Les résultats OCR restent à
comparer au scan, notamment pour les libellés et les petits caractères.

## API officielle PaddleOCR-VL-1.6 (même moteur que le site AI Studio)

Le moteur essaie d'abord le texte natif du PDF, y compris en mode `AI_STUDIO`.
Une lecture sans défaut détecté est conservée pour la revue. Les scans et lectures
incomplètes passent au fournisseur OCR configuré. `Ocr:PreferNativeText=false`
permet de demander explicitement une lecture OCR systématique.

En mode `AI_STUDIO`, les PDF nécessitant l'OCR sont transmis à l'API distante avec
le modèle `Ocr:CloudModel`. Une connexion Internet et un jeton AI Studio valide avec
du quota sont nécessaires. Aucun runtime Python local n'est requis dans ce mode.
Un refus explicite « file pleine » (10010), avant création d'un travail, déclenche
jusqu'à trois nouvelles tentatives après 2, 4 et 8 secondes. `Ocr:QueueRetryCount`
accepte 0 à 5 tentatives supplémentaires. Le délai global reste applicable ; les
erreurs de quota, d'authentification et les échecs réseau ne sont pas resoumis.

Ajouter cette section au fichier local `backend/BankStatementConverter.API/appsettings.Development.json`
(exclu de Git et de la publication), ou fournir les variables d’environnement correspondantes :

```json
"Ocr": {
  "Provider": "AI_STUDIO",
  "CloudToken": "VOTRE_JETON_AI_STUDIO",
  "TimeoutSeconds": 0
}
```

Variables serveur : `Ocr__Provider=AI_STUDIO`, `Ocr__CloudToken`, `Ocr__TimeoutSeconds`.
Ne jamais placer le jeton dans `NEXT_PUBLIC_*`, les fichiers suivis par Git ou le navigateur.
Redémarrer l’API après une modification de configuration. Pour Docker, renseigner ces
variables dans `.env` (voir `.env.example`).

Le client .NET suit le protocole du SDK officiel :

1. `POST https://paddleocr.aistudio-app.com/api/v2/ocr/jobs` : multipart `file`,
   `model=PaddleOCR-VL-1.6`, `optionalPayload` avec orientation, déformation et graphiques
   désactivés, comme dans l’exemple fourni.
2. `GET .../jobs/{jobId}` toutes les cinq secondes : `pending`, `running`, `done`, `failed`.
   Le délai couvre la soumission, l’attente et le téléchargement ; les annulations sont propagées.
3. Téléchargement JSONL depuis `data.resultUrl.jsonUrl`, **sans envoyer le jeton API**
   à l’hébergeur du résultat. La taille est limitée à 64 Mo et toutes les pages sont vérifiées.
4. Chaque `result.layoutParsingResults[]` conserve son JSON original, son Markdown,
   les blocs de `prunedResult.parsing_res_list` et les cellules des tableaux HTML.
   Les coordonnées des blocs sont converties des pixels en points PDF ; les cellules
   financières sont lues directement, sans inventer de coordonnées ou de scores de confiance.
5. L’adaptation reconnaît les en-têtes Date, Date valeur, Libellé/Opération/Nature,
   Débit, Crédit et Référence. Les retours de ligne, dates coupées, séparateurs décimaux,
   reports de page et soldes sont traités avant l’export. Les montants contenus dans
   les libellés restent du texte. Les images de visualisation ne sont pas nécessaires
   à l’adaptation bancaire ; leurs références restent dans le JSON brut.

Dans **Texte lu & adaptation**, les vues **Tableaux / Markdown / JSON** permettent
de contrôler les résultats. Les boutons d’extraction téléchargent toutes les pages
en JSON ou Markdown. L’adaptation manuelle utilise les numéros de cellules affichés
(colonnes à partir de 1 dans l’interface, indices à partir de 0 dans l’API), pas les
positions des blocs. Les lignes d’en-tête fusionnées sont prises en charge automatiquement.
La fiche et l’export restent à vérifier : un résultat OCR ne valide jamais un relevé.

Le JSON complet est conservé dans `StatementImports.RawExtractionJson` (JSONB),
le relevé adapté dans `NormalizedJson` et le PDF original dans le stockage existant.
Aucune nouvelle migration ni modification des modèles d’exportation n’est nécessaire.
Une erreur de jeton renvoie HTTP 503 avec une instruction de configuration, le quota
HTTP 429, une lecture incomplète HTTP 502 et un dépassement du délai HTTP 504.
Le mode cloud ne bascule pas silencieusement vers le moteur local.

Pour vérifier le traitement réel, importer votre relevé depuis `/conversions`,
sélectionner le fournisseur configuré puis comparer les mouvements, libellés,
débits, crédits et soldes extraits au PDF original.

Sources officielles : [API et SDK PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR/tree/main/api_sdk),
[pipeline PaddleOCR-VL](https://github.com/PaddlePaddle/PaddleOCR/blob/main/docs/version3.x/pipeline_usage/PaddleOCR-VL.en.md),
[AI Studio](https://aistudio.baidu.com/paddleocr).

## Moteur local PP-OCRv6

Pour ce mode, choisir `Ocr:Provider=AUTO` avec `Hybrid:AutoPaddleProvider=LOCAL`,
ou `Ocr:Provider=LOCAL` pour utiliser directement le moteur local. Le texte natif est lu en priorité ;
PaddleOCR local est utilisé pour les scans et les tableaux natifs non reconnus.

Le service effectue seulement l’étape de lecture : **PDF → images → blocs de texte**.
PaddleOCR PP-OCRv6 lit le texte français/latin avec ses positions et scores de
reconnaissance. L’API .NET adapte ensuite ces blocs en champs bancaires. Le moteur
d’export exécute les modèles CSV, Sage 100 et Sage X3 sur les données validées.

### Lancement Windows du moteur local

Python **3.12 64 bits** est requis pour le runtime PaddlePaddle retenu. Le Python
3.14 par défaut du poste ne doit pas être utilisé pour installer ce service.

```powershell
# Premier lancement sur un autre poste : crée .local/paddle-runtime et installe les dépendances.
powershell -ExecutionPolicy Bypass -File services/ocr/start.ps1 -Install
# Lancements suivants :
powershell -ExecutionPolicy Bypass -File services/ocr/start.ps1
```

Sur ce poste, le script réutilise le runtime compatible `.local/hybrid-runtime`
lorsqu’il existe. Ce dossier contient les bibliothèques Python installées ; il
ne constitue pas un retour de l’ancien traitement hybride.
Le script vérifie que l'interpréteur fonctionne en Python 3.12 et peut également
réutiliser `.local/python312/python.exe`. Cela évite de sélectionner un environnement
copié qui pointe vers le dossier utilisateur d'un autre poste. Les modèles sont
conservés dans `.local/paddle-cache`.

Le premier démarrage télécharge les modèles PaddleOCR. Attendre que
`http://127.0.0.1:8091/health` réponde `{"status":"ok",...}` avant un scan.
Le service traite les PDF **localement**. Les téléchargements initiaux des modèles
ne transmettent pas les documents bancaires. Les fichiers ne sont pas écrits dans
le service ; le PDF original et l’extraction sont conservés par l’application.

Lancer ensuite l’API et le frontend comme indiqué dans le README principal.
L’API utilise `Ocr:BaseUrl=http://127.0.0.1:8091` et `Ocr:TimeoutSeconds=0`
par défaut (aucun délai maximal de conversion). Ces valeurs peuvent être remplacées par `Ocr__BaseUrl` et
`Ocr__TimeoutSeconds`. Docker Compose lance aussi le service OCR.

## Contrat

`POST /extract` attend un formulaire multipart avec `file` (PDF, sans plafond de taille applicatif).
Le résultat contient `method=PADDLE_OCR` et `pages[]` : numéro, largeur, hauteur,
`boxes[]` avec texte, gauche/droite/haut/bas et confiance. Les coordonnées sont
en points PDF, avec l’origine en haut à gauche ; aucun montant bancaire n’est
calculé en Python. Les images sont limitées à 3 200 pixels par côté et les
documents n'ont plus de plafond de pages ni de taille PDF applicatif.
Chaque processus protège son instance OCR par un verrou.
Le lancement Windows et Docker utilise deux processus par défaut, pour permettre
des lectures parallèles. `start.ps1 -Workers 1` réduit la consommation mémoire ;
`OCR_CPU_THREADS` règle les threads par processus (deux par défaut). L'API possède
une file d'attente asynchrone et deux créneaux OCR configurables ; voir le
[guide de fonctionnement des conversions](../../README.md#traitement-des-relevés-et-extraits).
Un fichier vide, corrompu ou protégé
retourne une erreur explicite.

`OCR_MODEL_SIZE=small` est le réglage CPU par défaut ; `tiny` et `medium` restent
disponibles. La détection est plafonnée à 1 280 pixels sur le grand côté, réglable
avec `OCR_DETECTION_MAX_SIDE` ; les recadrages de reconnaissance conservent la
résolution du rendu PDF. Le point `/health` indique la taille du modèle chargé.
Une police PDF mal encodée peut déclencher l'OCR même sans image raster dans le PDF.

Le paramètre multipart facultatif `pages` sélectionne les numéros d'origine à lire.
Chaque import ou nouvelle lecture relance les moteurs nécessaires sur le PDF original,
même si ce fichier a déjà été traité. Aucun résultat de page PaddleOCR
n’est réutilisé entre traitements. Seuls les modèles OCR chargés restent en mémoire.
Une annulation arrête les pages suivantes
après la page en cours ; une relance recommence la lecture.
Le bouton **Nouvelle lecture du PDF** remplace les mouvements et corrections de la
version courante. Changer uniquement le modèle d’exportation conserve les données
revues, sans déclencher une nouvelle extraction.

## Adaptation et limites

Le score OCR indique la certitude du texte lu, pas la justesse comptable.
Les tableaux reconnus sont adaptés automatiquement. Si aucun mouvement ne peut
être reconnu, le texte lu reste enregistré et accessible dans **Texte lu & adaptation**.
Des positions de colonnes en pourcentage permettent d’adapter un autre tableau.
Ces positions communes supposent la même disposition sur toutes les pages ; des
mises en page différentes, des scans très flous, inclinés ou écrits à la main
peuvent nécessiter une adaptation supplémentaire. L’application bloque toujours
la validation d’un relevé sans mouvement ou comportant des montants ambigus.

## Vérification manuelle

Importer un PDF dans Alias avec le fournisseur OCR souhaité, puis comparer
chaque page et chaque mouvement au document original avant export.

Documentation officielle : [pipeline OCR PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR/blob/main/docs/version3.x/pipeline_usage/OCR.en.md).
