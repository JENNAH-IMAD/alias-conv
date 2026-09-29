'use client';
import { can } from '@/lib/permissions';
import { useAuth } from '@/components/auth-context';


import Link from 'next/link';
import { useState } from 'react';
import { ArrowUpRight, Landmark, Pencil, Plus, Trash2, Wallet } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardFooter, CardHeader } from '@/components/ui/card';
import { ResourceEditor, LoadState, Pagination } from '@/components/resource-editor';
import { Account, Page, request } from '@/services/api';
import { useApi } from '@/services/use-api';

export default function BankAccountsPage() {
  const { user } = useAuth();
  const [page, setPage] = useState(1);
  const { data, error, loading, reload } = useApi<Page<Account>>(`/bank-accounts?page=${page}&pageSize=20`);
  const [editing, setEditing] = useState<Account | null | undefined>(undefined);
  const [actionError, setActionError] = useState('');
  const [deleting, setDeleting] = useState<string | null>(null);

  async function remove(item: Account) {
    if (deleting || !confirm(`Supprimer le compte « ${item.accountName} » ?`)) return;
    setActionError(''); setDeleting(item.id);
    try {
      await request(`/bank-accounts/${item.id}`, { method: 'DELETE' });
      if (data?.items.length === 1 && page > 1) setPage(page - 1);
      else reload();
    } catch (e) { setActionError((e as Error).message); }
    finally { setDeleting(null); }
  }

  return <div className="ui-page accounts-page">
    <section className="ui-hero flex flex-wrap items-center justify-between gap-5">
      <div><p className="ui-eyebrow">RÉFÉRENTIEL</p><h1 className="ui-title">Comptes bancaires</h1><p className="ui-description max-w-xl">Vos comptes et leurs paramètres comptables, réunis dans un même espace.</p></div>
      <Button disabled={!can(user, 'accounts.write')} onClick={() => setEditing(null)}><Plus/> Nouveau compte</Button>
    </section>
    <section aria-label="Liste des comptes" className="space-y-5">
      <div className="accounts-section-heading"><div className="flex items-center gap-2.5"><Wallet className="size-4 text-muted-foreground"/><h2 className="text-sm font-semibold">Tous les comptes</h2><Badge variant="secondary">{data?.total ?? '—'}</Badge></div><span className="text-xs text-muted-foreground">Configuration bancaire et comptable</span></div>
      <LoadState error={error || actionError} loading={loading}/>
      {!loading && !error && !data?.items.length && <div className="accounts-empty"><span className="rounded-2xl bg-muted p-4 text-primary"><Landmark className="size-8"/></span><h3 className="mt-5 font-semibold">Ajoutez votre premier compte</h3><p className="mt-2 max-w-sm text-sm leading-6 text-muted-foreground">Associez un client à sa banque pour préparer l’import de ses relevés.</p><Button variant="edit" className="mt-5" disabled={!can(user, 'accounts.write')} onClick={() => setEditing(null)}><Plus/> Ajouter un compte bancaire</Button></div>}
      <div className="grid gap-5 xl:grid-cols-2">{data?.items.map(account => <Card key={account.id} className="account-card gap-5 overflow-hidden shadow-none">
        <CardHeader className="flex items-start gap-3.5"><span className="account-bank-icon"><Landmark className="size-5" strokeWidth={1.7}/></span><div className="min-w-0 flex-1"><h3 className="break-words text-base font-semibold leading-6">{account.accountName}</h3><p className="mt-1 break-words text-xs text-muted-foreground">{account.bank}</p></div><Badge variant="outline" className="shrink-0">{account.currency}</Badge></CardHeader>
        <CardContent className="space-y-5"><div className="account-number"><p className="text-[10px] font-medium uppercase tracking-widest text-muted-foreground">Numéro de compte</p><p className="mt-2 break-all font-mono text-sm font-medium tracking-wide">{account.accountNumber}</p></div><dl className="grid grid-cols-2 gap-4"><div><dt className="text-xs text-muted-foreground">Journal comptable</dt><dd className="mt-2 text-sm font-medium">{account.journal || 'Non renseigné'}</dd></div><div className="border-l border-border pl-5"><dt className="text-xs text-muted-foreground">Compte comptable</dt><dd className="mt-2 font-mono text-sm font-medium">{account.accountCode || 'Non renseigné'}</dd></div></dl></CardContent>
        <CardFooter className="flex flex-wrap justify-between gap-3 border-t border-border pt-4"><Button asChild variant="ghost" size="sm" className="text-[var(--link)]"><Link href={`/conversions?clientId=${encodeURIComponent(account.clientId)}`}>Importer un relevé <ArrowUpRight/></Link></Button><div className="flex gap-2"><Button variant="edit" size="sm" disabled={!can(user, 'accounts.write')} onClick={() => setEditing(account)}><Pencil/> Modifier</Button><Button variant="ghost" size="icon-sm" className="text-destructive hover:bg-destructive/10 hover:text-destructive" disabled={deleting !== null || !can(user, 'accounts.write')} aria-label={`Supprimer ${account.accountName}`} title="Supprimer le compte" onClick={() => void remove(account)}><Trash2/></Button></div></CardFooter>
      </Card>)}</div>
      {!loading && !!data?.total && <div className="border-t border-border pt-2"><Pagination page={page} total={data.total} onChange={setPage}/></div>}
    </section>
    {editing !== undefined && <ResourceEditor resource="bank-accounts" initial={editing || undefined} onClose={() => setEditing(undefined)} onSaved={reload}/>}
  </div>;
}
