# Catalogue bancaire proposé

Périmètre demandé le 22 octobre 2026 : banques conventionnelles de groupes marocains, avec inclusion explicite de BMCI Maroc. Ce choix fonctionnel ne constitue pas une classification de l’actionnariat de BMCI.

Catalogue proposé (11 banques) : Attijariwafa Bank, Al Barid Bank, Banque Populaire, Bank of Africa / BMCE, BMCI, Crédit Agricole du Maroc, CFG Bank, CIH Bank, Crédit du Maroc, Saham Bank et CDG Capital.

Les banques participatives ne sont plus proposées : Bank Assafa, Al Akhdar Bank, Bank Al Karam, Bank Al Yousr et Umnia Bank.

`MoroccanBankCatalog.SelectableEntries` et `IsSelectable` définissent ce périmètre pour l’initialisation, la liste paginée, le formulaire d’ajout, les nouveaux comptes et les nouveaux imports PDF. Les logos du catalogue sont conservés. Les identités complètes et leurs alias restent dans `Entries` pour lire les anciens PDF.

Aucune suppression des banques en base, comptes, documents ou relevés historiques n’est effectuée. Les comptes historiques gardent leur banque et peuvent être consultés et modifiés sans en changer. Les banques hors périmètre sont masquées dans les nouvelles sélections.

La liste de présence au Maroc peut être consultée auprès de la [SGFG](https://www.sgfg.ma/fr/garantie-des-depots/liste-des-banques-membres). Toute évolution du périmètre demandé doit être répercutée dans `SelectableCodes` et cette note ; les identités historiques doivent rester disponibles.
