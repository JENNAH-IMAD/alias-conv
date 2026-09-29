'use client';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Alert, AlertTitle, AlertDescription } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';
import { FormEvent, useEffect, useState } from 'react';
import { BankDialog } from './bank-dialogs';
import { ChevronLeft, ChevronRight, Inbox } from 'lucide-react';
import { all, Bank, Client, ExportField, request } from '@/services/api';

type Field = { key: string; label: string; type?: 'number' | 'email' | 'checkbox' | 'bank' | 'client' | 'select'; options?: [string, string][]; optional?: boolean };
type Schema = { defaults: Record<string, unknown>; fields: Field[]; };
const cultures: [string, string][] = [['fr-FR', 'Français (1 250,50)'], ['en-US', 'Anglais (1,250.50)']];
const delimiters: [string, string][] = [[';', 'Point-virgule'], ['\t', 'Tabulation'], ['|', 'Barre verticale']];
const schemas: Record<string, Schema> = {
  clients: { defaults: { country: 'Maroc' }, fields: [{key:'name',label:'Nom'}, {key:'legalName',label:'Raison sociale'}, {key:'ice',label:'ICE (15 chiffres)'}, {key:'if',label:'IF'}, {key:'rc',label:'RC'}, {key:'email',label:'Email',type:'email'}, {key:'phone',label:'Téléphone'}, {key:'country',label:'Pays'}] },
  banks: { defaults: {}, fields: [{key:'name',label:'Nom'}, {key:'code',label:'Code'}, {key:'description',label:'Description',optional:true}] },
  'bank-accounts': { defaults: {currency:'MAD',journal:'BQ',accountCode:'512000'}, fields: [{key:'clientId',label:'Client',type:'client'}, {key:'bankId',label:'Banque',type:'bank'}, {key:'accountNumber',label:'Numéro de compte'}, {key:'accountName',label:'Intitulé'}, {key:'currency',label:'Devise'}, {key:'journal',label:'Journal comptable'}, {key:'accountCode',label:'Compte comptable'}] },
  'bank-statement-templates': { defaults: {parserKey:'multibank-auto', delimiter:';',dateFormat:'dd/MM/yyyy',numberCulture:'fr-FR',skipLines:0,dateColumn:0,descriptionColumn:1,debitColumn:2,creditColumn:3,balanceColumn:4,referenceColumn:5,isActive:true}, fields: [{key:'name',label:'Nom'}, {key:'bankId',label:'Banque',type:'bank'}, {key:'description',label:'Description',optional:true}, {key:'parserKey',label:'Moteur de lecture',type:'select',options:[['multibank-auto','Automatique multibanque - PDF texte'],['delimited','PDF texte délimité']]}, {key:'delimiter',label:'Séparateur des colonnes',type:'select',options:delimiters}, {key:'dateFormat',label:'Format de date',type:'select',options:['dd/MM/yyyy','yyyy-MM-dd','dd-MM-yyyy','MM/dd/yyyy'].map(s=>[s,s])}, {key:'numberCulture',label:'Culture des montants',type:'select',options:cultures}, {key:'skipLines',label:'Lignes initiales à ignorer',type:'number'}, ...['date','description','debit','credit','balance','reference'].map((key,i)=>({key:`${key}Column`,label:`Colonne ${['date','libellé','débit','crédit','solde','référence'][i]} (première = 0)`,type:'number' as const,optional:i>3})), {key:'isActive',label:'Actif',type:'checkbox'}] },
  'export-templates': { defaults: {type:'CUSTOMCSV',fileExtension:'csv',delimiter:';',encoding:'utf-8',numberCulture:'fr-FR',includeHeader:true,isActive:true,fields:[{fieldName:'Date',sourceField:'Date',position:0,format:'dd/MM/yyyy',required:true},{fieldName:'Libellé',sourceField:'Description',position:1,format:null,required:true},{fieldName:'Débit',sourceField:'Debit',position:2,format:'0.00',required:false},{fieldName:'Crédit',sourceField:'Credit',position:3,format:'0.00',required:false},{fieldName:'Solde',sourceField:'Balance',position:4,format:'0.00',required:false}]}, fields: [{key:'name',label:'Nom'}, {key:'description',label:'Description',optional:true}, {key:'type',label:'Format cible',type:'select',options:[['SAGE100','Sage 100'],['SAGEX3','Sage X3'],['CUSTOMCSV','CSV personnalisé']]}, {key:'delimiter',label:'Séparateur',type:'select',options:[...delimiters,[',','Virgule'],[' ','Espace']]}, {key:'encoding',label:'Encodage',type:'select',options:[['utf-8','UTF-8'],['windows-1252','Windows-1252']]}, {key:'numberCulture',label:'Culture des montants',type:'select',options:cultures}, {key:'includeHeader',label:'Ligne d’en-tête',type:'checkbox'}, {key:'isActive',label:'Actif',type:'checkbox'}] },
};
const sources = ['Date','ValueDate','Journal','Account','Description','Debit','Credit','Reference','Amount','Direction','Analytic','Balance'];
export function ResourceEditor({ resource, initial, onClose, onSaved }: {resource:string; initial?: object; onClose:()=>void; onSaved:()=>void}) {
  const schema = schemas[resource];
  const [values,setValues] = useState<Record<string, unknown>>({...schema.defaults,...initial});
  const [banks,setBanks] = useState<Bank[]>([]); const [clients,setClients] = useState<Client[]>([]);
  const [error,setError] = useState(''); const [busy,setBusy] = useState(false);
  useEffect(()=> { if(schema.fields.some(f=>f.type==='bank')) all<Bank>('/banks').then(setBanks).catch(e=>setError(e.message)); if(schema.fields.some(f=>f.type==='client')) all<Client>('/clients').then(setClients).catch(e=>setError(e.message)); },[schema]);
  const inputClass = "ui-input";
  const set = (key:string, value:unknown) => setValues(v=>({...v,[key]:value,...(key==='parserKey'&&value==='multibank-auto'?{bankId:null}:{})}));
  const automatic = resource==='bank-statement-templates' && values.parserKey==='multibank-auto';
  const fields = (values.fields || []) as ExportField[];
  const submit = async (e:FormEvent) => {
    e.preventDefault(); if(busy)return; setBusy(true); setError('');
    const payload = {...values};
    for (const f of schema.fields) { if (payload[f.key] === undefined) payload[f.key] = f.type === 'number' ? null : ''; }
    if(automatic && !payload.bankId) payload.bankId = null;
    if(resource === 'export-templates') { payload.fileExtension = values.type === 'SAGE100' ? 'txt' : 'csv'; payload.fields = fields.map((f,position)=>({...f,position})); }
    try { await request(`/${resource}${values.id ? `/${values.id}`:''}`, {method:values.id?'PUT':'POST',body:JSON.stringify(payload)}); onSaved(); onClose(); } catch(e) {setError((e as Error).message);} finally {setBusy(false);}
  };
  return <BankDialog title={(values.id?'Modifier':'Créer')+' · '+({'clients':'Client','banks':'Banque','bank-accounts':'Compte bancaire','bank-statement-templates':'Profil d’extraction','export-templates':'Modèle d’export'}[resource]||'Référentiel')} onClose={onClose} busy={busy}><form onSubmit={submit} className="space-y-5"><fieldset disabled={busy} className="space-y-5">
    {resource==='bank-statement-templates' && !automatic && <p>Lecture de lignes délimitées dans un PDF texte. Les colonnes commencent à 0. Laisser solde ou référence vide si absent. Chaque ligne non vide doit correspondre au profil ; les scans ne sont pas pris en charge par les moteurs installés. Les indices de colonnes ne sont utilisés que par le moteur délimité.</p>}
    <div className="grid gap-4 sm:grid-cols-2">{schema.fields.filter(f=>!automatic||['name','bankId','description','parserKey','isActive'].includes(f.key)).map(f=><label key={f.key} className="space-y-2 text-sm"><span>{f.label}</span>{f.type==='checkbox'?<input type="checkbox" className="ml-3" checked={Boolean(values[f.key])} onChange={e=>set(f.key,e.target.checked)}/>:['bank','client','select'].includes(f.type||'')?<select required={!(automatic&&f.key==='bankId')} className={inputClass} value={String(values[f.key]??'')} onChange={e=>set(f.key,e.target.value)}><option value="">{automatic&&f.key==='bankId'?'Toutes les banques':'Choisir…'}</option>{(f.type==='bank'?banks.map(b=>[b.id,b.name]):f.type==='client'?clients.map(c=>[c.id,c.name]):f.options||[]).map(([value,label])=><option key={value} value={value}>{label}</option>)}</select>:<Input className={inputClass} type={f.type||'text'} required={!f.optional} min={f.type==='number'?0:undefined} value={String(values[f.key]??'')} onChange={e=>set(f.key,f.type==='number'?(e.target.value===''?null:Number(e.target.value)):e.target.value)}/>}</label>)}</div>
    {resource==='export-templates' && <div className="space-y-3"><h3>Colonnes d’export, dans l’ordre du fichier</h3>{fields.map((f,i)=><div className="grid grid-cols-2 gap-2 border-b border-border pb-3" key={i}><Input aria-label="Titre de colonne" required className={inputClass} value={f.fieldName} onChange={e=>set('fields',fields.map((x,j)=>j===i?{...x,fieldName:e.target.value}:x))}/><select aria-label="Valeur source" className={inputClass} value={f.sourceField} onChange={e=>set('fields',fields.map((x,j)=>j===i?{...x,sourceField:e.target.value,format:null}:x))}>{sources.map(s=><option key={s}>{s}</option>)}</select><select aria-label="Format" className={inputClass} value={f.format||''} onChange={e=>set('fields',fields.map((x,j)=>j===i?{...x,format:e.target.value||null}:x))}>{['',...(['Date','ValueDate'].includes(f.sourceField)?['dd/MM/yyyy','yyyy-MM-dd','ddMMyyyy','yyyyMMdd']:['0.00','0.000','0.##'])].map(s=><option key={s} value={s}>{s||'Par défaut'}</option>)}</select><div className="flex gap-3"><label><input type="checkbox" checked={f.required} onChange={e=>set('fields',fields.map((x,j)=>j===i?{...x,required:e.target.checked}:x))}/> Obligatoire</label><button type="button" onClick={()=>set('fields',fields.filter((_,j)=>j!==i))}>Retirer</button></div></div>)}<button type="button" onClick={()=>set('fields',[...fields,{fieldName:'Référence',sourceField:'Reference',position:fields.length,format:null,required:false}])}>+ Ajouter une colonne</button></div>}
    </fieldset>{error&&<p role="alert" className="text-destructive">{error}</p>}<Button disabled={busy}>{busy?'Enregistrement…':'Enregistrer'}</Button>
  </form></BankDialog>;
}
export function LoadState({error,loading,empty}:{error:string;loading:boolean;empty?:boolean}) {
  return <>
    {error && <Alert variant="destructive"><AlertTitle>Les données ne sont pas disponibles</AlertTitle><AlertDescription>{error}</AlertDescription></Alert>}
    {loading && <div className="space-y-3 rounded-xl border border-border bg-card p-5" role="status"><span className="sr-only">Chargement des données…</span><Skeleton className="h-4 w-1/3"/><Skeleton className="h-4 w-2/3"/><Skeleton className="h-4 w-1/2"/></div>}
    {!loading && !error && empty && <div className="load-state flex-col"><Inbox size={28}/><p>Aucun élément à afficher.</p><p className="text-xs">Ajoutez un élément ou ajustez vos filtres pour commencer.</p></div>}
  </>;
}
export function Pagination({page,total,onChange}:{page:number;total:number;onChange:(n:number)=>void}) { return <nav aria-label="Pagination" className="pagination"><span>{total} élément{total>1?'s':''} · Page {page} sur {Math.max(1,Math.ceil(total/20))}</span><div className="flex gap-2"><Button variant="outline" disabled={page===1} onClick={()=>onChange(page-1)}><ChevronLeft size={14}/> Précédent</Button><Button variant="outline" disabled={page*20>=total} onClick={()=>onChange(page+1)}>Suivant <ChevronRight size={14}/></Button></div></nav>; }

