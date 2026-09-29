'use client';
import { Button } from '@/components/ui/button';
import { ReactNode, useEffect,useState } from 'react';
import { BankDialog } from './bank-dialogs';
import dynamic from 'next/dynamic';
import { standardTransactions } from '@/lib/standard-transactions';
const ConversionReview = dynamic(() => import('./conversion-review').then(m => m.ConversionReview), { loading: () => <p role="status">Chargement de l’analyse…</p> });
import { all,API_URL,getSession,request,Client,Account } from '@/services/api';
export type WorkspaceDocument={id:string;clientId:string;bankAccountId:string;bankStatementTemplateId?:string|null;exportTemplateId?:string|null;bankId:string;status:string;errorMessage:string|null;inputFileName:string;createdAt:string;completedAt:string|null;transactionCount:number;bank:string;account:string;accountName?:string|null;processedByName?:string|null;processedAt?:string|null;modifiedByName?:string|null;modifiedAt?:string|null;outputFileName:string|null;exportLabel:string|null;hasOriginal:boolean;hasStandard:boolean;hasExport:boolean};
export function DocumentPreview({clientId,item,kind,onClose,inline=false}:{clientId:string;item:WorkspaceDocument;kind:string;onClose:()=>void;inline?:boolean}){
 const [retry,setRetry]=useState(0);
 const [mode,setMode]=useState<'table'|'text'|'analysis'>('table');
 const [url,setUrl]=useState('');const [data,setData]=useState<{rows?:string[][];text?:string|null;textTruncated?:boolean;truncated:boolean}|null>(null);const [error,setError]=useState('');
 useEffect(()=>{let active=true;let objectUrl='';const controller=new AbortController();async function load(){try{if(kind==='original'){const response=await fetch(`${API_URL}/client-workspace/${clientId}/archives/${item.id}/files/original`,{headers:{Authorization:`Bearer ${getSession()?.token||''}`},signal:controller.signal});if(!response.ok){if(response.status===401)window.dispatchEvent(new Event('session-expired'));const problem=await response.json().catch(()=>null);throw Error(problem?.title || (response.status===403?'Vous ne disposez pas de la permission pour consulter ce PDF.':'PDF indisponible. Réessayez ou vérifiez que le fichier existe encore.'));}objectUrl=URL.createObjectURL(await response.blob());if(active)setUrl(objectUrl);else URL.revokeObjectURL(objectUrl);}else{const result=await request<{rows?:string[][];text?:string|null;textTruncated?:boolean;truncated:boolean}>(`/client-workspace/${clientId}/archives/${item.id}/preview/${kind}`,{signal:controller.signal});if(active)setData(result);}}catch(e){if(active)setError((e as Error).message);}}void load();return()=>{active=false;controller.abort();if(objectUrl)URL.revokeObjectURL(objectUrl);};},[clientId,item.id,kind,retry]);
 return <PreviewFrame inline={inline} wide={kind!=='original'} title={kind==='original'?'Consulter le PDF':'Consulter le fichier exporté'} onClose={onClose}>
 <p className="mb-4 break-all text-sm">{kind==='export'?item.outputFileName:item.inputFileName}</p>
 {error&&<div role="alert" className="rounded-xl border border-destructive/30 p-4 text-destructive"><p>{error}</p><Button variant="outline" className="mt-3" onClick={()=>{setError('');setRetry(value=>value+1);}}>Réessayer</Button></div>}
 {!error&&!url&&!data&&<p role="status">Chargement de l’aperçu…</p>}
 {url&&<><iframe title="Aperçu du relevé PDF" src={url} className="h-[60vh] w-full rounded-lg border border-[var(--border)]"/><a href={url} target="_blank" rel="noreferrer" className="button-secondary mt-3">Ouvrir le PDF dans un nouvel onglet</a></>}
 {data&&<div className="mb-4 flex flex-wrap gap-2" role="group" aria-label="Affichage du fichier">
 <Button variant="outline" aria-pressed={mode==='table'} onClick={()=>setMode('table')}>Tableau</Button>
 {data.text!=null&&<Button variant="outline" aria-pressed={mode==='text'} onClick={()=>setMode('text')}>Texte brut</Button>}
 {kind==='standard'&&<Button variant="outline" aria-pressed={mode==='analysis'} onClick={()=>setMode('analysis')}>Analyse</Button>}
 </div>}
 {mode==='table'&&data?.rows&&<div className="max-h-[60vh] overflow-auto"><table className="conversion-table"><tbody>{data.rows.map((row,i)=><tr key={i}>{row.map((cell,j)=><td key={j} className="whitespace-pre-wrap min-w-28 max-w-lg break-words">{cell}</td>)}</tr>)}</tbody></table></div>}
 {mode==='text'&&data?.text!=null&&<pre className="max-h-[60vh] overflow-auto rounded-lg bg-[var(--surface-alt)] p-4 text-xs">{data.text}</pre>}
 {mode==='analysis'&&data?.rows&&<ConversionReview transactions={standardTransactions(data.rows)} truncated={data.truncated}/>}
 {(mode==='text'?data?.textTruncated:data?.truncated)&&<p className="ui-description">{mode==='text'?'Aperçu limité à 100 000 caractères.':'Aperçu limité aux 500 premières lignes.'} Le téléchargement contient le fichier complet.</p>}
 </PreviewFrame>;
}

