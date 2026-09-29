# Vérification locale

- Analyse initiale : frontend Next.js/React/TypeScript présent ; backend vide ; pas de configuration de base ni d’API ; authentification et données de démonstration codées dans le frontend.
- Backend .NET 8 compilé ; 22 tests xUnit réussis, dont l’intégration sur PostgreSQL 18 (CRUD, modification de champs d’export, droits et trois conversions).
- Migration initiale appliquée et réapplication idempotente vérifiée ; aucun changement de modèle en attente.
- Client API TypeScript réellement exécuté contre l’API locale : login, récupération des référentiels, upload de la fixture PDF, export CSV et historique réussis.
- Frontend : compilation Next.js, TypeScript et ESLint vérifiés.
- Docker Compose : syntaxe validée. Le moteur Docker Desktop local retourne HTTP 500 sur `/info` ; construction et exécution des images **non vérifiées**.
- Test visuel interactif **non effectué** : aucun navigateur disponible dans l’outil de contrôle connecté.

L’application locale utilise maintenant la base `bankconverter` du serveur PostgreSQL sur `127.0.0.1:5432`, avec la connexion enregistrée dans `.local/dev-secrets.json`, ignoré par Git. Le script `.local/start-backend.ps1` permet de relancer le backend après compilation. Les tests utilisent une base distincte `bankconverter_tests` dans le cluster `.local/pgdata`, sur `127.0.0.1:55432` ; ce cluster de test utilise une authentification locale `trust` et ne doit jamais héberger des données réelles.

Gestion des banques complétée : formulaires dédiés d’ajout/modification, normalisation et unicité du code, confirmation de suppression, protection des associations, recherche et tri côté serveur, filtre selon les profils actifs, consultation paginée des comptes et profils associés. Les tests d’intégration vérifient ces opérations et le refus des écritures pour le rôle User. Le compte local de test `imadjennah@gmal.com` possède le rôle Admin ; une nouvelle connexion est nécessaire après changement de rôle.

Les banques réelles, OCR et spécifications Sage propres à une installation restent à valider avec des exemples fournis par le métier.
