export type StatementAttribution = {
  bank: string; accountName?: string | null; accountNumber?: string | null; account?: string;
  processedByName?: string | null; processedAt?: string | null;
  modifiedByName?: string | null; modifiedAt?: string | null;
};

export function StatementDetails({ item }: { item: StatementAttribution }) {
  const date = (value?: string | null) => value ? ` · ${new Date(value).toLocaleString('fr-FR')}` : '';
  return <dl className="grid gap-3 rounded-xl border border-border bg-muted/30 p-4 text-xs sm:grid-cols-2">
    <div><dt className="text-muted-foreground">Banque</dt><dd className="mt-1 break-words font-medium">{item.bank}</dd></div>
    <div><dt className="text-muted-foreground">Compte bancaire</dt><dd className="mt-1 break-words font-medium">{item.accountName || 'Nom non renseigné'}{(item.accountNumber || item.account) && <span className="block font-normal text-muted-foreground">{item.accountNumber || item.account}</span>}</dd></div>
    <div><dt className="text-muted-foreground">Dernier traitement réalisé par</dt><dd className="mt-1 break-words font-medium">{item.processedByName || 'Non renseigné'}{date(item.processedAt)}</dd></div>
    <div><dt className="text-muted-foreground">Dernière modification par</dt><dd className="mt-1 break-words font-medium">{item.modifiedByName || 'Non renseigné'}{date(item.modifiedAt)}</dd></div>
  </dl>;
}
