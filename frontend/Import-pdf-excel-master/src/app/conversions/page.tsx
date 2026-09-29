'use client';

import Link from 'next/link';
import { useEffect, useRef, useState } from 'react';
import { ArrowRight, CheckCircle2, FileText, FileUp, Landmark, LoaderCircle, Upload, Users, X } from 'lucide-react';
import { ConversionSteps } from '@/components/conversion-review';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { all, Account, Client, History, request } from '@/services/api';
import { validatePdf } from '@/lib/pdf-validation';
import { cn } from '@/lib/utils';

export default function ConversionsPage() {
  const [clients, setClients] = useState<Client[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [clientId, setClientId] = useState('');
  const [accountId, setAccountId] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [result, setResult] = useState<History | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [fileError, setFileError] = useState('');
  const [dragging, setDragging] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);
  const submitting = useRef(false);
  const availableAccounts = accounts.filter(account => account.clientId === clientId);

  useEffect(() => {
    let active = true;
    Promise.all([all<Client>('/clients'), all<Account>('/bank-accounts')])
      .then(([c, a]) => {
        if (!active) return;
        setClients(c); setAccounts(a);
        const selected = new URLSearchParams(window.location.search).get('clientId');
        if (c.some(client => client.id === selected)) setClientId(selected!);
      })
      .catch(e => { if (active) setError(e.message); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  function selectFile(selected: File | null) {
    if (busy || loading || submitting.current) return;
    setResult(null); setError('');
    const validation = selected ? validatePdf(selected) : null;
    setFileError(validation || '');
    setFile(validation ? null : selected);
    if (fileInput.current) fileInput.current.value = '';
  }

  async function upload() {
    if (!file || !clientId || !availableAccounts.some(account => account.id === accountId) || submitting.current || result) return;
    const validation = validatePdf(file);
    if (validation) { setFileError(validation); return; }
    submitting.current = true;
    setBusy(true); setError('');
    try {
      const form = new FormData();
      form.append('clientId', clientId); form.append('bankAccountId', accountId); form.append('file', file);
      setResult(await request<History>('/documents', { method: 'POST', body: form }));
    } catch (e) { setError((e as Error).message); }
    finally { submitting.current = false; setBusy(false); }
  }

  return <div className="ui-page">
    <section className="ui-hero flex flex-wrap items-start justify-between gap-4">
      <div><p className="ui-eyebrow">CONVERSIONS</p><h1 className="ui-title">Un relevé. Trois étapes.</h1><p className="ui-description">Importez votre PDF, vérifiez les opérations et préparez votre export comptable.</p></div>
      <Badge variant="outline" className="gap-2 px-3 py-1.5"><FileText className="size-3.5"/> PDF vers TXT, CSV ou Excel</Badge>
    </section>
    <ConversionSteps step={0}/>
    <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1fr)_300px]">
      <Card className="overflow-hidden">
        <CardHeader className="border-b border-border pb-6"><CardTitle className="flex items-center gap-2"><FileUp className="size-5 text-primary"/> Importer un relevé PDF</CardTitle><CardDescription>Associez le document à un client et à son compte bancaire.</CardDescription></CardHeader>
        <CardContent>
          <form onSubmit={event => { event.preventDefault(); void upload(); }} aria-busy={busy}>
            <fieldset disabled={loading || busy} className="space-y-6">
              <div className="grid gap-5 sm:grid-cols-2">
                <label htmlFor="import-client" className="block"><span className="mb-2 flex items-center gap-2 text-xs font-medium text-muted-foreground"><Users className="size-4"/> Client</span><select id="import-client" required className="ui-input" value={clientId} onChange={e => { setClientId(e.target.value); setAccountId(''); setResult(null); }}><option value="">Choisir un client</option>{clients.map(c => <option key={c.id} value={c.id}>{c.name} — {c.legalName}</option>)}</select></label>
                <label htmlFor="import-account" className="block"><span className="mb-2 flex items-center gap-2 text-xs font-medium text-muted-foreground"><Landmark className="size-4"/> Compte bancaire</span><select id="import-account" required disabled={!clientId} className="ui-input" value={accountId} onChange={e => { setAccountId(e.target.value); setResult(null); }}><option value="">{clientId ? 'Choisir un compte' : 'Sélectionnez d’abord un client'}</option>{availableAccounts.map(a => <option key={a.id} value={a.id}>{a.bank} · {a.accountNumber} ({a.currency})</option>)}</select></label>
              </div>
              {!loading && !error && !clients.length && <p className="text-sm text-muted-foreground">Aucun client disponible. <Link href="/clients" className="font-medium text-primary underline">Créer un client</Link></p>}
              {clientId && !availableAccounts.length && <p className="text-sm text-muted-foreground">Ce client n’a pas encore de compte. <Link href="/bank-accounts" className="font-medium text-primary underline">Ajouter un compte bancaire</Link></p>}
              <div onDragOver={e => { e.preventDefault(); if (!busy && !loading) setDragging(true); }} onDragLeave={e => { if (!e.currentTarget.contains(e.relatedTarget as Node)) setDragging(false); }} onDrop={e => { e.preventDefault(); setDragging(false); if (busy || loading) return; if (e.dataTransfer.files.length !== 1) { setFileError('Déposez un seul relevé PDF à la fois.'); setFile(null); setResult(null); return; } selectFile(e.dataTransfer.files[0]); }} className={cn('flex min-h-64 flex-col items-center justify-center rounded-xl border-2 border-dashed bg-muted/40 p-6 text-center transition-colors', dragging ? 'border-primary bg-primary/10' : 'border-border', fileError && 'border-destructive/60')}>
                <span className="mb-4 rounded-2xl border border-border bg-card p-4 text-primary"><Upload className="size-7" strokeWidth={1.5}/></span>
                <h2 className="text-base font-semibold">Glissez votre relevé ici</h2><p id="pdf-help" className="mt-2 text-sm text-muted-foreground">Un fichier PDF · 20 Mo maximum</p>
                <input ref={fileInput} id="pdf-file" aria-label="Relevé PDF" aria-describedby={fileError ? 'pdf-help pdf-error' : 'pdf-help'} aria-invalid={!!fileError} type="file" accept="application/pdf,.pdf" className="sr-only" tabIndex={-1} onChange={e => selectFile(e.target.files?.[0] || null)}/>
                <Button type="button" variant="outline" className="mt-5" onClick={() => fileInput.current?.click()}>Parcourir les fichiers</Button>
              </div>
              {file && <div className="flex items-center gap-3 rounded-xl border border-border bg-muted/40 p-4" role="status"><FileText className="size-8 shrink-0 text-primary"/><div className="min-w-0 flex-1"><p className="truncate text-sm font-medium" title={file.name}>{file.name}</p><p className="mt-1 text-xs text-muted-foreground">{(file.size / 1024 / 1024).toLocaleString('fr-FR', { maximumFractionDigits: 2 })} Mo · Prêt à importer</p></div><Button type="button" variant="ghost" size="icon" aria-label="Retirer le fichier" onClick={() => selectFile(null)}><X/></Button></div>}
              {fileError && <p id="pdf-error" role="alert" className="text-sm text-destructive">{fileError}</p>}
              <div className="flex flex-wrap items-center justify-between gap-4 border-t border-border pt-5"><p className="max-w-xs text-xs leading-5 text-muted-foreground">Le profil de lecture et le format d’export seront choisis à l’étape suivante.</p><Button disabled={!file || !clientId || !accountId || busy || !!result} type="submit">{busy ? <LoaderCircle className="animate-spin"/> : <FileUp/>}{busy ? 'Importation…' : 'Importer le relevé'}</Button></div>
            </fieldset>
          </form>
          {loading && <p className="mt-4 text-sm text-muted-foreground" role="status">Chargement des clients et des comptes…</p>}
          {error && <Alert variant="destructive" className="mt-5"><AlertTitle>Importation indisponible</AlertTitle><AlertDescription>{error}</AlertDescription></Alert>}
        </CardContent>
      </Card>
      <aside className="space-y-4"><Card className="bg-muted/30 shadow-none"><CardHeader><Badge variant="secondary" className="w-fit">AVANT DE COMMENCER</Badge><CardTitle className="mt-2">Un import bien préparé</CardTitle></CardHeader><CardContent className="space-y-5 text-sm leading-6 text-muted-foreground"><p>Choisissez un <strong className="text-foreground">PDF texte</strong> contenant le relevé du compte sélectionné.</p><p>Les documents scannés et les photos ne sont pas pris en charge par le profil automatique.</p><div className="border-t border-border pt-4"><p className="font-medium text-foreground">Et après l’import ?</p><p className="mt-2">Votre PDF apparaît dans l’espace client avec le statut « En attente de traitement ».</p></div><Button asChild variant="outline" className="w-full"><Link href="/client-workspace">Consulter les documents <ArrowRight/></Link></Button></CardContent></Card></aside>
    </div>
    {result && <Alert className="border-success/30 bg-success/5" role="status"><CheckCircle2 className="text-[var(--success-text)]"/><AlertTitle>Votre relevé a bien été importé</AlertTitle><AlertDescription><p className="break-all">{result.inputFileName} est enregistré et attend son traitement.</p><Button asChild className="mt-3"><Link href={`/client-workspace?clientId=${result.clientId}`}>Ouvrir l’espace client <ArrowRight/></Link></Button></AlertDescription></Alert>}
  </div>;
}


