'use client';
import { ExportDialog, ExportRequest } from './export-dialog';
import { can } from '@/lib/permissions';
import { useAuth } from '@/components/auth-context';


import { Button } from '@/components/ui/button';
import { ExportTemplate, Transaction } from '@/services/api';
import {
  ArrowRight,
  Check,
  Copy,
  Download,
  Edit3,
  FileCheck2,
  Info,
  Plus,
  RotateCcw,
  ScanText,
  Search,
  Settings2,
  SlidersHorizontal,
  Trash2,
} from 'lucide-react';
import { useMemo, useState } from 'react';

type EditableTransaction = Transaction & { draftId: string; isNew?: boolean; dirty?: boolean };
type TransactionKey = keyof Pick<Transaction, 'date' | 'reference' | 'description' | 'debit' | 'credit' | 'amount' | 'balance'>;

const sourceLabels: Record<string, string> = {
  Date: 'Date operation',
  ValueDate: 'Date de valeur',
  Description: 'Libelle',
  Reference: 'Reference',
  Debit: 'Debit',
  Credit: 'Credit',
  Balance: 'Solde',
  Amount: 'Montant',
  Direction: 'Sens D/C',
  Account: 'Compte comptable',
  Journal: 'Journal',
  Analytic: 'Analytique',
};

