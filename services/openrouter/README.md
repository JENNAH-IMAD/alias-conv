# Lecture des relevés avec OpenRouter

Le parcours conserve deux résultats distincts :

1. **La transcription du PDF** : les pages sont envoyées une par une à OpenRouter.
   Le modèle restitue les blocs de texte et les cellules des tableaux, dans l'ordre
   imprimé, sans convertir les dates ou les montants. La transcription conserve les
   intitulés et l'ordre des colonnes, les cellules vides, les références, les soldes,
   les reports et les totaux. Les informations illisibles sont signalées.
2. **L'export métier** : Alias adapte les cellules reconnues en mouvements bancaires.
   Après contrôle et correction, les générateurs Sage X3, Sage 100 et CSV existants
   utilisent le modèle choisi. L'IA ne fabrique pas directement le fichier comptable.

La transcription n'est pas une garantie d'exactitude ni d'exhaustivité. Les vérifications
comptables et la consultation du PDF original restent nécessaires. Les corrections
apportées aux mouvements ne réécrivent pas le texte source conservé à l'import.

## Configuration

Dans l'application, avec un compte administrateur :

1. Ouvrir **Configuration OCR** et choisir **Automatique** (recommandé), ou **OpenRouter** pour imposer la transcription distante.
2. Choisir un identifiant de modèle, par exemple `openrouter/free`.
3. Saisir la clé API OpenRouter puis enregistrer.

Seuls `openrouter/free` et les identifiants se terminant par `:free` sont autorisés.
Le routeur `openrouter/free` choisit un modèle gratuit compatible avec les réponses
JSON structurées. Le parseur PDF `cloudflare-ai`, gratuit selon la documentation
OpenRouter, est explicitement sélectionné ; le parseur OCR payant par défaut n'est
jamais utilisé. Les plafonds de prix des jetons, requêtes et images sont fixés à zéro.
Aucun repli vers un modèle payant n'est configuré.
Le routeur gratuit peut essayer ensuite `dots-studio/dots-3-note-preview:free` et
`xingchen-agi/xing4.0-29b-a4b:free` si le routeur gratuit rencontre une indisponibilité.
Le choix explicite d'un modèle `:free` reste limité à ce modèle.

Le catalogue des modèles gratuits a été vérifié le 20 octobre 2026. La disponibilité
et les quotas de ces offres peuvent changer. Un refus ne déclenche aucun traitement payant.

Les nouvelles conversions utilisent immédiatement la configuration enregistrée.
L'API ne retourne jamais les clés au navigateur. Le jeton PaddleOCR et la clé
OpenRouter sont indépendants et conservés chiffrés dans le fichier local
`uploads/.settings/ocr.protected`, avec les clés de protection dans `uploads/.keys`.
Ces dossiers sont exclus de Git et des livrables de code.

Sans réglage enregistré dans l'interface, la configuration serveur initiale peut être
fournie par `Ocr__Provider=OPENROUTER`, `OpenRouter__Model` et `OpenRouter__ApiKey`.
Une configuration chiffrée existante reste prioritaire : utiliser l'écran administrateur
pour la modifier. Ne jamais ajouter une vraie clé aux fichiers exemples ou au dépôt.

Les PDF sont transmis à OpenRouter et au fournisseur sélectionné. Les requêtes demandent
des fournisseurs n'utilisant pas les données pour leur collecte (`data_collection=deny`).
Cette option ne constitue pas une garantie d'absence de toute rétention technique.
Les quotas gratuits et les restrictions du compte OpenRouter continuent de s'appliquer.
Alias ne modifie ni le crédit ni le plafond de la clé. Les PDF sont analysés par le
parseur gratuit avant l'inférence ; la qualité de la structure dépend aussi de cette
première lecture, notamment pour les scans et les tableaux complexes.

## Consulter et télécharger le texte

Après un import OpenRouter, la sélection du modèle propose **Consulter la transcription
du relevé**. Dans l'espace de conversion, le panneau des PDF propose aussi **Texte source**.
Le bouton **Télécharger le texte source** produit un `.txt` UTF-8 : les colonnes sont
séparées par des tabulations et les valeurs contenant tabulations ou retours à la ligne
sont entourées de guillemets. Ce fichier conserve le contenu et l'ordre logique, pas les
polices, couleurs ou positions exactes du PDF.

La consultation nécessite `documents.read` ; le téléchargement nécessite
`documents.export`. Le texte est affiché comme du texte, jamais exécuté comme du HTML.
Les anciens imports sans extraction conservée doivent être réimportés.

## Traitement et erreurs

### Mode automatique

Le texte natif est lu localement en premier. PaddleOCR reçoit seulement les pages
suspectes, avec leurs numéros originaux ; AI Studio reçoit un PDF limité à ces pages.
OpenRouter n'est sollicité que pour les pages encore non résolues, si une clé est
configurée. Le moteur Paddle est choisi séparément : local par défaut, ou AI Studio.

`Hybrid:TimeoutSeconds`, `Hybrid:PaddleTimeoutSeconds` et `Hybrid:MaxAiPages`
valent désormais `0` par défaut : aucune durée maximale ni limite de pages assistées
n'est imposée à la conversion. Une valeur positive rétablit un plafond explicite.
Un fournisseur distant peut continuer son propre traitement après annulation.
L'attente dans la file partagée reste distincte. Aucun plafond de taille PDF n'est imposé par l'application.