export function ManageDocument({item,remove,onClose,onSaved}:{item:WorkspaceDocument;remove:boolean;onClose:()=>void;onSaved:()=>void}){
 const [clients,setClients]=useState<Client[]>([]);const [accounts,setAccounts]=useState<Account[]>([]);const [clientId,setClientId]=useState(item.clientId);const [accountId,setAccountId]=useState(item.bankAccountId);const [file,setFile]=useState<File|null>(null);const [busy,setBusy]=useState(false);const [loading,setLoading]=useState(!remove);const [error,setError]=useState('');
 useEffect(()=>{if(remove)return;let active=true;Promise.all([all<Client>('/clients'),all<Account>('/bank-accounts')]).then(([c,a])=>{if(active){setClients(c);setAccounts(a);}}).catch(e=>{if(active)setError(e.message);}).finally(()=>{if(active)setLoading(false);});return()=>{active=false;};},[remove]);
 async function save(){setBusy(true);setError('');try{if(remove)await request('/documents/'+item.id,{method:'DELETE'});else{const form=new FormData();form.append('clientId',clientId);form.append('bankAccountId',accountId);if(file)form.append('file',file);await request('/documents/'+item.id,{method:'PUT',body:form});}onSaved();onClose();}catch(e){setError((e as Error).message);}finally{setBusy(false);}}
 return <BankDialog title={remove?'Supprimer le relevé':'Modifier le relevé'} onClose={onClose} busy={busy}><div className="space-y-5"><p className="break-all font-semibold">{item.inputFileName}</p>{remove?<p>Supprimer définitivement ce dossier et tous ses fichiers (PDF, CSV standard et export) ? Cette action est irréversible.</p>:<fieldset disabled={busy||loading} className="space-y-4"><label className="block"><span className="ui-label">Client</span><select className="ui-input" value={clientId} onChange={e=>{setClientId(e.target.value);setAccountId('');}}>{clients.map(c=><option key={c.id} value={c.id}>{c.name}</option>)}</select></label><label className="block"><span className="ui-label">Compte bancaire</span><select className="ui-input" value={accountId} onChange={e=>setAccountId(e.target.value)}><option value="">Choisir un compte</option>{accounts.filter(a=>a.clientId===clientId).map(a=><option key={a.id} value={a.id}>{a.bank} · {a.accountNumber}</option>)}</select></label><label className="block"><span className="ui-label">Remplacer le PDF (facultatif)</span><input type="file" accept="application/pdf" className="ui-file" onChange={e=>setFile(e.target.files?.[0]||null)}/></label><p className="muted text-xs">Sans nouveau fichier, le PDF actuel est conservé. Le dossier revient en attente de traitement. Si le client change, retrouvez-le dans l’espace du nouveau client.</p></fieldset>}{error&&<p role="alert" className="danger-button">{error}</p>}<div className="flex justify-end gap-3"><Button variant="outline" disabled={busy} onClick={onClose}>Annuler</Button><Button variant={remove?'outline':'default'} className={remove?'danger-button':''} disabled={busy||loading||(!remove&&(!clientId||!accountId))} onClick={save}>{busy?'En cours…':remove?'Confirmer la suppression':'Enregistrer'}</Button></div></div></BankDialog>;
}


function PreviewFrame({ inline, title, children, onClose, wide }: { inline: boolean; title: string; children: ReactNode; onClose: () => void; wide: boolean }) {
  if (!inline) return <BankDialog title={title} wide={wide} onClose={onClose}>{children}</BankDialog>;
  return <section className="mt-5 min-w-0 rounded-xl border border-border p-4" aria-label={title}><div className="mb-4 flex items-center justify-between gap-3"><h4 className="font-semibold">{title}</h4><Button variant="ghost" size="sm" onClick={onClose}>Fermer l’aperçu</Button></div>{children}</section>;
}
