'use client';
import { useState } from 'react';
import { Building2, Landmark, Plus, Trash2, Users, Wallet } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { BankAccountWallet } from '@/components/bank-account-wallet';
import { ResourceEditor } from '@/components/resource-editor';
import { LoadState } from '@/components/ui/load-state';
import { Pagination } from '@/components/ui/pagination';
import { Account, Client, request } from '@/services/api';
import { useAll } from '@/services/use-api';
import { PageHeader } from '@/components/page-header';
import { BankingSectionNavigation } from '@/components/banking-section-navigation';
import { BankDialog } from '@/components/bank-dialogs';
export default function BankAccountsPage() {
  const [page, setPage] = useState(1);
  const { data: accounts, error: accountsError, loading, reload } = useAll<Account>('/bank-accounts');
  const { data: clients, error: clientsError } = useAll<Client>('/clients');
  const [selectedClientId, setSelectedClientId] = useState('');
  const filteredAccounts = (accounts ?? []).filter(
    (account) => !selectedClientId || account.clientId === selectedClientId,
  );
  const pageSize = 20;
  const pageAccounts = filteredAccounts.slice((page - 1) * pageSize, page * pageSize);
  const error = accountsError || clientsError;
  const [editing, setEditing] = useState<Account | null | undefined>(undefined);
  const [actionError, setActionError] = useState('');
  const [deleting, setDeleting] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Account | null>(null);
  async function remove(item: Account) {
    if (deleting) return;
    setActionError('');
    setDeleting(item.id);
    try {
      await request(`/bank-accounts/${item.id}`, { method: 'DELETE' });
      setDeleteTarget(null);
      if (pageAccounts.length === 1 && page > 1) setPage(page - 1);
      reload();
    } catch (e) {
      setActionError((e as Error).message);
    } finally {
      setDeleting(null);
    }
  }
  return (
    <div className="ui-page accounts-page">
      <BankingSectionNavigation active="accounts" />
      <PageHeader
        icon={Wallet}
        eyebrow="Référentiel"
        title="Comptes bancaires"
        description="Parcourez vos comptes, retournez une carte et retrouvez ses paramètres en un geste."
        actions={
          <Button permission="accounts.write" onClick={() => setEditing(null)}>
            <Plus /> Nouveau compte
          </Button>
        }
      />
      <section aria-label="Liste des comptes" className="accounts-content space-y-5">
        <div className="flex flex-wrap items-end justify-between gap-4 rounded-2xl border border-border bg-card p-4 shadow-sm sm:p-5">
          <label className="grid w-full max-w-md gap-2 text-sm font-medium" htmlFor="account-client-filter">
            <span className="flex items-center gap-2 text-foreground">
              <Users className="size-4 text-muted-foreground" />
              Filtrer par client
            </span>
            <span className="relative">
              <select
                id="account-client-filter"
                className="h-11 w-full appearance-none rounded-lg border border-input bg-background px-3 pr-10 text-sm text-foreground shadow-sm outline-none transition focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/30"
                value={selectedClientId}
                onChange={(event) => {
                  setSelectedClientId(event.target.value);
                  setPage(1);
                }}
              >
                <option value="">Tous les clients</option>
                {[...(clients ?? [])].sort((a, b) => a.name.localeCompare(b.name, 'fr')).map((client) => (
                  <option key={client.id} value={client.id}>{client.name}</option>
                ))}
              </select>
              <Building2 className="pointer-events-none absolute right-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            </span>
          </label>
          <p className="text-xs text-muted-foreground">
            {selectedClientId ? 'Comptes du client sélectionné' : 'Comptes de tous les clients'}
          </p>
        </div>
        <div className="accounts-section-heading">
          <div className="flex items-center gap-2.5">
            <Wallet className="size-4 text-muted-foreground" />
            <h2 className="text-sm font-semibold">{selectedClientId ? 'Comptes du client' : 'Tous les comptes'}</h2>
            <Badge variant="secondary">{filteredAccounts.length}</Badge>
          </div>
          <span className="text-xs text-muted-foreground">Configuration bancaire et comptable</span>
        </div>
        <LoadState error={error || actionError} loading={loading} />
        {!loading && !error && !filteredAccounts.length && (
          <div className="accounts-empty">
            <span className="rounded-2xl bg-muted p-4 text-primary">
              <Landmark className="size-8" />
            </span>
            <h3 className="mt-5 font-semibold">Ajoutez votre premier compte</h3>
            <p className="mt-2 max-w-sm text-sm leading-6 text-muted-foreground">
              {selectedClientId ? 'Aucun compte bancaire n’est associé à ce client.' : 'Associez un client à sa banque pour préparer l’import de ses relevés.'}
            </p>
            <Button
              permission="accounts.write"
              variant="edit"
              className="mt-5"
              onClick={() => setEditing(null)}
            >
              <Plus /> Ajouter un compte bancaire
            </Button>
          </div>
        )}
        {!!pageAccounts.length && (
          <BankAccountWallet
            accounts={pageAccounts}
            onEdit={setEditing}
            onDelete={(account) => {
              setActionError('');
              setDeleteTarget(account);
            }}
            onNew={() => setEditing(null)}
            deleting={deleting !== null}
          />
        )}
        {!loading && filteredAccounts.length > pageSize && (
          <Pagination page={page} total={filteredAccounts.length} loading={loading} onChange={setPage} />
        )}
      </section>
      {deleteTarget && (
        <BankDialog
          title="Supprimer le compte bancaire"
          busy={!!deleting}
          onClose={() => setDeleteTarget(null)}
        >
          <p>
            Supprimer le compte <strong>{deleteTarget.accountName}</strong> ?
          </p>
          <p className="muted text-sm mt-2">
            {deleteTarget.bank} · {deleteTarget.accountNumber}
          </p>
          {actionError && (
            <p role="alert" className="conversion-alert error mt-4">
              {actionError}
            </p>
          )}
          <div className="dialog-form-actions">
            <Button variant="outline" disabled={!!deleting} onClick={() => setDeleteTarget(null)}>
              Annuler
            </Button>
            <Button
              variant="danger"
              disabled={!!deleting}
              onClick={() => void remove(deleteTarget)}
            >
              <Trash2 size={15} />
              {deleting ? 'Suppression…' : 'Supprimer le compte'}
            </Button>
          </div>
        </BankDialog>
      )}
      {editing !== undefined && (
        <ResourceEditor
          resource="bank-accounts"
          initial={editing || undefined}
          onClose={() => setEditing(undefined)}
          onSaved={reload}
        />
      )}
    </div>
  );
}
