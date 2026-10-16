'use client';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { X } from 'lucide-react';
import { FormEvent, ReactNode, useEffect, useRef, useState } from 'react';
import Link from 'next/link';
import { CatalogDelete } from './catalog-delete';
import { Bank, Page, request } from '@/services/api';
import { useApi } from '@/services/use-api';
export function BankDialog({
  title,
  children,
  onClose,
  busy = false,
  wide = false,
  className,
  bodyClassName,
  closeButtonLabel,
}: {
  title: string;
  children: ReactNode;
  onClose: () => void;
  busy?: boolean;
  wide?: boolean;
  className?: string;
  bodyClassName?: string;
  closeButtonLabel?: string;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const dialog = ref.current;
    dialog?.showModal();
    return () => dialog?.close();
  }, []);
  return (
    <dialog
      ref={ref}
      aria-label={title}
      onCancel={(e) => {
        e.preventDefault();
        e.stopPropagation();
        if (!busy) onClose();
      }}
      className={`app-dialog ${wide ? 'wide-dialog' : ''} ${className ?? ''}`}
    >
      <div className="dialog-header mb-6 flex items-start justify-between gap-4">
        <h2 className="text-xl font-semibold">{title}</h2>
        <button
          type="button"
          disabled={busy}
          onClick={onClose}
          aria-label={closeButtonLabel}
          className={
            closeButtonLabel
              ? 'icon-button dialog-close disabled:opacity-40'
              : 'rounded-lg border border-current/20 px-3 py-1.5 disabled:opacity-40'
          }
        >
          {closeButtonLabel ? <X size={18} aria-hidden="true" /> : 'Fermer'}
        </button>
      </div>
      <div className={`dialog-body ${bodyClassName ?? ''}`}>{children}</div>
    </dialog>
  );
}
export function BankEditor({
  initial,
  onClose,
  onSaved,
}: {
  initial: Bank | null;
  onClose: () => void;
  onSaved: (bank: Bank) => void;
}) {
  const [name, setName] = useState(initial?.name || '');
  const [code, setCode] = useState(initial?.code || '');
  const [description, setDescription] = useState(initial?.description || '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const input = 'ui-input mt-2';
  async function save(e: FormEvent) {
    e.preventDefault();
    if (busy) return;
    setBusy(true);
    setError('');
    try {
      const saved = await request<Bank>('/banks' + (initial ? '/' + initial.id : ''), {
        method: initial ? 'PUT' : 'POST',
        body: JSON.stringify({
          name: name.trim(),
          code: code.trim(),
          description: description.trim(),
        }),
      });
      onSaved(saved);
      onClose();
    } catch (e) {
      setError((e as Error).message);
      setBusy(false);
    }
  }
  return (
    <BankDialog
      title={initial ? 'Modifier la banque' : 'Ajouter une banque'}
      onClose={onClose}
      busy={busy}
    >
      <form onSubmit={save} className="space-y-5">
        <fieldset disabled={busy} className="space-y-5">
          <label className="block text-sm">
            Nom de la banque *
            <Input
              autoFocus
              required
              maxLength={200}
              className={input}
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Ex. Banque Populaire"
            />
          </label>
          <label className="block text-sm">
            Code unique *
            <Input
              required
              maxLength={30}
              className={input}
              value={code}
              onChange={(e) => setCode(e.target.value.toUpperCase())}
              placeholder="Ex. BP"
            />
            <span className="mt-2 block opacity-70">
              Lettres, chiffres, tirets et underscores. Ce code identifie la banque.
            </span>
          </label>
          <label className="block text-sm">
            Description (facultatif)
            <textarea
              rows={4}
              maxLength={2000}
              className={input}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Notes sur cette banque"
            />
          </label>
          <p className="text-sm opacity-70">Les comptes bancaires sont configurés séparément.</p>
        </fieldset>
        {error && (
          <p role="alert" className="rounded-xl bg-destructive/10 p-3 text-destructive">
            {error}
          </p>
        )}
        <div className="flex justify-end gap-3">
          <Button type="button" disabled={busy} onClick={onClose} variant="outline">
            Annuler
          </Button>
          <Button disabled={busy}>{busy ? 'Enregistrement…' : 'Enregistrer'}</Button>
        </div>
      </form>
    </BankDialog>
  );
}
export function BankDelete({
  bank,
  onClose,
  onDeleted,
}: {
  bank: Bank;
  onClose: () => void;
  onDeleted: () => void;
}) {
  return (
    <CatalogDelete
      resource="banks"
      id={bank.id}
      name={bank.name}
      onClose={onClose}
      onDeleted={onDeleted}
    />
  );
}
type Related = {
  id: string;
  client: string;
  accountName: string;
  accountNumber: string;
  currency: string;
};
export function BankDetails({ bank, onClose }: { bank: Bank; onClose: () => void }) {
  const { data, error, loading, reload } = useApi<Page<Related>>(
    `/banks/${bank.id}/accounts?pageSize=100`,
  );
  return (
    <BankDialog title={bank.name} onClose={onClose}>
      <p className="mb-3 text-sm opacity-70">Code : {bank.code}</p>
      <p>{bank.description || 'Aucune description.'}</p>
      <h3 className="mt-6 font-semibold">Comptes ({bank.accounts})</h3>
      {loading && <p role="status">Chargement…</p>}
      {error && (
        <p role="alert">
          {error} <button onClick={reload}>Réessayer</button>
        </p>
      )}
      {data?.items.map((item) => (
        <div key={item.id} className="mt-3 rounded-xl border p-3">
          <strong>
            {item.accountName} · {item.client}
          </strong>
          <p>
            {item.accountNumber} · {item.currency}
          </p>
        </div>
      ))}
      {data?.items.length === 0 && <p>Aucun compte associé.</p>}
      <Link className="mt-6 block text-[var(--link)]" href="/bank-accounts">
        Gérer les comptes bancaires →
      </Link>
    </BankDialog>
  );
}
