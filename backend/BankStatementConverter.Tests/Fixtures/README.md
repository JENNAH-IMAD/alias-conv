# Fixture fictive

`fictional-statement.pdf` contient deux lignes de données techniques fictives, sans données de banque ou de client réels :

```text
01/05/2026;Virement fictif;;1 250,50;1 250,50;REF001
02/05/2026;Frais fictifs;25,50;;1 225,00;REF002
```

Profil : séparateur `;`, date `dd/MM/yyyy`, culture `fr-FR`, aucune ligne ignorée, indices date 0, libellé 1, débit 2, crédit 3, solde 4, référence 5. Le seed `Seed__DemoData=true` crée ce profil dans une banque explicitement fictive.
