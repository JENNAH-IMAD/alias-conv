# Gestion des utilisateurs par l’administrateur

La route `/users` est accessible depuis le menu uniquement pour le rôle `Admin`. Elle propose la recherche par nom/e-mail, le filtre de rôle, la pagination, la création, la modification du nom et de l’e-mail, le changement de rôle, la réinitialisation du mot de passe et la suppression avec confirmation.

API : `GET /api/users`, `POST /api/users`, `PUT /api/users/{id}`, `DELETE /api/users/{id}`. Le contrôleur impose `Authorize(Roles = "Admin")` à toutes les opérations. Les réponses ne contiennent ni mot de passe ni empreinte de mot de passe. Les mots de passe utilisent PasswordHasher et la politique existante (12 à 128 caractères, majuscule, minuscule et chiffre).

Un administrateur ne peut pas supprimer son propre compte ni retirer son propre rôle. Le dernier administrateur ne peut pas être supprimé ou rétrogradé, y compris lors de modifications concurrentes : les écritures sont sérialisées par un verrou transactionnel PostgreSQL. Un compte associé à des conversions ne peut pas être supprimé ; l’historique est conservé.

Chaque requête authentifiée contrôle maintenant l’existence du compte et une empreinte de session liée à son identité, son rôle et son mot de passe. Une modification effective invalide ses anciens jetons. Une suppression invalide également l’accès. Les sessions antérieures à cette mise à jour demandent une reconnexion. Aucune migration de schéma n’est nécessaire.

L’inscription publique existante reste disponible et crée uniquement le rôle `User`. Cette évolution n’introduit pas de statut de désactivation, d’invitation par e-mail ou d’envoi de mot de passe. Le mot de passe initial est défini par l’administrateur dans le formulaire.

Validation : compilation frontend/TypeScript, ESLint ciblé et tests .NET dédiés sur un cluster PostgreSQL temporaire distinct de la base de l’application. Le test d’intégration vérifie les accès anonymes/utilisateur/admin, la création, les doublons, les protections de son propre compte, la promotion, le changement de mot de passe, la recherche, la suppression et l’invalidation des sessions. Un scénario Playwright avec API simulée vérifie les formulaires, les modifications, la confirmation de suppression et l’écran d’accès réservé. Les données de test ne sont pas ajoutées à la base réelle.

Résultats de validation : compilation de production frontend et TypeScript réussies ; ESLint ciblé sans erreur ; 55 tests .NET hors intégration réussis ; deux tests dédiés à la gestion des utilisateurs réussis, dont une intégration PostgreSQL réelle. Les six scénarios navigateur existants passent, et le scénario utilisateurs passe après correction de son sélecteur de rôle. L’API locale a été redémarrée : `/health` retourne 200 et `/api/users` retourne 401 sans authentification. Le cluster PostgreSQL temporaire a été arrêté après les tests.
