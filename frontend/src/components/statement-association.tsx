'use client';
import { useState } from 'react';
import Link from 'next/link';
import { FolderOpen, Pencil } from 'lucide-react';
import { Account, Client, request } from '@/services/api';
import { useAll } from '@/services/use-api';
import { StatementDetail, accountSuffix } from '@/services/statement-exports';
import { Button } from './ui/button';
export function StatementAssociation({
  detail,
  onSaved,
  disabled,
}: {
  detail: StatementDetail;
  onSaved: () => void;
  disabled?: boolean;
}) {
  const clients = useAll<Client>('/clients');
  const accounts = useAll<Account>('/bank-accounts');
  const [editing, setEditing] = useState(false);
  const [clientId, setClientId] = useState(detail.clientId ?? '');
  const [accountId, setAccountId] = useState(detail.bankAccountId ?? '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const client = clients.data?.find((c) => c.id === detail.clientId);
  const account = accounts.data?.find((a) => a.id === detail.bankAccountId);
  async function save(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError('');
    try {
      await request(`/statement-imports/${detail.id}/association`, {
        method: 'PUT',
        body: JSON.stringify({ version: detail.version, clientId, bankAccountId: accountId }),
      });
      setEditing(false);
      onSaved();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <div className="workspace-identity">
        <FolderOpen size={18} />
        <div className="flex-1">
          <strong>
            {client?.name || (detail.clientId ? 'Client associé' : 'Relevé à classer')}
          </strong>
          <span className="muted">
            {account
              ? `${account.accountName} · ${accountSuffix(account.accountNumber)}`
              : 'Conservé dans l’espace clients · associez-le au client et à son compte.'}
          </span>
        </div>
        <Button asChild size="sm" variant="outline">
          <Link href={`/client-workspace${detail.clientId ? `?clientId=${detail.clientId}` : ''}`}>
            Espace client
          </Link>
        </Button>
        <Button
          permission="documents.write"
          disabled={disabled || clients.loading || accounts.loading}
          size="sm"
          variant="outline"
          onClick={() => setEditing(true)}
        >
          <Pencil size={14} />
          {detail.clientId ? 'Modifier' : 'Classer'}
        </Button>
      </div>
      {(clients.error || accounts.error) && (
        <p role="alert" className="text-destructive text-xs mt-2">
          {clients.error || accounts.error}
        </p>
      )}
      {editing && (
        <section className="ui-card mt-4">
          <h3 className="font-semibold mb-4">Classer le relevé dans un dossier client</h3>
          <p className="muted text-sm mb-4">
            5 relevés maximum par compte. Au 6e ajout, le plus ancien est supprimé définitivement et
            sa trace est conservée.
          </p>
          <form className="space-y-5" onSubmit={save}>
            <p className="muted text-sm">
              Le journal et le compte comptable seront repris si ces champs sont vides. Vérifiez
              ensuite le relevé avant de le valider à nouveau.
            </p>
            <label className="block text-sm">
              Client
              <select
                aria-label="Client"
                required
                disabled={busy}
                className="ui-input mt-2"
                value={clientId}
                onChange={(e) => {
                  setClientId(e.target.value);
                  setAccountId('');
                }}
              >
                <option value="">Choisir le client…</option>
                {clients.data?.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="block text-sm">
              Compte bancaire
              <select
                aria-label="Compte bancaire"
                required
                disabled={busy || !clientId}
                className="ui-input mt-2"
                value={accountId}
                onChange={(e) => setAccountId(e.target.value)}
              >
                <option value="">Choisir le compte…</option>
                {accounts.data
                  ?.filter(
                    (a) =>
                      a.clientId === clientId &&
                      a.currency === detail.statement.currency &&
                      a.bank === detail.statement.bankName,
                  )
                  .map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.accountName} · {a.accountNumber}
                    </option>
                  ))}
              </select>
            </label>
            {error && (
              <p role="alert" className="conversion-alert error">
                {error}
              </p>
            )}
            <div className="flex justify-end gap-3">
              <Button
                type="button"
                disabled={busy}
                variant="outline"
                onClick={() => setEditing(false)}
              >
                Annuler
              </Button>
              <Button disabled={busy || !accountId}>Enregistrer</Button>
            </div>
          </form>
        </section>
      )}
    </>
  );
}
