'use client';
import { can } from '@/lib/permissions';
import { useAuth } from '@/components/auth-context';


import { StatementDetails } from '@/components/statement-details';
import { ExportDialog, ExportRequest } from '@/components/export-dialog';
import { exportName } from '@/lib/save-export';
import dynamic from 'next/dynamic';
const TreatmentPanel = dynamic(() => import('@/components/treatment-panel').then(m => m.TreatmentPanel), { loading: () => <p role="status" className="p-4 text-muted-foreground">Chargement du traitement…</p> });
import { ClientAvatar } from '@/components/client-photo';
import { DocumentPreview, ManageDocument, WorkspaceDocument } from '@/components/document-actions';
import { LoadState } from '@/components/resource-editor';
import { Button } from '@/components/ui/button';
import { all, exportBlob, Client } from '@/services/api';
import { useApi } from '@/services/use-api';
import { Archive, Download, Eye, FileSpreadsheet, FileText, Pencil, Plus, RefreshCw, Trash2 } from 'lucide-react';
import Link from 'next/link';
import { useEffect, useRef, useState } from 'react';

type ClientArchive = WorkspaceDocument;
type PreviewKind = 'original' | 'standard' | 'export';

const statusLabel: Record<string, string> = {
  Pending: 'En attente de traitement',
  Processing: 'En cours',
  Completed: 'Termine',
  Failed: 'Echec',
};

function Archives({ client }: { client: Client }) {
  const { user } = useAuth();
  const [management, setManagement] = useState<{ item: ClientArchive; remove: boolean } | null>(null);
  const [processing, setProcessing] = useState<ClientArchive | null>(null);
  const [preview, setPreview] = useState<{ item: ClientArchive; kind: PreviewKind } | null>(null);
  const { data, error, loading, reload } = useApi<ClientArchive[]>(`/client-workspace/${client.id}/archives?includePending=true`, true);
  const previewRef = useRef<HTMLDivElement>(null);
  const [visibleCount, setVisibleCount] = useState(20);
  useEffect(() => {
    if (!preview) return;
    const node = previewRef.current;
    node?.scrollIntoView({ block: 'start', behavior: 'instant' });
    node?.focus({ preventScroll: true });
  }, [preview]);
  const [exportRequest, setExportRequest] = useState<ExportRequest | null>(null);
  const [saveNotice, setSaveNotice] = useState('');

  useEffect(() => {
    if (!data?.some(item => item.status === 'Processing')) return;
    const timer = setTimeout(reload, 3000);
    return () => clearTimeout(timer);
  }, [data, reload]);

  function download(item: ClientArchive, kind: PreviewKind, excel = false) {
    const extension = excel ? 'xlsx' : kind === 'original' ? 'pdf' : kind === 'standard' ? 'csv' : item.outputFileName?.split('.').pop() || 'csv';
    setExportRequest({ name: exportName(item.bank, item.accountName, item.id, extension, item.completedAt || item.createdAt), getBlob: () => exportBlob(`/client-workspace/${client.id}/archives/${item.id}/files/${kind}${excel ? '?excel=true' : ''}`) });
  }

  return (
    <>
      {exportRequest && <ExportDialog request={exportRequest} onClose={() => setExportRequest(null)} onSaved={setSaveNotice}/>}
      {saveNotice && <p role="status" className="rounded-xl border border-border bg-card p-4 text-sm">{saveNotice}</p>}
      {management && <ManageDocument item={management.item} remove={management.remove} onClose={() => setManagement(null)} onSaved={reload} />}



      <section className="ui-card flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-center gap-4">
          <ClientAvatar client={client} />
          <div>
            <h2 className="text-lg font-semibold">{client.name}</h2>
            <p className="muted text-xs">{client.legalName} · {data?.filter(item => item.status === 'Completed').length ?? '-'} / 5 traitements archives</p>
          </div>
        </div>
        <div className="flex gap-2">
          <button className="icon-button" aria-label="Actualiser les archives" disabled={loading} onClick={reload}><RefreshCw size={17} /></button>
          <Link className="button-primary" href={`/conversions?clientId=${client.id}`}><Plus size={16} /> Importer un relevé</Link>
        </div>
      </section>

      <LoadState error={error} loading={loading} />

      {!loading && !error && !data?.length && (
        <section className="load-state flex-col">
          <Archive size={30} />
          <h2 className="font-semibold">Aucun relevé pour ce client.</h2>
          <p className="text-sm">Importez un PDF pour commencer.</p>
        </section>
      )}

      <div className="space-y-5">
        {data?.slice(0, visibleCount).map(item => {
          const locked = item.status === 'Processing';
          const files = [
            { kind: 'original' as const, label: 'PDF original', hint: 'Releve importe, conserve sans modification.', available: item.hasOriginal, icon: FileText },
            { kind: 'standard' as const, label: 'CSV standard', hint: 'Toutes les colonnes des operations extraites.', available: item.hasStandard, icon: FileSpreadsheet },
            { kind: 'export' as const, label: 'Export comptable', hint: item.exportLabel || 'Format choisi lors de la conversion.', available: item.hasExport, icon: FileSpreadsheet },
          ];

          return (
            <article className="ui-card workspace-document" key={item.id}>
              <header className="workspace-document-header">
                <div className="min-w-0">
                  <div className="mb-2 flex flex-wrap items-center gap-2">
                    <span className="ui-tag">{item.status === 'Completed' ? 'Archive' : 'Relevé importé'}</span>
                    <span className={item.status === 'Completed' ? 'status-positive' : 'ui-tag'}>{statusLabel[item.status] || item.status}</span>
                  </div>
                  <h3 className="break-all font-semibold">{item.inputFileName}</h3>
                  <p className="muted mt-2 text-xs">{item.bank} · Compte {item.account}</p>
                </div>
                <div className="workspace-document-meta">
                  <span>{new Date(item.completedAt || item.createdAt).toLocaleString('fr-FR')}</span>
                  <span>{item.transactionCount} opérations</span>
                </div>
              </header>

              <StatementDetails item={item}/><div className="workspace-document-actions" aria-label={`Actions du relevé ${item.inputFileName}`}>
                <Button variant="outline" disabled={!item.hasOriginal} onClick={() => { setProcessing(null); setPreview({ item, kind: 'original' }); }}>
                  <Eye /> Consulter
                </Button>
                <Button variant="edit" disabled={locked || !can(user, 'documents.write')} onClick={() => setManagement({ item, remove: false })}>
                  <Pencil /> Modifier
                </Button>
                <Button variant="danger" disabled={locked || !can(user, 'documents.write')} onClick={() => setManagement({ item, remove: true })}>
                  <Trash2 /> Supprimer
                </Button>
                {['Pending', 'Failed'].includes(item.status) && item.hasOriginal && (
                  <Button disabled={!can(user, 'documents.write')} onClick={() => { setPreview(null); setProcessing(item); }}>{item.status === 'Failed' ? 'Configurer et réessayer' : 'Configurer et traiter'}</Button>
                )}
              </div>

              {item.status === 'Completed' && <Button variant="outline" className="mb-4" onClick={() => { setPreview(null); setProcessing(item); }}>Consulter les étapes du traitement</Button>}
              {item.errorMessage && <p role="alert" className="danger-button mb-4 text-sm">{item.errorMessage}</p>}

              <div className="grid gap-3 lg:grid-cols-3">
                {files.map(({ kind, label, hint, available, icon: Icon }) => (
                  <section className="file-card" key={kind} aria-label={`${label} — ${item.inputFileName}`}>
                    <div className="file-card-head"><span className="file-card-icon"><Icon size={21}/></span><h4 className="text-sm font-semibold">{label}</h4></div>
                    <p className="file-card-hint">{hint}</p><div className="file-card-actions">
                    <Button variant="outline" disabled={!available} aria-pressed={preview?.item.id === item.id && preview.kind === kind} onClick={() => { setProcessing(null); setPreview({ item, kind }); }}><Eye size={16}/>{preview?.item.id === item.id && preview.kind === kind ? "Aperçu ouvert" : "Consulter"}</Button>
                    <Button disabled={!available || !can(user, 'documents.export')} title={!can(user, 'documents.export') ? 'Demandez la permission de téléchargement à un administrateur' : undefined} onClick={() => download(item, kind)}>
                      <Download size={15} />{available ? 'Télécharger' : 'Non disponible'}
                    </Button>
                    {kind !== 'original' && (
                      <Button variant="outline" disabled={!available || !can(user, 'documents.export')} onClick={() => download(item, kind, true)}>
                        <FileSpreadsheet size={15} /> Excel (.xlsx)
                      </Button>
                    )}
                  </div></section>
                ))}
              </div>
              {processing?.id === item.id && <TreatmentPanel key={item.id} item={processing} onClose={() => setProcessing(null)} onSaved={reload}/>}
              {preview?.item.id === item.id && <div ref={previewRef} tabIndex={-1} className="file-preview-region" role="region" aria-label="Aperçu du fichier sélectionné"><DocumentPreview inline key={item.id+'-'+preview.kind} clientId={client.id} item={item} kind={preview.kind} onClose={() => setPreview(null)}/></div>}
            </article>
          );
        })}
      </div>
      {data && visibleCount < data.length && <Button variant="outline" onClick={() => setVisibleCount(count => count + 20)}>Afficher 20 relevés supplémentaires ({data.length - visibleCount} restants)</Button>}
    </>
  );
}

