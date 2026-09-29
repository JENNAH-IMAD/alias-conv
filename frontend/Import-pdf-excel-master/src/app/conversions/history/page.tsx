'use client';

import { useEffect, useState } from 'react';
import { Download, History as HistoryIcon, Search } from 'lucide-react';
import { History, Page, exportBlob } from '@/services/api';
import { ExportDialog, ExportRequest } from '@/components/export-dialog';
import { exportName } from '@/lib/save-export';
import { can } from '@/lib/permissions';
import { useAuth } from '@/components/auth-context';
import { useApi } from '@/services/use-api';
import { LoadState, Pagination } from '@/components/resource-editor';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { StatusBadge } from '@/components/ui/status-badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';

export default function ConversionHistoryPage() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [term, setTerm] = useState('');
  const [status, setStatus] = useState('');
  const { user } = useAuth();
  const [exportRequest, setExportRequest] = useState<ExportRequest | null>(null);
  const [saveNotice, setSaveNotice] = useState('');
  useEffect(() => { const value = new URLSearchParams(window.location.search).get('status'); if (value && ['Completed', 'Failed', 'Pending', 'Processing'].includes(value)) queueMicrotask(() => setStatus(value)); }, []);
  useEffect(() => { const timer = setTimeout(() => { setTerm(search); setPage(1); }, 300); return () => clearTimeout(timer); }, [search]);
  const { data, error, loading } = useApi<Page<History>>(`/history?page=${page}&pageSize=20&search=${encodeURIComponent(term)}&status=${status}`);

  function save(item: History) {
    setExportRequest({ name: exportName(item.bank, item.accountName, item.id, item.outputFileName?.split('.').pop() || 'csv', item.completedAt || item.createdAt), getBlob: () => exportBlob(`/conversions/${item.id}/download`) });
  }

  return <div className="ui-page">{exportRequest && <ExportDialog request={exportRequest} onClose={() => setExportRequest(null)} onSaved={setSaveNotice}/>} {saveNotice && <p role="status" className="rounded-xl border border-border p-4 text-sm">{saveNotice}</p>}
    <section className="ui-hero"><p className="ui-eyebrow">ACTIVITÉ</p><h1 className="ui-title">Historique des conversions</h1><p className="ui-description">Retrouvez vos traitements, consultez leur statut et téléchargez vos exports.</p></section>
    <Card className="min-w-0"><CardHeader><CardTitle className="flex items-center gap-2"><HistoryIcon className="size-5 text-primary"/> Tous les traitements</CardTitle><CardDescription>{data ? `${data.total} résultat${data.total > 1 ? 's' : ''}` : 'Vos relevés, du plus récent au plus ancien.'}</CardDescription></CardHeader><CardContent>
      <div className="mb-6 grid gap-3 md:grid-cols-[1fr_220px]"><div className="relative"><Search className="pointer-events-none absolute top-3 left-3 size-4 text-muted-foreground"/><Input type="search" aria-label="Recherche" value={search} onChange={e => setSearch(e.target.value)} placeholder="Rechercher un client, une banque ou un fichier…" className="h-10 pl-9"/></div><select aria-label="Statut" className="ui-input" value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="">Tous les statuts</option><option value="Completed">Terminées</option><option value="Pending">En attente</option><option value="Processing">En cours</option><option value="Failed">Échouées</option></select></div>
      <LoadState error={error} loading={loading} empty={!data?.items.length}/>
      {!loading && !!data?.items.length && <Table><caption className="sr-only">Conversions bancaires et fichiers à télécharger</caption><TableHeader><TableRow className="bg-muted/50"><TableHead>Client / Banque</TableHead><TableHead>Document</TableHead><TableHead>Traçabilité</TableHead><TableHead>Date</TableHead><TableHead className="text-right">Opérations</TableHead><TableHead>Statut</TableHead><TableHead className="text-right">Export</TableHead></TableRow></TableHeader><TableBody>{data.items.map(item => <TableRow key={item.id}><TableCell><p className="font-medium">{item.client}</p><p className="mt-1 text-xs text-muted-foreground">{item.bank} · {item.accountName || "Compte non renseigné"}</p><p className="text-xs text-muted-foreground">{item.accountNumber}</p></TableCell><TableCell><p className="max-w-64 truncate" title={item.inputFileName}>{item.inputFileName}</p>{item.errorMessage && <p className="mt-1 max-w-64 whitespace-normal text-xs text-destructive">{item.errorMessage}</p>}</TableCell><TableCell className="text-xs"><p>Traité par : {item.processedByName || "Non renseigné"}</p>{item.processedAt && <p className="text-muted-foreground">{new Date(item.processedAt).toLocaleString("fr-FR")}</p>}<p className="mt-2">Modifié par : {item.modifiedByName || "Non renseigné"}</p>{item.modifiedAt && <p className="text-muted-foreground">{new Date(item.modifiedAt).toLocaleString("fr-FR")}</p>}</TableCell><TableCell className="text-muted-foreground">{new Date(item.createdAt).toLocaleDateString('fr-FR')}<span className="mt-1 block text-xs">{new Date(item.createdAt).toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit' })}</span></TableCell><TableCell className="text-right tabular-nums">{item.count}</TableCell><TableCell><StatusBadge status={item.status}/></TableCell><TableCell className="text-right">{item.status === 'Completed' ? <Button variant="outline" size="sm" disabled={!can(user, 'documents.export')} aria-label={`Télécharger ${item.inputFileName}`} onClick={() => void save(item)}><Download/> Télécharger</Button> : <span className="text-xs text-muted-foreground">Indisponible</span>}</TableCell></TableRow>)}</TableBody></Table>}
      {!loading && <div className="mt-5 border-t border-border pt-3"><Pagination page={page} total={data?.total || 0} onChange={setPage}/></div>}
    </CardContent></Card>
  </div>;
}
