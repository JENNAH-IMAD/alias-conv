# Parcours de traitement intégré

Dans l’espace clients, « Configurer et traiter » et « Consulter les étapes du traitement » ouvrent maintenant un panneau directement sous le relevé concerné. La configuration, le résultat et les aperçus de fichiers ne sont plus présentés dans une fenêtre superposée. Les confirmations de suppression et la modification du document restent des dialogues séparés.

Le panneau comporte trois étapes consultables : document importé et compte bancaire ; profil de lecture, format et correspondance des colonnes ; contrôle des opérations et export. Les sections restent montées lorsqu’on change d’étape afin de conserver les corrections locales. Elles ne sont pas enregistrées sur le serveur ; réduire le panneau ou quitter la page les abandonne, comme pour la fermeture de l’ancien dialogue.

Un traitement terminé ne peut pas être relancé depuis ce panneau de consultation. Les anciens dossiers exposent désormais les identifiants des profils associés via la liste des archives. Les paramètres présentés sont ceux des modèles actuels, pas une copie immuable de leur état au moment de la conversion ; cette limite est indiquée à l’écran. Si les identifiants sont absents, aucun modèle arbitraire n’est choisi.

Le contrôle des archives utilise l’aperçu standard existant, limité aux 500 premières lignes ; sa limitation est transmise au composant de contrôle. Les téléchargements d’archive conservent le fichier complet. Le téléchargement CSV corrigé reste local et ne comporte pas d’en-tête.

Validation : deux scénarios Playwright (API simulée) couvrent la consultation des étapes d’une archive, la conservation d’une correction en naviguant entre les étapes, le résultat intégré après traitement, l’absence de dialogue ouvert et la largeur mobile. Compilation API et vérification frontend effectuées.
