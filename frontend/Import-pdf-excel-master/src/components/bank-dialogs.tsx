'use client';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { FormEvent, ReactNode, useEffect, useRef, useState } from 'react';
import Link from 'next/link';
import { CatalogDelete } from './catalog-delete';
import { Bank, Page, request } from '@/services/api';
import { useApi } from '@/services/use-api';


export function BankDialog({ title, children, onClose, busy = false, wide = false }: { title: string; children: ReactNode; onClose: () => void; busy?: boolean; wide?: boolean }) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => { const dialog = ref.current; dialog?.showModal(); return () => dialog?.close(); }, []);
  return <dialog ref={ref} aria-labelledby="bank-dialog-title" onCancel={e => { e.preventDefault(); if (!busy) onClose(); }} className={`app-dialog ${wide ? 'conversion-dialog' : ''}`}>
    <div className="mb-6 flex items-start justify-between gap-4"><h2 id="bank-dialog-title" className="text-xl font-semibold">{title}</h2><button type="button" disabled={busy} onClick={onClose} className="rounded-lg border border-current/20 px-3 py-1.5 disabled:opacity-40">Fermer</button></div>{children}
  </dialog>;
}
export function BankEditor({ initial, onClose, onSaved }: { initial: Bank | null; onClose: () => void; onSaved: (bank: Bank) => void }) {

  const [name, setName] = useState(initial?.name || ''); const [code, setCode] = useState(initial?.code || ''); const [description, setDescription] = useState(initial?.description || '');
  const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  const input = "ui-input mt-2";
  async function save(e: FormEvent) {
    e.preventDefault(); if (busy) return; setBusy(true); setError('');
    try { const saved = await request<Bank>('/banks' + (initial ? '/' + initial.id : ''), { method: initial ? 'PUT' : 'POST', body: JSON.stringify({ name: name.trim(), code: code.trim(), description: description.trim() }) }); onSaved(saved); onClose(); }
    catch (e) { setError((e as Error).message); setBusy(false); }
  }
  return <BankDialog title={initial ? 'Modifier la banque' : 'Ajouter une banque'} onClose={onClose} busy={busy}>
    <form onSubmit={save} className="space-y-5"><fieldset disabled={busy} className="space-y-5">
      <label className="block text-sm">Nom de la banque *<Input autoFocus required maxLength={200} className={input} value={name} onChange={e => setName(e.target.value)} placeholder="Ex. Banque Populaire" /></label>
      <label className="block text-sm">Code unique *<Input required maxLength={30} className={input} value={code} onChange={e => setCode(e.target.value.toUpperCase())} placeholder="Ex. BP" /><span className="mt-2 block opacity-70">Lettres, chiffres, tirets et underscores. Ce code identifie la banque.</span></label>
      <label className="block text-sm">Description (facultatif)<textarea rows={4} maxLength={2000} className={input} value={description} onChange={e => setDescription(e.target.value)} placeholder="Notes sur cette banque" /></label>
      <p className="text-sm opacity-70">Les comptes et les profils de lecture PDF sont configurés séparément.</p>
    </fieldset>{error && <p role="alert" className="rounded-xl bg-destructive/10 p-3 text-destructive">{error}</p>}
      <div className="flex justify-end gap-3"><button type="button" disabled={busy} onClick={onClose} className="rounded-xl border border-current/20 px-4 py-2.5">Annuler</button><Button disabled={busy}>{busy ? 'Enregistrement…' : 'Enregistrer'}</Button></div>
    </form>
  </BankDialog>;
}
export function BankDelete({bank,onClose,onDeleted}:{bank:Bank;onClose:()=>void;onDeleted:()=>void}){return <CatalogDelete resource="banks" id={bank.id} name={bank.name} onClose={onClose} onDeleted={onDeleted}/>;}
type Related = { id: string; client?: string; accountName?: string; accountNumber?: string; currency?: string; name?: string; parserKey?: string; isActive?: boolean };
function RelatedList({ bankId, kind }: { bankId: string; kind: 'accounts' | 'profiles' }) {
  const [page, setPage] = useState(1); const { data, error, loading, reload } = useApi<Page<Related>>(`/banks/${bankId}/${kind}?page=${page}&pageSize=20`);
  return <div className="mt-4 space-y-3">
    {loading && <p role="status">Chargement…</p>}{error && <div role="alert" className="text-destructive">{error} <button onClick={reload}>Réessayer</button></div>}
    {!loading && !error && data?.items.length === 0 && <p className="opacity-70">{kind === 'accounts' ? 'Aucun compte associé.' : 'Aucun profil de lecture configuré.'}</p>}
    {data?.items.map(item => <div className="rounded-xl border border-current/15 p-3 text-sm" key={item.id}>{kind === 'accounts' ? <><p className="font-semibold">{item.accountName} · {item.client}</p><p className="mt-1 opacity-70">{item.accountNumber} · {item.currency}</p></> : <><p className="font-semibold">{item.name}</p><p className="mt-1 opacity-70">{item.isActive ? 'Actif' : 'Inactif'} · {item.parserKey}</p></>}</div>)}
    {(data?.total || 0) > 20 && <div className="flex justify-between"><button disabled={page === 1 || loading} onClick={() => setPage(p => p - 1)}>Précédent</button><span>Page {page}</span><button disabled={page * 20 >= (data?.total || 0) || loading} onClick={() => setPage(p => p + 1)}>Suivant</button></div>}
  </div>;
}
export function BankDetails({ bank, onClose }: { bank: Bank; onClose: () => void }) {
  const [tab, setTab] = useState<'accounts' | 'profiles'>('accounts');
  return <BankDialog title={bank.name} onClose={onClose}>
    <p className="mb-3 text-sm opacity-70">Code : {bank.code} · {bank.status}</p><p className="whitespace-pre-wrap break-words">{bank.description || 'Aucune description.'}</p>
    <div className="mt-6 flex gap-3">{(['accounts', 'profiles'] as const).map(kind => <button key={kind} aria-pressed={tab === kind} className={`rounded-xl border px-4 py-2 ${tab === kind ? 'border-primary bg-primary/15 text-[var(--link)]' : 'border-current/20'}`} onClick={() => setTab(kind)}>{kind === 'accounts' ? `Comptes (${bank.accounts})` : `Profils (${bank.profiles})`}</button>)}</div>
    <RelatedList key={tab} bankId={bank.id} kind={tab} />
    <div className="mt-6 flex flex-wrap gap-4 border-t border-current/15 pt-4 text-sm text-[var(--link)]"><Link href="/bank-accounts">Gérer les comptes bancaires →</Link><Link href="/extraction-profiles">Gérer les profils de lecture →</Link></div>
  </BankDialog>;
}
