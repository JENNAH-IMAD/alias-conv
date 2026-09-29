'use client';
import { can } from '@/lib/permissions';
import { useAuth } from '@/components/auth-context';


import { useEffect, useState } from 'react';
import { StatementDetails } from './statement-details';
import { ExportDialog, ExportRequest } from './export-dialog';
import { exportName } from '@/lib/save-export';
import { Button } from '@/components/ui/button';
import { ConversionReview, ExportMapping, standardTransactions } from './conversion-review';
import { DocumentPreview, WorkspaceDocument } from './document-actions';
import { all, exportBlob, Conversion, ExportTemplate, request, Statement, Transaction } from '@/services/api';

type ReviewData = { transactions: Transaction[]; truncated: boolean };

export function TreatmentPanel({ item, onClose, onSaved }: { item: WorkspaceDocument; onClose: () => void; onSaved: () => void }) {
  const { user } = useAuth();
  const completed = item.status === 'Completed';
  const [step, setStep] = useState(completed ? 2 : 1);
  const [profiles, setProfiles] = useState<Statement[]>([]);
  const [exports, setExports] = useState<ExportTemplate[]>([]);
  const [profileId, setProfileId] = useState(item.bankStatementTemplateId || '');
  const [exportId, setExportId] = useState(item.exportTemplateId || '');
  const [result, setResult] = useState<Conversion | null>(null);
  const [review, setReview] = useState<ReviewData | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [exportRequest, setExportRequest] = useState<ExportRequest | null>(null);
  const [saveNotice, setSaveNotice] = useState('');
  const [reviewError, setReviewError] = useState('');
  const readOnly = completed || !!result;
  const selectedProfile = profiles.find(profile => profile.id === profileId);
  const selectedExport = exports.find(template => template.id === exportId);

  useEffect(() => {
    let active = true;
    Promise.all([all<Statement>('/bank-statement-templates'), all<ExportTemplate>('/export-templates')]).then(([p, e]) => {
      if (active) { setProfiles(p.filter(profile => profile.id === item.bankStatementTemplateId || ((profile.bankId === item.bankId || (profile.bankId === null && profile.parserKey === 'multibank-auto')) && profile.isActive))); setExports(e.filter(template => template.isActive || template.id === item.exportTemplateId)); }
    }).catch(e => { if (active) setError(e.message); }).finally(() => { if (active) setLoading(false); });
    if (completed && item.hasStandard) request<{ rows: string[][]; truncated: boolean }>(`/client-workspace/${item.clientId}/archives/${item.id}/preview/standard`).then(data => { if (active) setReview({ transactions: standardTransactions(data.rows), truncated: data.truncated }); }).catch(e => { if (active) setReviewError(e.message); });
    return () => { active = false; };
  }, [item.id, item.clientId, item.bankId, item.bankStatementTemplateId, item.exportTemplateId, item.hasStandard, completed]);

  async function process() {
    if (busy || readOnly || !profileId || !exportId) return;
    setBusy(true); setError('');
    try { const converted = await request<Conversion>(`/documents/${item.id}/process`, { method: 'POST', body: JSON.stringify({ profileId, exportId }) }); setResult(converted); setStep(2); onSaved(); }
    catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  function save(excel: boolean) {
    const history = result?.history;
    const extension = excel ? 'xlsx' : (history?.outputFileName || item.outputFileName)?.split('.').pop() || 'csv';
    setExportRequest({ name: exportName(item.bank, item.accountName, item.id, extension, history?.completedAt || item.completedAt), getBlob: () => exportBlob(`/client-workspace/${item.clientId}/archives/${item.id}/files/export${excel ? '?excel=true' : ''}`) });
  }
  const transactions = result?.transactions || review?.transactions;
  return <section className="treatment-panel mt-6 border-t border-border pt-6" aria-label={`Étapes du traitement ${item.inputFileName}`}>
    <div className="mb-5 flex flex-wrap items-center justify-between gap-3"><div><h4 className="text-lg font-semibold">Parcours du traitement</h4><p className="muted mt-1 text-xs">Consultez chaque étape sans quitter ce relevé.</p></div><Button variant="outline" size="sm" disabled={busy} onClick={() => { onClose(); onSaved(); }}>Réduire le traitement</Button></div>
    <div className="treatment-navigation" role="group" aria-label="Étapes du traitement">{['Document importé', 'Lecture et configuration', 'Contrôle et export'].map((label, index) => <button key={label} type="button" aria-pressed={step === index} aria-controls={`treatment-${item.id}-${index}`} disabled={busy} onClick={() => setStep(index)} className={step === index ? 'selected' : ''}><span>{index + 1}</span><strong>{label}</strong></button>)}</div>
    {exportRequest && <ExportDialog request={exportRequest} onClose={() => setExportRequest(null)} onSaved={setSaveNotice}/>}
    {saveNotice && <p role="status" className="my-4 text-sm">{saveNotice}</p>}
    <StatementDetails item={result?.history || item}/>
    {error && <p role="alert" className="my-4 text-sm text-destructive">{error}</p>}
    <div id={`treatment-${item.id}-0`} hidden={step !== 0} className="space-y-4"><dl className="grid gap-4 rounded-xl bg-muted/50 p-4 sm:grid-cols-2"><div><dt className="muted text-xs">Document</dt><dd className="mt-1 break-all text-sm font-medium">{item.inputFileName}</dd></div><div><dt className="muted text-xs">Banque et compte</dt><dd className="mt-1 break-all text-sm font-medium">{item.bank} · {item.account}</dd></div></dl>{item.hasOriginal && <DocumentPreview inline clientId={item.clientId} item={item} kind="original" onClose={() => setStep(1)}/>}</div>
    <div id={`treatment-${item.id}-1`} hidden={step !== 1} className="space-y-5">
      {loading && <p role="status">Chargement de la configuration…</p>}
      {completed && <p className="rounded-lg bg-muted p-3 text-xs leading-5 text-muted-foreground">Profils associés à ce traitement, affichés dans leur version actuelle. Les anciennes archives ne contiennent pas de copie historique de tous les paramètres. La consultation ne relance pas la conversion.</p>}
      <div className="grid gap-4 lg:grid-cols-2"><label><span className="ui-label">Profil de lecture PDF</span><select aria-label="Profil de lecture PDF" className="ui-input" disabled={readOnly || busy || loading} value={profileId} onChange={e => setProfileId(e.target.value)}><option value="">{completed ? 'Profil non disponible' : 'Choisir un profil'}</option>{profiles.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label><label><span className="ui-label">Format d’export</span><select aria-label="Format d’export" className="ui-input" disabled={readOnly || busy || loading} value={exportId} onChange={e => setExportId(e.target.value)}><option value="">{completed ? item.exportLabel || 'Modèle non disponible' : 'Choisir un format'}</option>{exports.map(e => <option key={e.id} value={e.id}>{e.name} · {e.type}</option>)}</select></label></div>
      {selectedProfile && <div className="rounded-xl border border-border p-4 text-sm"><h5 className="font-semibold">Paramètres de lecture</h5><p className="muted mt-2">{selectedProfile.description || selectedProfile.name}</p><dl className="mt-4 grid gap-4 sm:grid-cols-3">{[['Moteur', selectedProfile.parserKey], ['Format des dates', selectedProfile.dateFormat], ['Culture des montants', selectedProfile.numberCulture]].map(([label, value]) => <div key={label}><dt className="muted text-xs">{label}</dt><dd className="mt-1 break-all">{value}</dd></div>)}</dl>{selectedProfile.parserKey === 'delimited' && <p className="muted mt-3 text-xs">Lignes ignorées : {selectedProfile.skipLines} · Colonnes : date {selectedProfile.dateColumn}, libellé {selectedProfile.descriptionColumn}, débit {selectedProfile.debitColumn}, crédit {selectedProfile.creditColumn}.</p>}</div>}
      {selectedExport && <ExportMapping template={selectedExport}/>}
      {!readOnly && <div className="flex justify-end"><Button disabled={!can(user, 'documents.write') || busy || loading || !profileId || !exportId} onClick={() => void process()}>{busy ? 'Traitement en cours…' : 'Lancer le traitement'}</Button></div>}
    </div>
    <div id={`treatment-${item.id}-2`} hidden={step !== 2}>
      {transactions ? <ConversionReview transactions={transactions} truncated={review?.truncated} action={<div className="flex flex-wrap gap-2"><Button variant="outline" disabled={!can(user, 'documents.export') || busy} onClick={() => void save(true)}>Excel (.xlsx)</Button><Button disabled={!can(user, 'documents.export') || busy} onClick={() => void save(false)}>Télécharger l’export</Button></div>}/> : <div className="rounded-xl border border-dashed border-border p-6 text-sm text-muted-foreground">{reviewError ? <p role="alert">{reviewError}</p> : completed && item.hasStandard ? <p role="status">Chargement des opérations…</p> : completed ? 'Les opérations de cette archive ne sont pas disponibles.' : <>Les opérations apparaîtront ici après le traitement.<Button variant="outline" className="mt-4 block" onClick={() => setStep(1)}>Configurer le traitement</Button></>}</div>}
    </div>
  </section>;
}