const money = (value: number | null) =>
  value === null || Number.isNaN(value)
    ? 'Non disponible'
    : new Intl.NumberFormat('fr-FR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);

const today = () => new Date().toISOString().slice(0, 10);
const draftId = () => (globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random()}`);
const numberValue = (value: unknown) => {
  const parsed = Number(String(value ?? '').replace(/\s/g, '').replace(',', '.'));
  return Number.isFinite(parsed) ? parsed : 0;
};
const nullableNumberValue = (value: unknown) => {
  const raw = String(value ?? '').trim();
  if (!raw) return null;
  const parsed = Number(raw.replace(/\s/g, '').replace(',', '.'));
  return Number.isFinite(parsed) ? parsed : null;
};

function toEditable(transactions: Transaction[]): EditableTransaction[] {
  return transactions.map((transaction, index) => ({
    ...transaction,
    draftId: transaction.id || `row-${index}`,
    debit: numberValue(transaction.debit),
    credit: numberValue(transaction.credit),
    amount: numberValue(transaction.amount),
    balance: transaction.balance == null ? null : numberValue(transaction.balance),
  }));
}

function buildEmptyRow(): EditableTransaction {
  return {
    id: draftId(),
    draftId: draftId(),
    date: today(),
    reference: '',
    description: '',
    debit: 0,
    credit: 0,
    amount: 0,
    balance: null,
    isNew: true,
    dirty: true,
  };
}

function csvCell(value: unknown) {
  const text = value == null ? '' : String(value);
  return /[;"\n\r]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

function correctedCsv(rows: EditableTransaction[]) {
  const body = rows.map(row => [
    row.date,
    row.reference,
    row.description,
    row.debit ? row.debit.toFixed(2) : '',
    row.credit ? row.credit.toFixed(2) : '',
    row.balance == null ? '' : row.balance.toFixed(2),
    row.amount ? row.amount.toFixed(2) : '',
  ]);
  const csv = body.map(line => line.map(csvCell).join(';')).join('\r\n');
  const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8' });
  return blob;
}

export function ConversionSteps({ step }: { step: number }) {
  const steps = [
    { title: 'Document importe', hint: 'Client et compte bancaire', icon: FileCheck2 },
    { title: 'Lecture et mapping', hint: 'Profil, colonnes et format', icon: Settings2 },
    { title: 'Controle editable', hint: 'Corrections et export final', icon: ScanText },
  ];
  return (
    <ol className="conversion-steps" aria-label="Progression de la conversion">
      {steps.map(({ title, hint, icon: Icon }, index) => (
        <li key={title} className={index === step ? 'current' : index < step ? 'done' : ''} aria-current={index === step ? 'step' : undefined}>
          <span className="step-icon">{index < step ? <Check size={18} /> : <Icon size={18} />}</span>
          <div>
            <strong>{title}</strong>
            <small>{hint}</small>
          </div>
        </li>
      ))}
    </ol>
  );
}

export function ExportMapping({ template }: { template: ExportTemplate }) {
  const fields = [...template.fields].sort((a, b) => a.position - b.position);
  return (
    <section className="conversion-section">
      <div className="conversion-heading">
        <div>
          <h3>Correspondance des champs</h3>
          <p className="ui-description">Donnees extraites et parametres du compte vers les colonnes du fichier exporte.</p>
        </div>
        <span className="ui-tag">{template.fileExtension.toUpperCase()} - {template.encoding}</span>
      </div>
      <div className="mapping-grid">
        {fields.map((field, index) => (
          <div className="mapping-field" key={`${field.fieldName}-${index}`}>
            <span className="mapping-index">{index + 1}</span>
            <span>
              {sourceLabels[field.sourceField] || field.sourceField}
              <small>{field.format || (['Account', 'Journal'].includes(field.sourceField) ? 'Parametre du compte' : 'Donnee du releve')}</small>
            </span>
            <ArrowRight size={16} className="accent-text" />
            <strong>
              {field.fieldName}
              {field.required && <small>Obligatoire</small>}
            </strong>
          </div>
        ))}
      </div>
      <p className="ui-description">
        Separateur : {template.delimiter === ' ' ? 'espace' : template.delimiter === '\t' ? 'tabulation' : template.delimiter} - Téléchargements sans ligne d’en-tête.
      </p>
    </section>
  );
}

export function ConversionReview({ transactions, truncated = false, action }: { transactions: Transaction[]; truncated?: boolean; action?: React.ReactNode }) {
  const [exportRequest, setExportRequest] = useState<ExportRequest | null>(null);
  const [saveNotice, setSaveNotice] = useState('');
  const { user } = useAuth();
  const [rows, setRows] = useState<EditableTransaction[]>(() => toEditable(transactions));
  const [query, setQuery] = useState('');
  const [filter, setFilter] = useState('all');
  const [page, setPage] = useState(1);
  const [editMode, setEditMode] = useState(false);

  const totals = useMemo(() => ({
    debit: rows.reduce((value, row) => value + numberValue(row.debit), 0),
    credit: rows.reduce((value, row) => value + numberValue(row.credit), 0),
    corrections: rows.filter(row => row.dirty || row.isNew).length,
  }), [rows]);

  const first = rows[0];
  const last = rows.at(-1);
  const opening = !truncated && first?.balance != null ? first.balance - first.credit + first.debit : null;
  const closing = !truncated && last?.balance != null ? last.balance : null;
  const invalidRows = rows.filter(row => !row.date || !row.description.trim() || (row.debit > 0 && row.credit > 0)).length;
  const searchable = (row: EditableTransaction) => `${row.description} ${row.reference} ${row.date}`.toLocaleLowerCase('fr');
  const filteredRows = useMemo(
    () => rows.filter(row =>
      (filter === 'all' || (filter === 'debit' ? row.debit > 0 : filter === 'credit' ? row.credit > 0 : row.dirty || row.isNew)) &&
      searchable(row).includes(query.toLocaleLowerCase('fr')),
    ),
    [rows, query, filter],
  );
  const pages = Math.max(1, Math.ceil(filteredRows.length / 20));
  const current = Math.min(page, pages);
  const pageRows = filteredRows.slice((current - 1) * 20, current * 20);

  function updateRow(id: string, key: TransactionKey, value: string) {
    setRows(currentRows => currentRows.map(row => {
      if (row.draftId !== id) return row;
      const next = { ...row, dirty: true };
      if (key === 'debit' || key === 'credit' || key === 'amount') next[key] = numberValue(value);
      else if (key === 'balance') next.balance = nullableNumberValue(value);
      else next[key] = value;
      if ((key === 'debit' || key === 'credit') && (next.debit || next.credit)) next.amount = next.credit - next.debit;
      return next;
    }));
  }

  function addRow() {
    setRows(currentRows => [buildEmptyRow(), ...currentRows]);
    setEditMode(true);
    setPage(1);
  }

  function duplicateRow(row: EditableTransaction) {
    setRows(currentRows => {
      const index = currentRows.findIndex(item => item.draftId === row.draftId);
      const duplicate = { ...row, id: draftId(), draftId: draftId(), isNew: true, dirty: true };
      if (index < 0) return [duplicate, ...currentRows];
      return [...currentRows.slice(0, index + 1), duplicate, ...currentRows.slice(index + 1)];
    });
    setEditMode(true);
  }

  function removeRow(id: string) {
    setRows(currentRows => currentRows.filter(row => row.draftId !== id));
  }

  function resetRows() {
    setRows(toEditable(transactions));
    setEditMode(false);
    setQuery('');
    setFilter('all');
    setPage(1);
  }

  return (
    <div className="conversion-workbench">
      {exportRequest && <ExportDialog request={exportRequest} onClose={() => setExportRequest(null)} onSaved={setSaveNotice}/>}
      {saveNotice && <p role="status" className="text-sm">{saveNotice}</p>}
      <section className="conversion-section">
        <div className="conversion-heading">
          <div>
            <h3>Tableau de bord du traitement</h3>
            <p className="ui-description">{rows.length} operations {truncated ? 'dans cet apercu' : 'extraites'} - controle, correction et export au meme endroit.</p>
          </div>
          <span className="ui-tag">{truncated ? 'Apercu partiel' : invalidRows ? `${invalidRows} ligne(s) a verifier` : 'Pret a exporter'}</span>
        </div>
        <div className="conversion-metrics">
          {[
            { label: 'Solde initial reconstitue', value: opening, tone: '' },
            { label: truncated ? 'Credits affiches' : 'Total credits', value: totals.credit, tone: 'amount-credit' },
            { label: truncated ? 'Debits affiches' : 'Total debits', value: totals.debit, tone: 'amount-debit' },
            { label: 'Dernier solde', value: closing, tone: '' },
          ].map(metric => (
            <div className="conversion-metric" key={metric.label}>
              <span>{metric.label}</span>
              <strong className={metric.tone}>{money(metric.value)}</strong>
            </div>
          ))}
        </div>
        <div className="conversion-quality-grid">
          <div>
            <strong>{totals.corrections}</strong>
            <span>Corrections locales</span>
          </div>
          <div>
            <strong>{invalidRows}</strong>
            <span>Lignes a revoir</span>
          </div>
          <div>
            <strong>{money(totals.credit - totals.debit)}</strong>
            <span>Variation nette</span>
          </div>
        </div>
        <p className="conversion-note">
          <Info size={16} />
          {opening === null ? 'Aucun solde complet disponible : le rapprochement ne peut pas etre confirme.' : 'Le solde initial est reconstitue depuis la premiere operation.'}
          {' '}Les corrections restent locales dans cette interface avant export CSV corrige.
        </p>
      </section>

      <section className="conversion-section">
        <div className="conversion-heading">
          <div>
            <h3>Operations extraites</h3>
            <p className="ui-description">Filtrez, corrigez les champs detectes et exportez une version nettoyee.</p>
          </div>
          <div className="conversion-actions">
            {action}
            <Button variant="outline" onClick={() => setEditMode(value => !value)} aria-pressed={editMode}>
              <Edit3 /> {editMode ? 'Lecture seule' : 'Modifier'}
            </Button>
            <Button variant="outline" onClick={addRow}>
              <Plus /> Ajouter
            </Button>
            <Button variant="outline" disabled={!totals.corrections && rows.length === transactions.length} onClick={resetRows}>
              <RotateCcw /> Reinitialiser
            </Button>
            <Button disabled={!can(user, 'documents.export')} onClick={() => setExportRequest({ name: `transactions_corrigees_${new Date().toISOString().slice(0, 10)}.csv`, getBlob: async () => correctedCsv(rows) })}>
              <Download /> CSV corrige
            </Button>
          </div>
        </div>
        <div className="conversion-toolbar">
          <label className="conversion-search">
            <Search size={17} />
            <input aria-label="Rechercher une operation" placeholder="Libelle, reference ou date..." value={query} onChange={event => { setQuery(event.target.value); setPage(1); }} />
          </label>
          <label className="conversion-filter">
            <SlidersHorizontal size={16} />
            <select aria-label="Filtrer les operations" value={filter} onChange={event => { setFilter(event.target.value); setPage(1); }}>
              <option value="all">Toutes les operations</option>
              <option value="debit">Debits uniquement</option>
              <option value="credit">Credits uniquement</option>
              <option value="edited">Corrigees / ajoutees</option>
            </select>
          </label>
        </div>
        <div className="conversion-table-scroll">
          <table className="conversion-table conversion-table-editable">
            <caption className="sr-only">Operations extraites du releve bancaire</caption>
            <thead>
              <tr>
                {['Date', 'Reference', 'Libelle', 'Debit', 'Credit', 'Solde', 'Actions'].map(header => <th key={header} scope="col">{header}</th>)}
              </tr>
            </thead>
            <tbody>
              {pageRows.map(row => (
                <tr key={row.draftId} className={row.dirty || row.isNew ? 'row-edited' : ''}>
                  <td>{editMode ? <input aria-label="Date" type="date" value={row.date.slice(0, 10)} onChange={event => updateRow(row.draftId, 'date', event.target.value)} /> : row.date.slice(0, 10).split('-').reverse().join('/')}</td>
                  <td>{editMode ? <input aria-label="Reference" value={row.reference} onChange={event => updateRow(row.draftId, 'reference', event.target.value)} /> : <span className="muted">{row.reference || '-'}</span>}</td>
                  <td>{editMode ? <textarea aria-label="Libelle" rows={2} value={row.description} onChange={event => updateRow(row.draftId, 'description', event.target.value)} /> : row.description}</td>
                  <td>{editMode ? <input aria-label="Debit" inputMode="decimal" value={row.debit || ''} onChange={event => updateRow(row.draftId, 'debit', event.target.value)} /> : <span className="amount-debit">{row.debit ? money(row.debit) : '-'}</span>}</td>
                  <td>{editMode ? <input aria-label="Credit" inputMode="decimal" value={row.credit || ''} onChange={event => updateRow(row.draftId, 'credit', event.target.value)} /> : <span className="amount-credit">{row.credit ? money(row.credit) : '-'}</span>}</td>
                  <td>{editMode ? <input aria-label="Solde" inputMode="decimal" value={row.balance ?? ''} onChange={event => updateRow(row.draftId, 'balance', event.target.value)} /> : row.balance == null ? '-' : money(row.balance)}</td>
                  <td>
                    <div className="row-actions">
                      <button type="button" className="icon-button" title="Dupliquer" aria-label="Dupliquer la ligne" onClick={() => duplicateRow(row)}><Copy size={15} /></button>
                      <button type="button" className="icon-button danger-button" title="Supprimer" aria-label="Supprimer la ligne" onClick={() => removeRow(row.draftId)}><Trash2 size={15} /></button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {!filteredRows.length && <p className="load-state">Aucune operation ne correspond a votre recherche.</p>}
        </div>
        <div className="pagination">
          <span>{filteredRows.length} operations - Page {current} / {pages}</span>
          <div className="flex gap-2">
            <Button variant="outline" disabled={current === 1} onClick={() => setPage(current - 1)}>Precedent</Button>
            <Button variant="outline" disabled={current === pages} onClick={() => setPage(current + 1)}>Suivant</Button>
          </div>
        </div>
      </section>
    </div>
  );
}

export { standardTransactions } from '@/lib/standard-transactions';
