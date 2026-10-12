'use client';
import { Button } from '@/components/ui/button';
import Link from 'next/link';
import { useEffect, useState } from 'react';
import { BankDialog } from './bank-dialogs';
import { request } from '@/services/api';
type Preview = {
  name: string;
  accounts: number;
  documents: number;
  files: number;
  version: string;
};
export function CatalogDelete({
  resource,
  id,
  name,
  onClose,
  onDeleted,
}: {
  resource: 'banks' | 'clients';
  id: string;
  name: string;
  onClose: () => void;
  onDeleted: () => void;
}) {
  const [preview, setPreview] = useState<Preview | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const path = `/catalog-deletions/${resource}/${id}`;
  useEffect(() => {
    let active = true;
    request<Preview>(path)
      .then((p) => {
        if (active) setPreview(p);
      })
      .catch((e) => {
        if (active) setError(e.message);
      });
    return () => {
      active = false;
    };
  }, [path]);
  async function remove() {
    if (!preview || !confirmed || busy) return;
    setBusy(true);
    setError('');
    try {
      await request(path, { method: 'POST', body: JSON.stringify({ version: preview.version }) });
      onDeleted();
      onClose();
    } catch (e) {
      setError((e as Error).message);
      setConfirmed(false);
    } finally {
      setBusy(false);
    }
  }
  return (
    <BankDialog
      title={resource === 'banks' ? 'Supprimer la banque' : 'Supprimer le client'}
      onClose={onClose}
      busy={busy}
    >
      <p className="font-semibold mb-4">{preview?.name || name}</p>
      {!preview && !error && <p role="status">Vérification des éléments liés…</p>}
      {preview && (
        <>
          <p className="ui-description">
            Cette suppression est définitive. Voici les éléments qui seront supprimés avec{' '}
            {resource === 'banks' ? 'la banque' : 'le client'} :
          </p>
          <div className="grid grid-cols-2 gap-3 my-5">
            {[
              ['Comptes bancaires', preview.accounts],
              ['Documents PDF', preview.documents],
              ['Fichiers PDF', preview.files],
            ].map(([label, count]) => (
              <div className="ui-summary" key={label}>
                <strong className="text-xl">{count}</strong>
                <p className="muted text-xs mt-1">{label}</p>
              </div>
            ))}
          </div>
          <p className="ui-description">
            {resource === 'banks'
              ? 'Les clients sont conservés ; seuls les éléments liés à cette banque sont concernés.'
              : 'Les banques sont conservées.'}{' '}
            Les fichiers seront effacés automatiquement après la suppression.
          </p>
          <label className="flex items-start gap-3 mt-5 text-sm">
            <input
              type="checkbox"
              checked={confirmed}
              disabled={busy}
              onChange={(e) => setConfirmed(e.target.checked)}
              className="mt-1"
            />
            Je confirme la suppression définitive de cet élément et de toutes les données indiquées
            ci-dessus.
          </label>
          <div className="flex flex-wrap gap-3 mt-4">
            {resource === 'clients' && (
              <Link
                className="button-secondary"
                href={`/client-workspace?clientId=${id}`}
                onClick={onClose}
              >
                Consulter les relevés
              </Link>
            )}
          </div>
        </>
      )}
      {error && (
        <p role="alert" className="danger-button mt-4">
          {error}
        </p>
      )}
      <div className="flex justify-end gap-3 mt-6">
        <Button variant="outline" disabled={busy} onClick={onClose}>
          Annuler
        </Button>
        <Button
          variant="outline"
          className="danger-button"
          disabled={!preview || !confirmed || busy}
          onClick={remove}
        >
          {busy ? 'Suppression…' : 'Supprimer définitivement'}
        </Button>
      </div>
    </BankDialog>
  );
}
