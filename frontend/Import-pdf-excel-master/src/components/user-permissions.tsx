'use client';

import { useState } from 'react';
import { ShieldCheck } from 'lucide-react';
import { BankDialog } from './bank-dialogs';
import { Button } from './ui/button';
import { User, request } from '@/services/api';
import { useApi } from '@/services/use-api';

type Definition = { key: string; group: string; label: string; requires: string[] };
type Catalog = { items: Definition[]; defaults: string[] };

export function UserPermissions({ user, onClose, onSaved }: { user: User; onClose: () => void; onSaved: () => void }) {
  const { data, loading, error: loadError, reload } = useApi<Catalog>('/users/permissions');
  const [selection, setSelection] = useState<string[] | null>(user.permissions ?? null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const admin = user.role === 'Admin';
  const selected = admin ? data?.items.map(p => p.key) ?? [] : selection ?? data?.defaults ?? [];
  function toggle(key: string, checked: boolean) {
    if (!data) return;
    const next = new Set(selected);
    function add(value: string) {
      if (next.has(value)) return;
      next.add(value);
      data!.items.find(p => p.key === value)?.requires.forEach(add);
    }
    if (checked) add(key);
    else {
      next.delete(key);
      // Removing a prerequisite also removes dependent permissions, transitively.
      let changed = true;
      while (changed) {
        changed = false;
        for (const p of data.items) if (next.has(p.key) && p.requires.some(required => !next.has(required))) { next.delete(p.key); changed = true; }
      }
    }
    setSelection([...next]);
  }
  async function save() {
    if (busy || !data || admin) return;
    setBusy(true); setError('');
    try {
      await request(`/users/${user.id}/permissions`, { method: 'PUT', body: JSON.stringify({ permissions: selected }) });
      onSaved(); onClose();
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  return <BankDialog title={`Permissions — ${user.name}`} onClose={onClose} busy={busy}>
    <p className="mb-5 flex items-start gap-2 text-sm leading-6 text-muted-foreground"><ShieldCheck className="mt-1 size-4 shrink-0"/>{admin ? 'Les administrateurs disposent de toutes les permissions. Ces droits ne peuvent pas être retirés.' : 'Les droits de consultation nécessaires sont activés automatiquement. Toute modification impose une nouvelle connexion à cet utilisateur.'}</p>
    {loading && <p role="status">Chargement des permissions…</p>}
    {loadError && <p role="alert" className="text-destructive">{loadError} <Button variant="outline" onClick={reload}>Réessayer</Button></p>}
    {data && <>
      <fieldset disabled={busy || admin} className="space-y-4">
        {!admin && <div className="flex flex-wrap gap-2"><Button type="button" size="sm" variant="outline" onClick={() => setSelection(data.items.map(p => p.key))}>Tout autoriser</Button><Button type="button" size="sm" variant="outline" onClick={() => setSelection([])}>Tout retirer</Button><Button type="button" size="sm" variant="outline" onClick={() => setSelection(data.defaults)}>Droits par défaut</Button></div>}
        <div className="grid gap-3 sm:grid-cols-2">{[...new Set(data.items.map(p => p.group))].map(group => <fieldset key={group} className="rounded-xl border border-border bg-card p-4"><legend className="px-1 text-sm font-semibold">{group}</legend><div className="space-y-3">{data.items.filter(p => p.group === group).map(p => <label key={p.key} className="flex items-start gap-3 text-sm leading-5"><input type="checkbox" className="mt-1 size-4 shrink-0 accent-primary" checked={selected.includes(p.key)} onChange={e => toggle(p.key, e.target.checked)} aria-label={`${group} : ${p.label}`}/><span>{p.label}</span></label>)}</div></fieldset>)}</div>
      </fieldset>
      {!admin && selected.length === 0 && <p className="mt-4 text-sm text-muted-foreground">Aucun accès métier : seuls le profil et les préférences resteront accessibles.</p>}
    </>}
    {error && <p role="alert" className="mt-4 text-sm text-destructive">{error}</p>}
    <div className="mt-6 flex justify-end gap-3"><Button variant="outline" disabled={busy} onClick={onClose}>{admin ? 'Fermer' : 'Annuler'}</Button>{!admin && <Button disabled={busy || !data} onClick={() => void save()}>{busy ? 'Enregistrement…' : 'Enregistrer les permissions'}</Button>}</div>
  </BankDialog>;
}