export default function ClientWorkspacePage() {
  const [clients, setClients] = useState<Client[]>([]);
  const [clientId, setClientId] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;
    all<Client>('/clients')
      .then(items => {
        if (!active) return;
        setClients(items);
        const requested = new URLSearchParams(window.location.search).get('clientId');
        setClientId(items.find(client => client.id === requested)?.id || items[0]?.id || '');
      })
      .catch(e => { if (active) setError(e.message); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  const client = clients.find(item => item.id === clientId);

  return (
    <div className="ui-page workspace-page">
      <section className="ui-hero">
        <p className="ui-eyebrow">DOCUMENTS & ARCHIVES</p>
        <h1 className="ui-title">Espace clients</h1>
        <p className="ui-description">Consultez, modifiez et supprimez les relevés de chaque client.</p>
      </section>
      <div className="ui-card">
        <label className="ui-label" htmlFor="archive-client">Sélectionner un client</label>
        <select className="ui-input" id="archive-client" value={clientId} disabled={loading} onChange={e => setClientId(e.target.value)}>
          <option value="">Choisir un client</option>
          {clients.map(item => <option key={item.id} value={item.id}>{item.name} — {item.legalName}</option>)}
        </select>
        <p className="ui-description">Modifier un relevé traité le remet en attente et supprime ses anciens fichiers générés. Vous pourrez relancer le traitement ensuite.</p>
      </div>
      <LoadState error={error} loading={loading} />
      {!loading && !error && !clients.length && <Link className="button-primary justify-self-start" href="/clients">Créer votre premier client</Link>}
      {client && <Archives key={client.id} client={client} />}
    </div>
  );
}
