# Lecture PaddleOCR pour Alias

Le compte **Admin** peut désormais saisir ou remplacer le jeton depuis
**Configuration OCR** dans l’application, sans redémarrage. Le jeton n’est jamais
renvoyé au navigateur et les réglages sont enregistrés chiffrés côté serveur.
Voir [la saisie du jeton et ses règles de conservation](../../docs/historique-et-configuration-ocr.md).
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

La configuration de PP-OCRv6 et ses tests simulés peuvent être validés localement.
Le test réel complet reste impossible tant que ce blocage réseau persiste.

Source : [API officielle PP-OCRv6](https://ai.baidu.com/ai-doc/AISTUDIO/7mfz6dgx9).

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
  "TimeoutSeconds": 600
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

Vérification réelle sur le relevé BMCE fictif fourni :

```powershell
dotnet run --project tools/OcrSmokeCheck -- output/paddle-vl-cloud-client.json "relvée/releve_bmce_exemple_01.pdf" --cloud
```

Le test soumet réellement le PDF puis compare les **36 mouvements**, dates, colonnes
débit/crédit et les deux soldes au document de référence. Autre vérification compatible
avec l’exemple Python fourni :

```powershell
python -m pip install requests
python services/ocr/cloud_smoke_test.py
dotnet run --project tools/OcrSmokeCheck -- output/paddle-vl-result.jsonl "relvée/releve_bmce_exemple_01.pdf" --vl
```

Sources officielles : [API et SDK PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR/tree/main/api_sdk),
[pipeline PaddleOCR-VL](https://github.com/PaddlePaddle/PaddleOCR/blob/main/docs/version3.x/pipeline_usage/PaddleOCR-VL.en.md),
[AI Studio](https://aistudio.baidu.com/paddleocr).

## Moteur local PP-OCRv6

Pour ce mode, choisir `Ocr:Provider=LOCAL`. Le texte natif est lu en priorité ;
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
L’API utilise `Ocr:BaseUrl=http://127.0.0.1:8091` et un délai de 600 secondes par
défaut. Ces valeurs peuvent être remplacées par `Ocr__BaseUrl` et
`Ocr__TimeoutSeconds`. Docker Compose lance aussi le service OCR.

## Contrat

`POST /extract` attend un formulaire multipart avec `file` (PDF, 20 Mo maximum).
Le résultat contient `method=PADDLE_OCR` et `pages[]` : numéro, largeur, hauteur,
`boxes[]` avec texte, gauche/droite/haut/bas et confiance. Les coordonnées sont
en points PDF, avec l’origine en haut à gauche ; aucun montant bancaire n’est
calculé en Python. Les images sont limitées à 3 200 pixels par côté et les
documents à 200 pages. Chaque processus protège son instance OCR par un verrou.
Le lancement Windows et Docker utilise deux processus par défaut, pour permettre
des lectures parallèles. `start.ps1 -Workers 1` réduit la consommation mémoire ;
`OCR_CPU_THREADS` règle les threads par processus (deux par défaut). L'API possède
une file d'attente asynchrone et deux créneaux OCR configurables ; voir le
[guide multi-utilisateur](../../docs/multi-user-conversions.md).
Un fichier vide, corrompu ou protégé
retourne une erreur explicite.

## Adaptation et limites

Le score OCR indique la certitude du texte lu, pas la justesse comptable.
Les tableaux reconnus sont adaptés automatiquement. Si aucun mouvement ne peut
être reconnu, le texte lu reste enregistré et accessible dans **Texte lu & adaptation**.
Des positions de colonnes en pourcentage permettent d’adapter un autre tableau.
Ces positions communes supposent la même disposition sur toutes les pages ; des
mises en page différentes, des scans très flous, inclinés ou écrits à la main
peuvent nécessiter une adaptation supplémentaire. L’application bloque toujours
la validation d’un relevé sans mouvement ou comportant des montants ambigus.

## Vérification réelle

```powershell
.local/hybrid-runtime/Scripts/python.exe services/ocr/smoke_test.py
dotnet run --project tools/OcrSmokeCheck -- output/ocr-scan-result.json "relvée/releve_credit_agricole_exemple_02.pdf"
```

Le script transforme un relevé synthétique de référence en PDF avec une couche
image uniquement, exécute réellement PaddleOCR, puis compare les 22 mouvements,
dates, débits/crédits et les soldes avec le document d’origine. Les descriptions
doivent toujours être vérifiées après OCR. Les tests backend normaux utilisent
un faux client OCR pour vérifier l’orchestration sans télécharger de modèles.

Documentation officielle : [pipeline OCR PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR/blob/main/docs/version3.x/pipeline_usage/OCR.en.md).