En cas d'échec, les données déjà lues restent disponibles pour revue. Les pages sans
lecture et les lignes datées avec montant non reprises deviennent des erreurs bloquant
la validation. Les contrôles comptables existants restent actifs. Les exports forcés
avec erreurs restent soumis au parcours existant de confirmation. Une divergence
entre deux lectures ne remplace pas silencieusement les montants de la première.
Le nombre de pages par méthode et la durée mesurée apparaissent dans la revue.
Ces contrôles ne peuvent pas prouver l'exhaustivité d'un relevé de format inconnu.

Pour le moteur local sur CPU, la détection est plafonnée à 1 280 pixels sur le grand
côté, tout en conservant les images de reconnaissance en haute résolution.
`OCR_DETECTION_MAX_SIDE` permet d'ajuster ce plafond entre 960 et 2 560 pixels selon
la finesse du texte et le matériel ; un plafond inférieur peut manquer du petit texte.
Le paramètre correspond à `text_det_limit_side_len` avec `text_det_limit_type=max`
dans la [documentation PaddleOCR](https://paddlepaddle.github.io/PaddleOCR/main/en/version3.x/pipeline_usage/OCR.html).

Mesures locales du 20 octobre 2026, sur un relevé synthétique de huit pages et huit
mouvements : 788 ms pour les huit pages natives ; 54 467 ms pour sept pages natives
et une page image avec PaddleOCR CPU. Les huit mouvements, le débit total de 80 et
le solde final de 920 ont été vérifiés. Ces mesures excluent le démarrage des modèles
et ne préjugent pas du délai d'un relevé réel plus dense ou entièrement scanné.

### Mode OpenRouter explicite

- Le moteur est appelé même si le PDF contient déjà du texte natif.
- Un appel est fait par page, avec une limite de sortie de 16 384 jetons par page.
- Deux pages au maximum sont traitées simultanément dans le processus API.
  `OpenRouter:ConcurrentPages` permet de réduire ce nombre à 1.
  `OpenRouter:PageTimeoutSeconds` et `OpenRouter:DocumentTimeoutSeconds` valent
  `0` par défaut : seul le jeton d'annulation utilisateur interrompt l'attente locale.
- Les pages réussies sont conservées uniquement en mémoire pendant dix minutes,
  dans un cache limité à 32 Mo. Une relance du même PDF avec la même clé et le même
  modèle les réutilise ; un redémarrage ou une éviction les supprime. Aucune page
  d'un autre compte OpenRouter n'est réutilisée. Une modification du contexte OCR
  invalide la réponse mise en cache pour l'ancien contexte.
- Une réponse tronquée, mal formée ou interrompue refuse l'import complet.
- Les limites structurelles ne permettent pas de détecter toute omission sémantique
  d'une ligne par le modèle. Le contrôle humain du relevé demeure nécessaire.
- Les refus de clé, plafonds de dépenses, crédits insuffisants, indisponibilités et
  dépassements de délai produisent des messages explicites, sans réponse brute du
  fournisseur ni secret dans les journaux applicatifs.
- Une page sans colonnes bancaires reconnaissables peut rester transcrite, avec des
  mouvements à compléter avant l'export. Aucun montant manquant n'est inventé par l'IA.

Le prompt de transcription est versionné dans
`backend/BankStatementConverter.Infrastructure/Exports/OpenRouterTranscription.cs`.
Les appels sont implémentés avec `HttpClient` ; aucune dépendance SDK n'est ajoutée.

## Vérifications

```powershell
dotnet test backend/BankStatementConverter.Tests --filter 'FullyQualifiedName~HybridExtraction|FullyQualifiedName~OpenRouter|FullyQualifiedName~SourceTextPermission'
cd frontend
npm run typecheck
npm run lint
npm run test:e2e -- --grep 'OpenRouter|Configuration PaddleOCR'
```

Les tests serveur utilisent des PDF synthétiques et des réponses HTTP simulées :
ils vérifient l'ordre des pages, les cellules, la normalisation, l'indépendance des
exports, les erreurs, les secrets et les permissions. Les tests navigateur vérifient
les réglages et le téléchargement du texte source sur ordinateur et mobile.
Un test avec une vraie clé reste nécessaire pour valider l'accès au fournisseur.

Le 20 octobre 2026, le test réel d'un PDF synthétique d'une page a réussi via
`dots-studio/dots-3-note-preview:free` : deux mouvements reconnus, débits/crédits
exacts, coût d'inférence déclaré par OpenRouter de `0`, puis génération d'un CSV
à quatre colonnes avec le moteur d'export existant. Aucun document client n'a été
transmis pour ce test. Les autres essais ont aussi rencontré des refus liés aux
politiques de confidentialité ou aux quotas : l'offre gratuite reste dépendante
de la disponibilité des fournisseurs.

Les tests ciblés couvrent aussi la lecture sélective, l'ordre des pages, la reprise
après échec et le maintien des alertes sur les données incomplètes.
Après ajout du parcours automatique : 38 tests serveur ciblés, deux tests Python de
sélection des pages et quatre scénarios navigateur ont réussi. La suite unitaire
complète compte 247 réussites et les mêmes 70 échecs préexistants ; les noms des
tests en échec ont été comparés à l'exécution de référence.
La compilation .NET, la compilation de production Next.js, les types, le lint et
le formatage du frontend ont été vérifiés. La suite .NET hors intégration conserve
les 70 échecs préexistants documentés dans `docs/qualite-projet.md` ; l'ensemble
du projet ne dispose donc pas encore d'une suite de tests entièrement verte.

Documentation officielle : [entrées PDF](https://openrouter.ai/docs/guides/overview/multimodal/pdfs)
et [sorties structurées](https://openrouter.ai/docs/guides/features/structured-outputs).
