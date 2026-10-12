'use client';
import { useEffect, useRef, useState, useSyncExternalStore } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  CheckCheck,
  History,
  Download,
  Eye,
  FileOutput,
  Pencil,
  Save,
  ShieldCheck,
  AlertTriangle,
  ArrowUpRight,
  CheckCircle2,
  LoaderCircle,
  Copy,
  Plus,
  Settings2,
  WandSparkles,
  Trash2,
  Columns3,
  Undo2,
  GripVertical,
} from 'lucide-react';
import { Button } from './ui/button';
import { StatementEditableCell } from './statement-editable-cell';
import { cellKind, previewEditTarget } from '@/lib/statement-preview-editing';
import { statementPreviewRows } from '@/lib/statement-preview-rows';
import { ConversionSteps } from './conversion-steps';
import { StatementExtraction } from './statement-extraction';
import { StatementPdfPanel } from './statement-pdf-panel';
import type { PdfSelection } from '@/lib/pdf-selection';
import type { EditableTransactionField } from '@/lib/statement-row-editing';
import { API_URL, ApiError, ExportDiagnostic, ExportTemplate, request } from '@/services/api';
import {
  accountSuffix,
  BankStatement,
  date,
  downloadExport,
  ExportPreview,
  money,
  separator,
  StatementDetail,
  StatementTransaction,
} from '@/services/statement-exports';
import { useUnsavedChanges, confirmDiscard } from '@/lib/use-unsaved-changes';
import { useAuth } from './auth-context';
import { ExportArchive } from './export-archive';
import { StatementAssociation } from './statement-association';
import { ExportTemplateEditor } from './export-template-editor';
import { BankDialog } from './bank-dialogs';
import { StatementColumnEditor } from './statement-column-editor';
import { editTransactionValue, newStatementTransaction } from '@/lib/statement-row-editing';
import {
  canChooseExportLocation,
  chooseExportDestination,
  exportFileName,
} from '@/lib/save-export';

const subscribeToBrowserCapabilities = () => () => {};
const diagnosticFieldNames: Record<string, string> = {
  Date: 'Date opération',
  ValueDate: 'Date valeur',
  Description: 'Libellé',
  Amount: 'Montant',
  Debit: 'Débit',
  Credit: 'Crédit',
  Direction: 'Sens',
  OpeningBalance: 'Solde initial',
  ClosingBalance: 'Solde final',
  TotalDebit: 'Total débit',
  TotalCredit: 'Total crédit',
  Currency: 'Devise',
  StatementNumber: 'Numéro du relevé',
  AccountNumber: 'Compte bancaire',
  BankCode: 'Code banque',
  Journal: 'Journal',
  Account: 'Compte comptable',
  Balance: 'Solde',
  Sequence: 'Numéro de ligne',
  Reference: 'Référence',
};

function Diagnostics({
  errors,
  warnings,
  onTransaction,
  onStatement,
}: {
  errors: ExportDiagnostic[];
  warnings: ExportDiagnostic[];
  onTransaction?: (sequence: number) => void;
  onStatement?: () => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const items = [
    ...errors.map((e) => ({ ...e, blocking: true })),
    ...warnings.map((e) => ({ ...e, blocking: false })),
  ];
  if (!items.length)
    return (
      <div className="conversion-clean">
        <ShieldCheck size={18} />
        <span>Aucune anomalie détectée dans les données contrôlées.</span>
      </div>
    );
  return (
    <div className="conversion-diagnostics" aria-label="Diagnostics du relevé">
      <div className="conversion-section-heading">
        <h3>
          <AlertTriangle size={17} />
          Points à vérifier
        </h3>
        <span>
          {errors.length} erreur(s) · {warnings.length} avertissement(s)
        </span>
      </div>
      <ul>
        {(expanded ? items : items.slice(0, 12)).map((issue, index) => (
          <li
            key={`${issue.code}-${issue.transactionSequence}-${issue.fieldName}-${index}`}
            className={issue.blocking ? 'error' : 'warning'}
          >
            <span className="conversion-diagnostic-mark">{issue.blocking ? '!' : 'i'}</span>
            <div>
              <strong>
                {issue.transactionSequence ? `Transaction ${issue.transactionSequence}` : 'Relevé'}
                {issue.fieldName
                  ? ` · ${issue.fieldName}`
                  : issue.sourceField
                    ? ` · ${diagnosticFieldNames[issue.sourceField] ?? issue.sourceField}`
                    : ''}
              </strong>
              <p>{issue.message}</p>
              <small
                title={
                  issue.blocking
                    ? 'À corriger avant validation ou export'
                    : 'À vérifier avec le PDF original'
                }
              >
                {issue.code}
              </small>
            </div>
            {issue.transactionSequence && onTransaction && (
              <button
                type="button"
                className="conversion-text-action"
                onClick={() => onTransaction(issue.transactionSequence!)}
              >
                Corriger
              </button>
            )}
            {!issue.transactionSequence && onStatement && issue.blocking && (
              <button type="button" className="conversion-text-action" onClick={onStatement}>
                Corriger les informations
              </button>
            )}
          </li>
        ))}
      </ul>
      {items.length > 12 && (
        <button className="conversion-text-action mt-3" onClick={() => setExpanded(!expanded)}>
          {expanded ? 'Réduire' : `Voir les ${items.length} diagnostics`}
        </button>
      )}
    </div>
  );
}

export function StatementWorkspace({
  detail,
  templates,
  templateId,
  onTemplate,
  onSaved,
  onTemplatesChanged,
  guided = false,
  initialPreview,
  initialTab,
  onChooseTemplate,
  onImport,
}: {
  guided?: boolean;
  initialPreview?: ExportPreview;
  initialTab?: 'transactions' | 'preview';
  onChooseTemplate?: () => void;
  onImport?: () => void;
  detail: StatementDetail;
  templates: ExportTemplate[];
  templateId: string;
  onTemplate: (id: string) => void;
  onSaved: (tab?: 'transactions' | 'preview') => void;
  onTemplatesChanged: (id: string) => void;
}) {
  const { can } = useAuth();
  const diagnosticsRef = useRef<HTMLDivElement>(null);
  const flowHeaderRef = useRef<HTMLDivElement>(null);
  const documentRef = useRef<HTMLElement>(null);
  const [draft, setDraft] = useState<BankStatement>(detail.statement);
  const [pdfSelection, setPdfSelection] = useState<PdfSelection | null>(null);
  const dirty = draft !== detail.statement;
  const [editing, setEditing] = useState(false);
  const [focusRow, setFocusRow] = useState<string | null>(null);
  const draggingRow = useRef<string | null>(null);
  const [dropRow, setDropRow] = useState<string | null>(null);
  const [insertPosition, setInsertPosition] = useState('1');
  const [pendingCells, setPendingCells] = useState<Set<string>>(new Set());
  const [selectedRows, setSelectedRows] = useState<Set<number>>(new Set());
  const [columnEditor, setColumnEditor] = useState(false);
  const [extraColumns, setExtraColumns] = useState(false);
  const [deletingRows, setDeletingRows] = useState<Set<number> | null>(null);
  const [rowHistory, setRowHistory] = useState<StatementTransaction[][]>([]);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [actionErrors, setActionErrors] = useState<ExportDiagnostic[]>([]);
  const [preview, setPreview] = useState<ExportPreview | null>(initialPreview ?? null);
  const [flowStage, setFlowStage] = useState<3 | 4>(3);
  const router = useRouter();
  const [showPdfTools, setShowPdfTools] = useState(true);
  const [editedExportText, setEditedExportText] = useState<string | null>(null);
  const [exportTextSaved, setExportTextSaved] = useState(false);
  const [exportView, setExportView] = useState<'table' | 'text'>('table');
  const allowExportErrors = true;
  const textEdited = editedExportText !== null && editedExportText !== preview?.text;
  useUnsavedChanges(dirty || pendingCells.size > 0 || (textEdited && !exportTextSaved));
  const [templateEditor, setTemplateEditor] = useState<{
    initial: ExportTemplate | null;
    duplicate?: boolean;
  } | null>(null);
  const [, setReviewConfirmed] = useState(false);
  const [transactionPage, setTransactionPage] = useState(1);
  const [previewPage, setPreviewPage] = useState(1);
  const [tab, setTab] = useState<'transactions' | 'preview' | 'extraction' | 'history'>(
    detail.extraction?.needsMapping
      ? 'extraction'
      : (initialTab ?? (guided ? 'preview' : 'transactions')),
  );
  useEffect(() => {
    if (!focusRow || !['transactions', 'preview'].includes(tab)) return;
    const frame = requestAnimationFrame(() => {
      const row = document.querySelector(
        `#conversion-panel-${tab} [data-movement-id="${CSS.escape(focusRow)}"]`,
      );
      const cell = row?.querySelector<HTMLButtonElement>('.statement-cell-button');
      cell?.scrollIntoView({ block: 'nearest' });
      cell?.click();
      setFocusRow(null);
    });
    return () => cancelAnimationFrame(frame);
  }, [focusRow, tab, transactionPage, previewPage]);
  useEffect(() => {
    if ((!guided && initialTab !== 'preview') || initialPreview || !templateId) return;
    const controller = new AbortController();
    request<ExportPreview>(`/statement-imports/${detail.id}/exports/${templateId}/preview`, {
      method: 'POST',
      body: JSON.stringify({ statementVersion: detail.version }),
      signal: controller.signal,
    })
      .then((result) => {
        if (!controller.signal.aborted) setPreview(result);
      })
      .catch((reason: Error) => {
        if (!controller.signal.aborted) setError(reason.message);
      });
    return () => controller.abort();
  }, [guided, initialTab, initialPreview, templateId, detail.id, detail.version]);
  const [exportHistoryVersion, setExportHistoryVersion] = useState(0);
  const [downloadName, setDownloadName] = useState('');
  const [chooseLocation, setChooseLocation] = useState(true);
  const locationSupported = useSyncExternalStore(
    subscribeToBrowserCapabilities,
    canChooseExportLocation,
    () => false,
  );
  const model = templates.find((t) => t.id === templateId);
  const previewRows = preview
    ? statementPreviewRows(preview, detail.statement.transactions, draft.transactions)
    : [];
  const previewPages = Math.max(1, Math.ceil(previewRows.length / 50));
  const visiblePreviewPage = Math.min(previewPage, previewPages);
  const movementsById = new Map(draft.transactions.map((row) => [row.id, row]));
  const originalMovementsById = new Map(detail.statement.transactions.map((row) => [row.id, row]));
  const validated = detail.status === 'VALIDATED' && !dirty;
  function failed(e: unknown) {
    setError((e as Error).message);
    setActionErrors(e instanceof ApiError ? e.diagnostics : []);
    // Keep the edited table visible on a version conflict so corrections can be reviewed.
    if (e instanceof ApiError && e.status === 409 && !dirty) setPreview(null);
  }
  function correct(sequence: number) {
    const row = draft.transactions.find((t) => t.sequence === sequence);
    if (row) {
      setTab('transactions');
      setTransactionPage(Math.floor(draft.transactions.indexOf(row) / 50) + 1);
      setFocusRow(row.id);
    }
  }
  function update<K extends keyof BankStatement>(
    key: K,
    value: BankStatement[K],
    keepPreview = false,
  ) {
    if (textEdited && !exportTextSaved && !confirmDiscard(true)) return false;
    setDraft((previous) => ({ ...previous, [key]: value }));
    if (!keepPreview) setPreview(null);
    setEditedExportText(null);
    setExportTextSaved(false);
    setNotice('');
    setActionErrors([]);
    return true;
  }
  function commitRows(rows: StatementTransaction[], message: string, keepPreview = true) {
    if (!update('transactions', rows, keepPreview)) return false;
    setRowHistory((history) => [...history.slice(-4), draft.transactions]);
    setTransactionPage((page) => Math.min(page, Math.max(1, Math.ceil(rows.length / 50))));
    const sequences = new Set(rows.map((row) => row.sequence));
    setSelectedRows(
      (selected) => new Set([...selected].filter((sequence) => sequences.has(sequence))),
    );
    setReviewConfirmed(false);
    setError('');
    setNotice(message + ' Enregistrez les corrections pour les conserver.');
    return true;
  }
  function pendingCell(key: string, pending: boolean) {
    setPendingCells((previous) => {
      const next = new Set(previous);
      if (pending) next.add(key);
      else next.delete(key);
      return next;
    });
  }
  function editCell(
    row: StatementTransaction,
    field: EditableTransactionField,
    value: string | null,
    keepPreview = true,
  ) {
    return commitRows(
      draft.transactions.map((t) => (t.id === row.id ? editTransactionValue(t, field, value) : t)),
      `Ligne ${row.sequence} corrigée.`,
      keepPreview,
    );
  }
  function movementCell(
    row: StatementTransaction,
    field: EditableTransactionField,
    label: string,
    display: React.ReactNode,
  ) {
    return (
      <StatementEditableCell
        value={row[field]}
        display={display}
        label={`${label} · ligne ${row.sequence}`}
        kind={cellKind(field)}
        multiline={field === 'description'}
        signed={field === 'balance'}
        disabled={!!busy || !can('documents.write')}
        onPending={(pending) => pendingCell(`${row.id}:${field}`, pending)}
        onApply={(value) => editCell(row, field, value)}
      />
    );
  }
  function moveRow(row: StatementTransaction, position: number) {
    if (busy || pendingCells.size || !can('documents.write')) return false;
    if (!Number.isInteger(position) || position < 1 || position > draft.transactions.length)
      return false;
    const rows = draft.transactions.filter((t) => t.id !== row.id);
    rows.splice(position - 1, 0, row);
    if (
      !commitRows(
        rows.map((t, i) => ({ ...t, sequence: i + 1 })),
        'Ligne déplacée.',
      )
    )
      return false;
    setSelectedRows(new Set());
    setTransactionPage(Math.ceil(position / 50));
    setPreviewPage(
      Math.ceil((position + (previewRows[0]?.kind === 'OPENING_BALANCE' ? 1 : 0)) / 50),
    );
    return true;
  }
  function insertRow(position: number) {
    if (busy || pendingCells.size || !can('documents.write') || draft.transactions.length >= 50000)
      return;
    if (!Number.isInteger(position) || position < 1 || position > draft.transactions.length + 1) {
      setError(`Choisissez une position entre 1 et ${draft.transactions.length + 1}.`);
      return;
    }
    const row = newStatementTransaction(draft.transactions);
    const rows = [...draft.transactions];
    rows.splice(position - 1, 0, row);
    if (
      commitRows(
        rows.map((row, index) => ({ ...row, sequence: index + 1 })),
        `Ligne insérée à la position ${position}.`,
      )
    ) {
      setSelectedRows(new Set());
      setTransactionPage(Math.ceil(position / 50));
      setPreviewPage(
        Math.ceil((position + (previewRows[0]?.kind === 'OPENING_BALANCE' ? 1 : 0)) / 50),
      );
      setFocusRow(row.id);
    }
  }
  function rowDropEvents(row: StatementTransaction) {
    return {
      onDragOver: (event: React.DragEvent<HTMLTableRowElement>) => {
        if (!draggingRow.current || busy || pendingCells.size || !can('documents.write')) return;
        event.preventDefault();
        event.dataTransfer.dropEffect = 'move';
        setDropRow(row.id);
      },
      onDrop: (event: React.DragEvent<HTMLTableRowElement>) => {
        event.preventDefault();
        const source = draft.transactions.find((t) => t.id === draggingRow.current);
        draggingRow.current = null;
        setDropRow(null);
        if (source && source.id !== row.id) moveRow(source, draft.transactions.indexOf(row) + 1);
      },
    };
  }
  function rowControls(row: StatementTransaction) {
    if (!can('documents.write')) return null;
    const disabled = !!busy || pendingCells.size > 0;
    return (
      <div className="statement-row-controls">
        <button
          type="button"
          className="row-drag-handle"
          disabled={disabled}
          draggable={!disabled}
          aria-label={`Déplacer la ligne ${row.sequence} par glisser-déposer`}
          title="Glisser vers une autre ligne. La position permet aussi un déplacement au clavier."
          onDragStart={(event) => {
            draggingRow.current = row.id;
            event.dataTransfer.effectAllowed = 'move';
            event.dataTransfer.setData('text/plain', row.id);
          }}
          onDragEnd={() => {
            draggingRow.current = null;
            setDropRow(null);
          }}
        >
          <GripVertical size={16} />
        </button>
        <button
          type="button"
          className="row-insert-button"
          disabled={disabled || draft.transactions.length >= 50000}
          aria-label={`Insérer une ligne avant la ligne ${row.sequence}`}
          title="Insérer une ligne ici"
          onClick={() => insertRow(draft.transactions.indexOf(row) + 1)}
        >
          <Plus size={15} />
        </button>
        <label className="statement-row-position">
          Position
          <input
            key={`${row.id}-${row.sequence}`}
            type="number"
            min={1}
            max={draft.transactions.length}
            aria-label={`Position de la ligne ${row.sequence}`}
            disabled={disabled}
            defaultValue={draft.transactions.indexOf(row) + 1}
            onKeyDown={(event) => {
              if (event.key === 'Enter') event.currentTarget.blur();
            }}
            onBlur={(event) => {
              const position = Number(event.currentTarget.value);
              if (position !== draft.transactions.indexOf(row) + 1 && !moveRow(row, position))
                event.currentTarget.value = String(draft.transactions.indexOf(row) + 1);
            }}
          />
        </label>
      </div>
    );
  }
  function insertionToolbar() {
    if (!can('documents.write')) return null;
    return (
      <div className="row-insertion-toolbar">
        <label>
          Position d’insertion
          <input
            className="ui-input"
            type="number"
            min={1}
            max={draft.transactions.length + 1}
            aria-label="Position d’insertion"
            value={insertPosition}
            disabled={!!busy || pendingCells.size > 0}
            onChange={(event) => setInsertPosition(event.target.value)}
          />
        </label>
        <Button
          variant="outline"
          size="sm"
          disabled={!!busy || pendingCells.size > 0 || draft.transactions.length >= 50000}
          onClick={() => insertRow(Number(insertPosition))}
        >
          <Plus size={15} />
          Insérer à cette position
        </Button>
      </div>
    );
  }
  function undoRows() {
    const previous = rowHistory.at(-1);
    if (!previous) return;
    if (!update('transactions', previous, true)) return;
    setRowHistory((history) => history.slice(0, -1));
    setSelectedRows(new Set());
    setTransactionPage((page) => Math.min(page, Math.max(1, Math.ceil(previous.length / 50))));
    setNotice('Dernière modification des lignes annulée.');
  }
  async function save() {
    if (pendingCells.size || !can('documents.write')) return;
    setBusy('save');
    setError('');
    setActionErrors([]);
    try {
      await request(`/statement-imports/${detail.id}?version=${detail.version}`, {
        method: 'PUT',
        body: JSON.stringify(draft),
      });
      onSaved(tab === 'preview' ? 'preview' : 'transactions');
    } catch (e) {
      failed(e);
    } finally {
      setBusy(null);
    }
  }
  async function completeMetadata() {
    if (textEdited && !exportTextSaved && !confirmDiscard(true)) return;
    setBusy('metadata');
    setError('');
    setActionErrors([]);
    try {
      await request(`/statement-imports/${detail.id}/complete-metadata`, {
        method: 'POST',
        body: JSON.stringify({ version: detail.version }),
      });
      onSaved();
    } catch (e) {
      failed(e);
    } finally {
      setBusy(null);
    }
  }
  function configureDirection() {
    if (!model || busy || dirty || !can('templates.write')) return;
    setTemplateEditor({
      initial: {
        ...model,
        fields: model.fields.map((field) =>
          field.fieldName.trim().toLocaleLowerCase('fr') === 'sens' &&
          field.sourceField === 'Amount'
            ? { ...field, sourceField: 'Direction', format: null, required: true }
            : field,
        ),
      },
    });
  }
  async function showPreview() {
    if (!model || dirty || pendingCells.size) return;
    if (textEdited && !exportTextSaved && !confirmDiscard(true)) return;
    setBusy('preview');
    setError('');
    setActionErrors([]);
    setNotice('');
    try {
      const result = await request<ExportPreview>(
        `/statement-imports/${detail.id}/exports/${model.id}/preview`,
        { method: 'POST', body: JSON.stringify({ statementVersion: detail.version }) },
      );
      setPreview(result);
      setEditedExportText(null);
      setExportTextSaved(false);
      setTab('preview');
    } catch (e) {
      failed(e);
    } finally {
      setBusy(null);
    }
  }
  async function download() {
    if (!model || !model.isActive || dirty || busy || pendingCells.size) return;
    setBusy('download');
    setError('');
    setActionErrors([]);
    setNotice('');
    const checkedVersion = detail.version;
    try {
      const destination = await chooseExportDestination(
        exportFileName(downloadName || 'Alias-export', model.fileExtension),
        model.fileExtension,
        chooseLocation,
      );
      // Always check the current model and statement. Validation remounts this view,
      // and template settings may change after an earlier preview.
      const current = await request<ExportPreview>(
        `/statement-imports/${detail.id}/exports/${model.id}/preview`,
        { method: 'POST', body: JSON.stringify({ statementVersion: checkedVersion }) },
      );
      if (
        textEdited &&
        (!preview ||
          current.statementVersion !== preview.statementVersion ||
          current.templateFingerprint !== preview.templateFingerprint)
      ) {
        setError(
          'Le modèle ou le relevé a changé. Régénérez l’aperçu avant de télécharger le texte modifié.',
        );
        return;
      }
      setPreview(current);
      let actualEncoding = model.encoding;
      const name = await downloadExport(detail.id, model.id, current, {
        destination: downloadName.trim() || destination.handle ? destination : undefined,
        allowErrors: true,
        editedText: textEdited ? editedExportText : null,
        onEncoding: (encoding) => {
          actualEncoding = encoding;
        },
      });
      setNotice(
        `${name} téléchargé.${actualEncoding !== model.encoding ? ` Encodage effectif : ${actualEncoding.toUpperCase()}.` : ''}`,
      );
      setExportHistoryVersion((v) => v + 1);
      await request(`/statement-imports/${detail.id}/download-complete`, {
        method: 'POST',
        body: JSON.stringify({ version: current.statementVersion }),
      });
      router.push(
        `/client-workspace${detail.clientId ? '?clientId=' + encodeURIComponent(detail.clientId) : ''}`,
      );
      setExportTextSaved(true);
    } catch (e) {
      if ((e as Error).name !== 'AbortError') failed(e);
    } finally {
      setBusy(null);
    }
  }
  function metadata(key: keyof BankStatement, label: string, type = 'text') {
    return (
      <label key={key}>
        {label}
        <input
          type={type}
          className="ui-input mt-2"
          value={String(draft[key] ?? '')}
          onChange={(e) => update(key, e.target.value || null)}
        />
      </label>
    );
  }
  function balance(
    key: 'openingBalance' | 'closingBalance' | 'totalDebit' | 'totalCredit',
    label: string,
  ) {
    return (
      <label key={key}>
        {label}
        <input
          className="ui-input mt-2"
          inputMode="decimal"
          pattern="-?[0-9]+([.][0-9]+)?"
          value={String(draft[key] ?? '')}
          onChange={(e) => update(key, e.target.value.replace(',', '.') || null)}
        />
      </label>
    );
  }
  const visibleRows = draft.transactions.slice((transactionPage - 1) * 50, transactionPage * 50);
  const errors = actionErrors.length ? actionErrors : (preview?.errors ?? detail.errors);
  const warnings = (preview?.warnings ?? detail.warnings).filter(
    (issue) => !guided || issue.code !== 'STATEMENT_NOT_VALIDATED',
  );
  const needsAdaptation = detail.extraction?.needsMapping || draft.transactions.length === 0;
  const amountAsDirection =
    model?.type === 'SAGE100' &&
    model.fields.some(
      (field) =>
        field.fieldName.trim().toLocaleLowerCase('fr') === 'sens' && field.sourceField === 'Amount',
    );
  function correctStatement() {
    setEditing(true);
    documentRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
  return (
    <div
      className={`conversion-workspace ${detail.sourceFileName ? 'has-pdf-panel' : ''} ${guided ? `conversion-guided stage-${flowStage}` : ''}`}
    >
      {guided && (
        <div ref={flowHeaderRef} className="conversion-flow-header">
          <ConversionSteps
            current={flowStage}
            disabled={!!busy || pendingCells.size > 0}
            onNavigate={(step) => {
              if (step === 1) onImport?.();
              else if (step === 2) onChooseTemplate?.();
              else if (step === 3) setFlowStage(3);
            }}
          />
          {flowStage === 4 && error && (
            <p role="alert" className="conversion-alert error">
              {error}
            </p>
          )}
          {flowStage === 4 && notice && (
            <p role="status" className="conversion-alert success">
              {notice}
            </p>
          )}
        </div>
      )}
      {columnEditor && (
        <StatementColumnEditor
          rows={draft.transactions}
          selected={selectedRows}
          onClose={() => setColumnEditor(false)}
          onApply={(field, value, synchronize, scope) => {
            const count = scope === 'all' ? draft.transactions.length : selectedRows.size;
            commitRows(
              draft.transactions.map((row) =>
                scope === 'all' || selectedRows.has(row.sequence)
                  ? editTransactionValue(row, field, value, synchronize)
                  : row,
              ),
              `${count} ligne(s) modifiée(s).`,
            );
          }}
        />
      )}
      {deletingRows && (
        <BankDialog title="Supprimer des lignes" onClose={() => setDeletingRows(null)}>
          <div className="space-y-4">
            <p>Supprimer {deletingRows.size} ligne(s) du brouillon du relevé ?</p>
            <ul className="row-delete-summary">
              {draft.transactions
                .filter((row) => deletingRows.has(row.sequence))
                .slice(0, 3)
                .map((row) => (
                  <li key={row.sequence}>
                    <strong>Ligne {row.sequence}</strong>
                    <span>{row.description || 'Sans libellé'}</span>
                  </li>
                ))}
            </ul>
            <p className="muted text-sm">
              Vous pourrez annuler cette action avant l’enregistrement. La validation vérifiera
              ensuite les totaux et les soldes.
            </p>
            <div className="flex justify-end gap-3">
              <Button variant="outline" onClick={() => setDeletingRows(null)}>
                Annuler
              </Button>
              <Button
                variant="destructive"
                onClick={() => {
                  commitRows(
                    draft.transactions.filter((row) => !deletingRows.has(row.sequence)),
                    `${deletingRows.size} ligne(s) supprimée(s).`,
                  );
                  setDeletingRows(null);
                }}
              >
                Supprimer les lignes
              </Button>
            </div>
          </div>
        </BankDialog>
      )}
      <div
        className={`conversion-review-layout ${detail.sourceFileName && showPdfTools ? 'has-pdf-panel' : 'pdf-panel-hidden'}`}
      >
        <div className="conversion-main">
          <div className="conversion-next-step">
            <span className="conversion-next-icon">
              <ShieldCheck size={22} />
            </span>
            <div>
              <strong>
                {needsAdaptation
                  ? 'Préparez les colonnes du relevé'
                  : validated
                    ? 'Votre relevé est validé'
                    : 'Vérifiez les mouvements extraits'}
              </strong>
              <p>
                {needsAdaptation
                  ? 'Associez les colonnes aux dates, libellés et montants pour préparer les mouvements.'
                  : dirty
                    ? 'Enregistrez vos corrections, puis vérifiez les soldes avant de valider.'
                    : validated
                      ? 'Choisissez votre modèle d’export, vérifiez l’aperçu et téléchargez le fichier.'
                      : 'Comparez les dates, les montants et les soldes avec votre PDF, puis validez le relevé.'}
              </p>
            </div>
            <span className="ui-tag">
              {needsAdaptation ? 'Préparation' : validated ? 'Export' : 'Vérification'}
            </span>
          </div>
          <section ref={documentRef} className="conversion-document-card">
            <details open={!guided || editing}>
              <summary className="guided-document-summary" hidden={!guided}>
                {draft.bankName} · Informations du relevé
              </summary>
              <div className="conversion-document-heading">
                <span className="conversion-bank-icon">
                  <FileOutput size={24} />
                </span>
                <div>
                  <p className="conversion-eyebrow">RELEVÉ NORMALISÉ</p>
                  <h2>{draft.bankName}</h2>
                  <p>
                    {draft.statementNumber || 'Sans numéro de relevé'}
                    <span>·</span>
                    {accountSuffix(draft.accountNumber)}
                  </p>
                </div>
                <span className={`conversion-status ${validated ? 'validated' : 'review'}`}>
                  {validated ? <CheckCircle2 size={14} /> : <Pencil size={14} />}{' '}
                  {validated ? 'Validé' : dirty ? 'Modifications en cours' : 'À vérifier'}
                </span>
              </div>
              <div className="conversion-summary">
                <div>
                  <span>Période</span>
                  <strong>
                    {date(draft.periodStart)} — {date(draft.periodEnd)}
                  </strong>
                </div>
                <div>
                  <span>Mouvements</span>
                  <strong>{draft.transactions.length}</strong>
                </div>
                <div>
                  <span>Devise</span>
                  <strong>{draft.currency || '—'}</strong>
                </div>
                <div>
                  <span>Version</span>
                  <strong>{detail.version}</strong>
                </div>
              </div>
              <div className="conversion-balances">
                <div>
                  <span>Solde initial</span>
                  <strong>
                    {money(draft.openingBalance)} <small>{draft.currency}</small>
                  </strong>
                </div>
                <span className="conversion-balance-connector" aria-hidden="true">
                  →
                </span>
                <div>
                  <span>Solde final</span>
                  <strong>
                    {money(draft.closingBalance)} <small>{draft.currency}</small>
                  </strong>
                </div>
              </div>
              <div className="conversion-document-actions">
                <p className="muted text-xs">
                  Informations remplies automatiquement depuis le PDF ; vous pouvez les corriger.
                  Les totaux peuvent être calculés à partir des mouvements et le numéro généré selon
                  la période.
                </p>
                {detail.extraction && (
                  <Button
                    permission="documents.write"
                    variant="outline"
                    size="sm"
                    disabled={!!busy || dirty}
                    onClick={() => void completeMetadata()}
                  >
                    <WandSparkles size={14} />
                    {busy === 'metadata'
                      ? 'Lecture des informations…'
                      : 'Compléter les champs vides'}
                  </Button>
                )}
                {detail.sourceFileName && (
                  <Button asChild variant="outline" size="sm">
                    <a
                      href={`${API_URL}/statement-imports/${detail.id}/original`}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      <Eye size={14} /> Voir le PDF original
                    </a>
                  </Button>
                )}
                <Button
                  permission="documents.write"
                  variant="outline"
                  size="sm"
                  disabled={!!busy || pendingCells.size > 0}
                  onClick={() => setEditing(!editing)}
                >
                  <Pencil size={14} />
                  {editing ? 'Masquer les réglages' : 'Corriger les informations'}
                </Button>
              </div>
              <div className="px-5 pb-5">
                <StatementAssociation
                  detail={detail}
                  onSaved={onSaved}
                  disabled={dirty || !!busy || (textEdited && !exportTextSaved)}
                />
              </div>
              {editing && (
                <form
                  className="conversion-metadata"
                  onSubmit={(e) => {
                    e.preventDefault();
                    void save();
                  }}
                >
                  <fieldset disabled={!!busy || pendingCells.size > 0}>
                    <div className="grid gap-4 sm:grid-cols-2">
                      {metadata('bankName', 'Banque')}
                      {metadata('bankCode', 'Code banque')}
                      {metadata('statementNumber', 'Numéro du relevé')}
                      {metadata('currency', 'Devise')}
                      {metadata('periodStart', 'Début de période', 'date')}
                      {metadata('periodEnd', 'Fin de période', 'date')}
                      {balance('openingBalance', 'Solde initial')}
                      {balance('closingBalance', 'Solde final')}
                      {balance('totalDebit', 'Total débit')}
                      {balance('totalCredit', 'Total crédit')}
                      <label className="sm:col-span-2">
                        Convention des soldes
                        <select
                          aria-label="Convention des soldes"
                          className="ui-input mt-2"
                          value={draft.balanceConvention || ''}
                          onChange={(e) => update('balanceConvention', e.target.value || null)}
                        >
                          <option value="">Non renseignée</option>
                          <option value="CREDIT_INCREASES">Les crédits augmentent le solde</option>
                          <option value="DEBIT_INCREASES">Les débits augmentent le solde</option>
                        </select>
                      </label>
                    </div>
                    <details className="conversion-details mt-4">
                      <summary>Dates et libellés des soldes · Sage X3</summary>
                      <p className="muted text-xs mt-2">
                        Vérifiez les dates dans le PDF. Si elles sont vides, les dates de début et
                        de fin de période sont utilisées. La date de valeur reprend la date du solde
                        lorsqu’elle est absente. Les libellés restent conservés ici. Par défaut,
                        leur colonne est retirée des lignes de solde Sage X3 ; ce comportement se
                        règle dans le modèle.
                      </p>
                      <div className="mt-4 grid gap-4 sm:grid-cols-2">
                        {metadata('openingBalanceDate', 'Date du solde initial', 'date')}
                        {metadata(
                          'openingBalanceValueDate',
                          'Date valeur du solde initial',
                          'date',
                        )}
                        {metadata('openingBalanceDescription', 'Libellé du solde initial')}
                        {metadata('closingBalanceDate', 'Date du solde final', 'date')}
                        {metadata('closingBalanceValueDate', 'Date valeur du solde final', 'date')}
                        {metadata('closingBalanceDescription', 'Libellé du solde final')}
                      </div>
                    </details>
                    <details className="conversion-details mt-4">
                      <summary>Sources complémentaires du relevé</summary>
                      <p className="muted text-xs mt-2">
                        Renseignez des valeurs explicites. Elles sont utilisées uniquement lorsque
                        la transaction ne fournit pas sa propre valeur.
                      </p>
                      <div className="mt-4 grid gap-4 sm:grid-cols-2">
                        {metadata('journal', 'Journal')}
                        {metadata('account', 'Compte comptable')}
                        {metadata('analytic', 'Analytique')}
                        {metadata('recordType', 'Type de ligne')}
                      </div>
                    </details>
                    <Button
                      className="mt-4"
                      type="submit"
                      disabled={!dirty || !!busy || pendingCells.size > 0}
                    >
                      <Save size={15} />
                      Enregistrer les corrections
                    </Button>
                  </fieldset>
                </form>
              )}
            </details>
          </section>
          {error && (
            <div role="alert" className="conversion-alert error">
              {error}
            </div>
          )}
          {notice && (
            <div role="status" className="conversion-alert success">
              {notice}
            </div>
          )}
          {dirty && (
            <div className="conversion-unsaved">
              <div>
                <strong>Des corrections sont en attente</strong>
                <p>
                  Leur enregistrement remet le relevé en revue et nécessite une nouvelle validation.
                </p>
              </div>
              <Button
                permission="documents.write"
                disabled={!!busy || pendingCells.size > 0}
                onClick={() => void save()}
              >
                <Save size={15} />
                {busy === 'save' ? 'Enregistrement…' : 'Enregistrer les corrections'}
              </Button>
            </div>
          )}
          <div
            ref={diagnosticsRef}
            className="conversion-diagnostic-anchor"
            tabIndex={-1}
            aria-label="Points à vérifier"
          >
            <Diagnostics
              errors={errors}
              warnings={warnings}
              onTransaction={can('documents.write') ? correct : undefined}
              onStatement={can('documents.write') ? correctStatement : undefined}
            />
          </div>
          {needsAdaptation && detail.extraction && (
            <div className="conversion-alert warning">
              <div>
                <strong>L’export attend les mouvements du relevé.</strong>
                <p>
                  Les données brutes ont été conservées. Relancez l’adaptation automatique pour
                  récupérer les mouvements et les soldes avant de préparer le fichier.
                </p>
              </div>
              <Button
                variant="outline"
                disabled={!!busy || dirty}
                onClick={() => setTab('extraction')}
              >
                Adapter le relevé
              </Button>
            </div>
          )}
          <section className="conversion-data-card">
            <div className="conversion-tabs" role="tablist" aria-label="Données et export">
              {(
                [
                  ['transactions', 'Mouvements', CheckCheck],
                  ['preview', 'Aperçu du fichier', Eye],
                  ...(detail.extraction
                    ? [['extraction', 'Texte lu & adaptation', Eye] as const]
                    : []),
                  ['history', 'Historique', History],
                ] as const
              ).map(([value, label, Icon]) => (
                <button
                  key={value}
                  role="tab"
                  tabIndex={tab === value ? 0 : -1}
                  aria-selected={tab === value}
                  aria-controls={`conversion-panel-${value}`}
                  id={`conversion-tab-${value}`}
                  disabled={pendingCells.size > 0}
                  onClick={() => setTab(value)}
                  onKeyDown={(event) => {
                    const tabs = Array.from(
                      event.currentTarget.parentElement!.querySelectorAll<HTMLButtonElement>(
                        '[role="tab"]',
                      ),
                    );
                    const index = tabs.indexOf(event.currentTarget);
                    const target =
                      event.key === 'ArrowRight'
                        ? (index + 1) % tabs.length
                        : event.key === 'ArrowLeft'
                          ? (index + tabs.length - 1) % tabs.length
                          : event.key === 'Home'
                            ? 0
                            : event.key === 'End'
                              ? tabs.length - 1
                              : null;
                    if (target !== null) {
                      event.preventDefault();
                      tabs[target].click();
                      tabs[target].focus();
                    }
                  }}
                >
                  <Icon size={16} />
                  {label}
                  {value === 'transactions' && <span>{draft.transactions.length}</span>}
                </button>
              ))}
            </div>
            {tab === 'extraction' && (
              <div
                role="tabpanel"
                id="conversion-panel-extraction"
                aria-labelledby="conversion-tab-extraction"
              >
                <StatementExtraction
                  detail={detail}
                  dirty={dirty}
                  onAdapted={onSaved}
                  pdfSelection={pdfSelection}
                />
              </div>
            )}
            {tab === 'transactions' && (
              <div
                role="tabpanel"
                id="conversion-panel-transactions"
                aria-labelledby="conversion-tab-transactions"
              >
                <div className="conversion-table-heading">
                  <div>
                    <h3>Mouvements du relevé</h3>
                    <p>Vérifiez les dates, montants et sens avant validation.</p>
                  </div>
                  <span>
                    {Math.min((transactionPage - 1) * 50 + 1, draft.transactions.length)}–
                    {Math.min(transactionPage * 50, draft.transactions.length)} /{' '}
                    {draft.transactions.length}
                  </span>
                </div>
                {can('documents.write') && (
                  <div className="row-editor-toolbar" aria-label="Modification des lignes">
                    <div className="row-editor-actions">
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={
                          !!busy || pendingCells.size > 0 || draft.transactions.length >= 50000
                        }
                        onClick={() => insertRow(draft.transactions.length + 1)}
                      >
                        <Plus size={15} />
                        Ajouter une ligne
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={!!busy || pendingCells.size > 0 || !draft.transactions.length}
                        onClick={() => setColumnEditor(true)}
                      >
                        <Columns3 size={15} />
                        Modifier une colonne
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={!!busy || pendingCells.size > 0}
                        aria-pressed={extraColumns}
                        onClick={() => setExtraColumns(!extraColumns)}
                      >
                        {extraColumns
                          ? 'Masquer les champs complémentaires'
                          : 'Champs complémentaires'}
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={!!busy || pendingCells.size > 0 || !rowHistory.length}
                        onClick={undoRows}
                      >
                        <Undo2 size={15} />
                        Annuler la dernière action
                      </Button>
                    </div>
                    {insertionToolbar()}
                    {!!selectedRows.size && (
                      <div className="row-selection-bar">
                        <span>{selectedRows.size} ligne(s) sélectionnée(s), toutes les pages</span>
                        <Button
                          variant="danger"
                          size="sm"
                          disabled={!!busy || pendingCells.size > 0}
                          onClick={() => setDeletingRows(new Set(selectedRows))}
                        >
                          <Trash2 size={15} />
                          Supprimer la sélection
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          disabled={!!busy || pendingCells.size > 0}
                          onClick={() => setSelectedRows(new Set())}
                        >
                          Désélectionner
                        </Button>
                      </div>
                    )}
                    <p>
                      Cliquez sur une cellule pour la corriger. Glissez une ligne avec sa poignée ou
                      changez sa position. Le bouton + insère une ligne avant celle choisie. Entrée
                      applique la saisie, Échap l’annule. Enregistrez ensuite les corrections.
                    </p>
                  </div>
                )}
                <div
                  className="conversion-table-scroll"
                  tabIndex={0}
                  aria-label="Tableau des mouvements"
                >
                  <table className="conversion-table">
                    <thead>
                      <tr>
                        {can('documents.write') && (
                          <th className="row-selection-cell">
                            <input
                              type="checkbox"
                              aria-label="Sélectionner toutes les lignes"
                              disabled={
                                !!busy || pendingCells.size > 0 || !draft.transactions.length
                              }
                              checked={
                                draft.transactions.length > 0 &&
                                selectedRows.size === draft.transactions.length
                              }
                              onChange={(event) =>
                                setSelectedRows(
                                  event.target.checked
                                    ? new Set(draft.transactions.map((row) => row.sequence))
                                    : new Set(),
                                )
                              }
                            />
                          </th>
                        )}
                        <th>N°</th>
                        <th>Date</th>
                        <th>Date valeur</th>
                        <th>Libellé</th>
                        <th>Débit</th>
                        <th>Crédit</th>
                        <th>Sens</th>
                        <th>Solde</th>
                        {extraColumns &&
                          [
                            'Montant',
                            'Devise',
                            'Journal',
                            'Compte comptable',
                            'Analytique',
                            'Type de ligne',
                          ].map((label) => <th key={label}>{label}</th>)}
                        <th>
                          <span className="sr-only">Correction</span>
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {visibleRows.map((row) => (
                        <tr
                          key={row.id || `sequence-${row.sequence}`}
                          data-movement-id={row.id}
                          data-drop-target={dropRow === row.id || undefined}
                          {...rowDropEvents(row)}
                          data-selected={selectedRows.has(row.sequence) || undefined}
                          className={
                            errors.some((e) => e.transactionSequence === row.sequence)
                              ? 'has-error'
                              : ''
                          }
                        >
                          {can('documents.write') && (
                            <td className="row-selection-cell">
                              <input
                                type="checkbox"
                                aria-label={`Sélectionner la ligne ${row.sequence}`}
                                disabled={!!busy || pendingCells.size > 0}
                                checked={selectedRows.has(row.sequence)}
                                onChange={(event) => {
                                  setSelectedRows((previous) => {
                                    const next = new Set(previous);
                                    if (event.target.checked) next.add(row.sequence);
                                    else next.delete(row.sequence);
                                    return next;
                                  });
                                }}
                              />
                            </td>
                          )}
                          <td className="conversion-row-number">
                            {row.sequence}
                            {rowControls(row)}
                          </td>
                          <td>{movementCell(row, 'date', 'Date opération', date(row.date))}</td>
                          <td>
                            {movementCell(row, 'valueDate', 'Date valeur', date(row.valueDate))}
                          </td>
                          <td className="conversion-description">
                            {movementCell(
                              row,
                              'description',
                              'Libellé',
                              row.description || 'Non renseigné',
                            )}
                            {movementCell(
                              row,
                              'reference',
                              'Référence',
                              <span className="muted">
                                {row.reference || 'Référence facultative'}
                              </span>,
                            )}
                          </td>
                          <td className="conversion-money">
                            {movementCell(row, 'debit', 'Débit', money(row.debit))}
                          </td>
                          <td className="conversion-money credit">
                            {movementCell(row, 'credit', 'Crédit', money(row.credit))}
                          </td>
                          <td>
                            {movementCell(
                              row,
                              'direction',
                              'Sens',
                              <span
                                className={`conversion-direction ${row.direction?.toLowerCase() || 'unknown'}`}
                              >
                                {row.direction || '—'}
                              </span>,
                            )}
                          </td>
                          <td className="conversion-money">
                            {movementCell(row, 'balance', 'Solde', money(row.balance))}
                          </td>
                          {extraColumns && (
                            <>
                              <td className="conversion-money">
                                {movementCell(row, 'amount', 'Montant', money(row.amount))}
                              </td>
                              <td>{movementCell(row, 'currency', 'Devise', row.currency)}</td>
                              <td>{movementCell(row, 'journal', 'Journal', row.journal)}</td>
                              <td>
                                {movementCell(row, 'account', 'Compte comptable', row.account)}
                              </td>
                              <td>{movementCell(row, 'analytic', 'Analytique', row.analytic)}</td>
                              <td>
                                {movementCell(row, 'recordType', 'Type de ligne', row.recordType)}
                              </td>
                            </>
                          )}
                          <td>
                            <div className="row-inline-actions">
                              <button
                                className="conversion-row-edit"
                                disabled={!!busy || !can('documents.write')}
                                aria-label={`Corriger la transaction ${row.sequence}`}
                                title="Modifier directement les cellules de la ligne"
                                onClick={() => correct(row.sequence)}
                              >
                                <Pencil size={14} />
                              </button>
                              {can('documents.write') && (
                                <button
                                  className="conversion-row-edit danger-row-action"
                                  disabled={!!busy || pendingCells.size > 0}
                                  aria-label={`Supprimer la ligne ${row.sequence}`}
                                  onClick={() => setDeletingRows(new Set([row.sequence]))}
                                >
                                  <Trash2 size={15} />
                                </button>
                              )}
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                {draft.transactions.length > 50 && (
                  <nav className="conversion-pagination" aria-label="Pagination des mouvements">
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={transactionPage === 1 || pendingCells.size > 0}
                      onClick={() => setTransactionPage(transactionPage - 1)}
                    >
                      Précédent
                    </Button>
                    <span>Page {transactionPage}</span>
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={
                        transactionPage * 50 >= draft.transactions.length || pendingCells.size > 0
                      }
                      onClick={() => setTransactionPage(transactionPage + 1)}
                    >
                      Suivant
                    </Button>
                  </nav>
                )}
              </div>
            )}
            {tab === 'preview' && (
              <div
                role="tabpanel"
                id="conversion-panel-preview"
                aria-labelledby="conversion-tab-preview"
              >
                {!preview ? (
                  <div className="conversion-preview-empty">
                    <Eye size={30} />
                    <h3>Visualisez votre fichier avant de l’exporter</h3>
                    <p>
                      Sélectionnez un modèle, puis lancez l’aperçu pour contrôler les colonnes et le
                      format.
                    </p>
                    <Button
                      variant="outline"
                      disabled={!model || !!busy || dirty || pendingCells.size > 0}
                      onClick={() => void showPreview()}
                    >
                      Afficher l’aperçu
                    </Button>
                  </div>
                ) : (
                  <>
                    <div className="conversion-table-heading">
                      <div>
                        <h3>{preview.template.name}</h3>
                        <p>
                          {textEdited
                            ? 'Texte modifié manuellement · les diagnostics portent sur les mouvements enregistrés.'
                            : dirty
                              ? `${previewRows.length} lignes dans le brouillon · contrôles recalculés après enregistrement`
                              : `${preview.rows.length} premières lignes sur ${preview.totalRows} · toutes les lignes ont été contrôlées`}
                        </p>
                      </div>
                    </div>
                    {amountAsDirection && (
                      <section
                        className="conversion-auto-repair"
                        aria-label="Correspondance du sens"
                      >
                        <Settings2 size={19} aria-hidden="true" />
                        <div>
                          <strong>La colonne « Sens » utilise le montant</strong>
                          <p>
                            Pour obtenir D / C, appliquez la source « Sens » au modèle. Les autres
                            colonnes sont conservées.
                          </p>
                        </div>
                        <Button
                          permission="templates.write"
                          size="sm"
                          variant="outline"
                          disabled={!!busy || dirty || (textEdited && !exportTextSaved)}
                          onClick={configureDirection}
                        >
                          Configurer le sens D/C
                        </Button>
                      </section>
                    )}
                    <div
                      className="export-preview-switch"
                      role="group"
                      aria-label="Présentation du fichier"
                    >
                      <Button
                        size="sm"
                        variant={exportView === 'table' ? 'default' : 'outline'}
                        aria-pressed={exportView === 'table'}
                        onClick={() => setExportView('table')}
                      >
                        Vue tableau
                      </Button>
                      <Button
                        size="sm"
                        variant={exportView === 'text' ? 'default' : 'outline'}
                        aria-pressed={exportView === 'text'}
                        disabled={dirty || pendingCells.size > 0}
                        onClick={() => setExportView('text')}
                      >
                        Texte du fichier
                      </Button>
                      {guided && flowStage === 3 && (
                        <Button
                          variant="outline"
                          size="sm"
                          disabled={!!busy || pendingCells.size > 0}
                          onClick={onChooseTemplate}
                        >
                          Changer de modèle
                        </Button>
                      )}
                      <Button
                        permission="documents.export"
                        size="sm"
                        disabled={!!busy || dirty || pendingCells.size > 0 || !model?.isActive}
                        onClick={() => void download()}
                      >
                        <Download size={16} />{' '}
                        {busy === 'download' ? 'Préparation…' : 'Télécharger'}
                      </Button>
                      {dirty && (
                        <Button
                          permission="documents.write"
                          size="sm"
                          disabled={!!busy || pendingCells.size > 0}
                          onClick={() => void save()}
                        >
                          <Save size={15} /> Enregistrer les corrections
                        </Button>
                      )}
                      <span>
                        {textEdited
                          ? 'Texte modifié : le téléchargement utilisera vos modifications.'
                          : dirty
                            ? 'Corrections en attente : enregistrez pour recalculer le fichier et ses contrôles.'
                            : 'Cliquez sur une cellule pour corriger sa valeur. Les champs calculés suivent le modèle.'}
                      </span>
                    </div>
                    {exportView === 'table' && (
                      <div className="row-editor-toolbar">
                        {insertionToolbar()}
                        {can('documents.write') && (
                          <div className="row-editor-actions">
                            <Button
                              size="sm"
                              variant="outline"
                              disabled={
                                !!busy ||
                                pendingCells.size > 0 ||
                                draft.transactions.length >= 50000
                              }
                              onClick={() => insertRow(draft.transactions.length + 1)}
                            >
                              <Plus size={15} />
                              Ajouter une ligne
                            </Button>
                            <Button
                              size="sm"
                              variant="outline"
                              disabled={!!busy || pendingCells.size > 0 || !rowHistory.length}
                              onClick={undoRows}
                            >
                              <Undo2 size={15} />
                              Annuler la dernière action
                            </Button>
                          </div>
                        )}
                      </div>
                    )}
                    {exportView === 'text' ? (
                      <div className="export-text-editor">
                        <label htmlFor={`export-text-${detail.id}`}>
                          Contenu du fichier à télécharger
                        </label>
                        <p>
                          Modifiez directement les dates, valeurs, lignes ou séparateurs. Le texte
                          modifié concerne ce fichier ; les mouvements du relevé restent accessibles
                          dans leur onglet.
                        </p>
                        {preview.textTruncated && (
                          <p className="conversion-preview-warning">
                            Texte abrégé : ce fichier dépasse la limite d’édition de 2 000 000
                            caractères. Le téléchargement généré conserve toutes les lignes.
                          </p>
                        )}
                        <textarea
                          id={`export-text-${detail.id}`}
                          className="ui-input export-file-text"
                          spellCheck={false}
                          maxLength={2000000}
                          readOnly={
                            !can('documents.export') ||
                            !!preview.textTruncated ||
                            preview.text == null ||
                            !!busy
                          }
                          value={editedExportText ?? preview.text ?? ''}
                          onChange={(event) => {
                            setEditedExportText(
                              preview.text?.includes('\r\n')
                                ? event.target.value.replace(/\r?\n/g, '\r\n')
                                : event.target.value,
                            );
                            setExportTextSaved(false);
                            setError('');
                            setActionErrors([]);
                          }}
                        />
                        <div className="export-text-footer">
                          <span>
                            {(editedExportText ?? preview.text ?? '').length.toLocaleString(
                              'fr-FR',
                            )}{' '}
                            caractères · {textEdited ? 'modification manuelle' : 'texte généré'}
                          </span>
                          <Button
                            size="sm"
                            variant="outline"
                            disabled={!textEdited || !!busy}
                            onClick={() => {
                              if (confirmDiscard(textEdited)) setEditedExportText(null);
                            }}
                          >
                            Rétablir le texte généré
                          </Button>
                        </div>
                        {textEdited && (
                          <p className="conversion-preview-warning">
                            La vue tableau décrit les données générées. Le fichier téléchargé
                            contiendra le texte modifié visible ici.
                          </p>
                        )}
                      </div>
                    ) : (
                      <>
                        <div
                          className="conversion-table-scroll"
                          tabIndex={0}
                          aria-label="Aperçu du fichier exporté"
                        >
                          <table className="conversion-table preview">
                            <thead>
                              <tr>
                                <th>Ligne</th>
                                {preview.columns.map((column, index) => (
                                  <th key={`${column}-${index}`}>{column}</th>
                                ))}
                              </tr>
                            </thead>
                            <tbody>
                              {previewRows
                                .slice((visiblePreviewPage - 1) * 50, visiblePreviewPage * 50)
                                .map((entry, visibleIndex) => {
                                  const index = (visiblePreviewPage - 1) * 50 + visibleIndex;
                                  const row = entry.cells;
                                  const movement = movementsById.get(entry.movementId ?? '');
                                  const originalMovement = originalMovementsById.get(
                                    entry.movementId ?? '',
                                  );
                                  return (
                                    <tr
                                      key={entry.movementId ?? entry.kind}
                                      data-movement-id={movement?.id}
                                      data-drop-target={
                                        (movement && dropRow === movement.id) || undefined
                                      }
                                      {...(movement ? rowDropEvents(movement) : {})}
                                    >
                                      <td className="conversion-row-number">
                                        {entry.kind === 'OPENING_BALANCE'
                                          ? 'Solde initial'
                                          : entry.kind === 'CLOSING_BALANCE'
                                            ? 'Solde final'
                                            : entry.sequence}
                                        {movement && rowControls(movement)}
                                      </td>
                                      {preview.columns.map((_, column) => {
                                        const indexInRow = entry.columns?.indexOf(column) ?? column;
                                        const cell = indexInRow < 0 ? undefined : row[indexInRow];
                                        const source = model?.fields
                                          .slice()
                                          .sort((a, b) => a.position - b.position)[
                                          column
                                        ]?.sourceField;
                                        const rowKind = entry.kind;
                                        const target =
                                          source && indexInRow >= 0
                                            ? previewEditTarget(
                                                draft,
                                                movement,
                                                source,
                                                rowKind,
                                                model?.type ?? '',
                                              )
                                            : null;
                                        const originalTarget = source
                                          ? previewEditTarget(
                                              detail.statement,
                                              originalMovement,
                                              source,
                                              rowKind,
                                              model?.type ?? '',
                                            )
                                          : null;
                                        const changed =
                                          target &&
                                          String(target.value ?? '') !==
                                            String(originalTarget?.value ?? '');
                                        const rowLabel =
                                          rowKind === 'OPENING_BALANCE'
                                            ? 'solde initial'
                                            : rowKind === 'CLOSING_BALANCE'
                                              ? 'solde final'
                                              : `ligne ${entry.sequence}`;
                                        const key = `preview:${entry.movementId ?? entry.kind}:${column}`;
                                        const display =
                                          target && (changed || !entry.formatted)
                                            ? target.kind === 'date'
                                              ? date(String(target.value ?? '') || null)
                                              : target.kind === 'money'
                                                ? money(target.value)
                                                : String(target.value ?? '')
                                            : cell;

                                        return (
                                          <td
                                            key={column}
                                            className={`conversion-preview-cell ${model?.fields.some((field) => field.fieldName === preview.columns[column] && field.sourceField === 'Description') || /libell|description|designation|détails|details/i.test(preview.columns[column]) ? 'conversion-preview-description' : ''} ${indexInRow < 0 ? 'omitted' : ''}`}
                                            title={cell || undefined}
                                          >
                                            {indexInRow < 0 ? (
                                              <span
                                                className="conversion-empty-cell"
                                                title="Cette colonne est retirée de cette ligne dans le fichier téléchargé."
                                              >
                                                Non exporté
                                              </span>
                                            ) : target ? (
                                              <StatementEditableCell
                                                value={target.value}
                                                display={display}
                                                kind={target.kind}
                                                signed={target.signed}
                                                multiline={target.field
                                                  .toLowerCase()
                                                  .includes('description')}
                                                label={`${preview.columns[column]} · ${rowLabel}${target.signed ? ' (montant signé)' : ''}`}
                                                disabled={!!busy || !can('documents.write')}
                                                onPending={(pending) => pendingCell(key, pending)}
                                                onApply={(value) =>
                                                  target.scope === 'transaction' && movement
                                                    ? editCell(
                                                        movement,
                                                        target.field as EditableTransactionField,
                                                        value,
                                                        true,
                                                      )
                                                    : update(
                                                        target.field as keyof BankStatement,
                                                        value,
                                                        true,
                                                      )
                                                }
                                              />
                                            ) : (
                                              <span title="Champ calculé par le modèle ou sans source modifiable">
                                                {(source === 'Sequence'
                                                  ? String(
                                                      model?.type === 'SAGEX3'
                                                        ? index + 1
                                                        : entry.sequence,
                                                    )
                                                  : source === 'RecordType' &&
                                                      model?.type === 'SAGEX3'
                                                    ? rowKind === 'OPENING_BALANCE'
                                                      ? '1'
                                                      : rowKind === 'CLOSING_BALANCE'
                                                        ? '3'
                                                        : '2'
                                                    : cell) || (
                                                  <span className="conversion-empty-cell">
                                                    vide
                                                  </span>
                                                )}
                                              </span>
                                            )}
                                          </td>
                                        );
                                      })}
                                    </tr>
                                  );
                                })}
                            </tbody>
                          </table>
                        </div>
                        {previewPages > 1 && (
                          <nav className="conversion-pagination" aria-label="Pages de l’aperçu">
                            <span>
                              Page {visiblePreviewPage} sur {previewPages} · {previewRows.length}{' '}
                              lignes
                            </span>
                            <Button
                              variant="outline"
                              size="sm"
                              disabled={visiblePreviewPage <= 1 || !!busy || pendingCells.size > 0}
                              onClick={() => setPreviewPage(visiblePreviewPage - 1)}
                            >
                              Précédent
                            </Button>
                            <Button
                              variant="outline"
                              size="sm"
                              disabled={
                                visiblePreviewPage >= previewPages ||
                                !!busy ||
                                pendingCells.size > 0
                              }
                              onClick={() => setPreviewPage(visiblePreviewPage + 1)}
                            >
                              Suivant
                            </Button>
                          </nav>
                        )}
                      </>
                    )}
                  </>
                )}
              </div>
            )}
            {tab === 'history' && (
              <div
                role="tabpanel"
                id="conversion-panel-history"
                aria-labelledby="conversion-tab-history"
              >
                <ExportArchive statementId={detail.id} refreshKey={exportHistoryVersion} />
              </div>
            )}
          </section>
        </div>
        {detail.sourceFileName && (
          <StatementPdfPanel
            collapsed={!showPdfTools}
            onToggle={() => setShowPdfTools(!showPdfTools)}
            id={detail.id}
            hasExtraction={!!detail.extraction}
            rows={draft.transactions}
            disabled={!!busy || pendingCells.size > 0}
            onSelection={setPdfSelection}
            onApply={(sequence, field, value) => {
              if (!can('documents.write') || busy) return false;
              if (sequence === null) {
                if (!update(field as keyof BankStatement, value)) return false;
                setReviewConfirmed(false);
                setError('');
                return true;
              }
              const changed = commitRows(
                draft.transactions.map((row) =>
                  row.sequence === sequence
                    ? editTransactionValue(row, field as EditableTransactionField, value)
                    : row,
                ),
                `Ligne ${sequence} corrigée depuis le PDF.`,
              );
              if (changed)
                setTransactionPage(
                  Math.max(
                    1,
                    Math.ceil(
                      (draft.transactions.findIndex((row) => row.sequence === sequence) + 1) / 50,
                    ),
                  ),
                );
              return changed;
            }}
          />
        )}
      </div>
      <aside className="conversion-export-panel" aria-label="Paramètres de l’export">
        {templateEditor && (
          <ExportTemplateEditor
            initial={templateEditor.initial}
            duplicate={templateEditor.duplicate}
            defaultType={model?.type || 'SAGEX3'}
            onClose={() => setTemplateEditor(null)}
            onSaved={(saved) => {
              setPreview(null);
              setActionErrors([]);
              setError('');
              setNotice('Modèle enregistré. L’aperçu utilisera sa nouvelle configuration.');
              onTemplatesChanged(saved.id);
            }}
          />
        )}
        <div className="conversion-export-header">
          <span>
            <FileOutput size={22} />
          </span>
          <div>
            <h2>
              {guided
                ? flowStage === 4
                  ? 'Votre fichier est prêt'
                  : 'Contrôle du résultat'
                : 'Préparer l’export'}
            </h2>
            <p>
              {guided
                ? flowStage === 4
                  ? 'Choisissez le nom du fichier et téléchargez.'
                  : 'Vérifiez les champs du modèle, puis validez.'
                : 'Choisissez le format, vérifiez les données, puis téléchargez.'}
            </p>
          </div>
        </div>
        <div className="conversion-export-steps">
          {guided ? (
            <section className="export-step guided-model-summary" aria-label="Modèle choisi">
              <span className="conversion-eyebrow">MODÈLE CHOISI</span>
              <h3>{model?.name || 'Modèle indisponible'}</h3>
              <p className="muted text-sm">
                {model
                  ? `${model.fields.length} colonnes · .${model.fileExtension}`
                  : 'Choisissez un modèle actif pour continuer.'}
              </p>
              {flowStage !== 3 && (
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!!busy || pendingCells.size > 0}
                  onClick={onChooseTemplate}
                >
                  Changer de modèle
                </Button>
              )}
            </section>
          ) : (
            <section className="export-step export-step-model" aria-label="Choisir le format">
              <h3 className="export-step-title">
                <span>1</span> Choisir le format
              </h3>
              <label className="conversion-template-label">
                Modèle d’exportation
                <select
                  aria-label="Modèle d’exportation"
                  className="ui-input mt-2"
                  disabled={!!busy || pendingCells.size > 0}
                  value={templateId}
                  onChange={(e) => {
                    if (textEdited && !exportTextSaved && !confirmDiscard(true)) return;
                    onTemplate(e.target.value);
                    setPreview(null);
                    setEditedExportText(null);
                    setExportTextSaved(false);

                    setActionErrors([]);
                    setError('');
                  }}
                >
                  <option value="">Choisir un modèle…</option>
                  {templates.map((t) => (
                    <option key={t.id} value={t.id} disabled={!t.isActive}>
                      {t.name}
                      {!t.isActive ? ' (inactif)' : ''}
                    </option>
                  ))}
                </select>
              </label>
              {can('templates.write') && (
                <div className="conversion-template-actions">
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={!model || !!busy || dirty || pendingCells.size > 0}
                    onClick={() => model && setTemplateEditor({ initial: model })}
                  >
                    <Settings2 size={14} />
                    Configurer
                  </Button>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={!model || !!busy || dirty || pendingCells.size > 0}
                    onClick={() => model && setTemplateEditor({ initial: model, duplicate: true })}
                  >
                    <Copy size={14} />
                    Dupliquer
                  </Button>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={!!busy || dirty}
                    onClick={() => setTemplateEditor({ initial: null })}
                  >
                    <Plus size={14} />
                    Nouveau modèle
                  </Button>
                </div>
              )}
              {model ? (
                <div className="conversion-format-card">
                  <div className="conversion-format-badge">
                    {model.type === 'CUSTOMCSV'
                      ? 'CSV'
                      : model.type === 'SAGEX3'
                        ? 'SAGE X3'
                        : 'SAGE 100'}
                    <span>.{model.fileExtension}</span>
                  </div>
                  <p className="export-format-summary">
                    {model.fields.length} colonnes · {separator(model.delimiter)} · .
                    {model.fileExtension}
                  </p>
                  <details className="export-technical-details">
                    <summary>Détails techniques du modèle</summary>
                    <dl>
                      <div>
                        <dt>Encodage</dt>
                        <dd>{model.encoding.toUpperCase()}</dd>
                      </div>
                      <div>
                        <dt>Séparateur</dt>
                        <dd>{separator(model.delimiter)}</dd>
                      </div>
                      <div>
                        <dt>En-tête</dt>
                        <dd>{model.type !== 'SAGE100' && model.includeHeader ? 'Oui' : 'Non'}</dd>
                      </div>
                      <div>
                        <dt>Culture des nombres</dt>
                        <dd>{model.numberCulture}</dd>
                      </div>
                      <div>
                        <dt>Colonnes</dt>
                        <dd>{model.fields.length}</dd>
                      </div>
                    </dl>
                    <div className="conversion-column-chips">
                      {model.fields
                        .slice()
                        .sort((a, b) => a.position - b.position)
                        .map((field, index) => (
                          <span
                            key={`${field.position}-${index}`}
                            title={`${field.sourceField}${field.required ? ' · obligatoire' : ''}`}
                          >
                            {field.fieldName}
                          </span>
                        ))}
                    </div>
                  </details>
                </div>
              ) : (
                <div className="conversion-model-placeholder">
                  <FileOutput size={28} />
                  <p>Choisissez la structure du fichier à produire.</p>
                </div>
              )}
              <details className="export-format-help">
                <summary>Comment fonctionne ce format ?</summary>
                {model?.type === 'SAGE100' && (
                  <p className="conversion-x3-note">
                    Sage 100 : une ligne par mouvement, montant absolu et sens D/C si la colonne
                    utilise la source Direction. Le fichier commence directement par les valeurs,
                    sans ligne d’en-tête. Les dates, colonnes et séparateur suivent le modèle
                    sélectionné.
                  </p>
                )}
                {model?.type === 'SAGEX3' && (
                  <p className="conversion-x3-note">
                    Sage X3 : solde initial (numéro 1), mouvements (2), solde final (3). Le numéro
                    interne augmente à chaque ligne. Par défaut, la colonne Libellé est retirée des
                    deux lignes de solde : 9 valeurs par solde, 10 par mouvement. Vous pouvez
                    modifier ce choix dans le modèle. Sens : crédit 1, débit -1 ; solde positif ou
                    nul -1, solde négatif 1. Numéro de relevé : RB + code banque + mois et année de
                    la date d’opération. Attijariwafa : ATT ; Crédit Agricole : CA ; Crédit du Maroc
                    : CDM.
                  </p>
                )}
                {model?.type === 'CUSTOMCSV' && (
                  <p className="conversion-x3-note">
                    Un fichier CSV contient une ligne par mouvement. Les colonnes et leur ordre
                    suivent le modèle choisi.
                  </p>
                )}
                {!model && (
                  <p className="muted text-xs mt-2">
                    Sélectionnez un modèle pour consulter les règles de son format.
                  </p>
                )}
              </details>
              <Link className="conversion-manage-link" href="/templates">
                Gérer les modèles <ArrowUpRight size={14} />
              </Link>
            </section>
          )}
          {guided && flowStage === 3 ? (
            <section
              className="export-step guided-result-next"
              aria-label="Terminer la vérification"
            >
              <Button
                variant="outline"
                disabled={!!busy || dirty || !model}
                onClick={() => void showPreview()}
              >
                <Eye size={16} /> Actualiser le résultat
              </Button>
            </section>
          ) : (
            <section
              className="export-step export-step-download"
              aria-label="Télécharger le fichier"
            >
              {guided && (
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!!busy || pendingCells.size > 0}
                  onClick={() => setFlowStage(3)}
                >
                  Revenir au résultat
                </Button>
              )}
              <h3 className="export-step-title">
                <span>{guided ? '4' : '3'}</span> Télécharger le fichier
              </h3>
              {model && can('documents.export') && (
                <div className="space-y-3 mt-4 mb-4">
                  <label className="conversion-template-label">
                    Nom du fichier
                    <input
                      className="ui-input mt-2"
                      value={downloadName}
                      maxLength={180}
                      placeholder="Nom automatique si laissé vide"
                      disabled={!!busy || pendingCells.size > 0}
                      onChange={(event) => setDownloadName(event.target.value)}
                      aria-describedby="export-filename-help"
                    />
                  </label>
                  <p id="export-filename-help" className="conversion-download-note">
                    L’extension .{model.fileExtension} est ajoutée automatiquement. Laissez vide
                    pour le nom automatique.
                  </p>
                  {locationSupported ? (
                    <label className="conversion-confirm">
                      <input
                        type="checkbox"
                        checked={chooseLocation}
                        disabled={!!busy || pendingCells.size > 0}
                        onChange={(event) => setChooseLocation(event.target.checked)}
                      />
                      <span>Choisir l’emplacement avec « Enregistrer sous »</span>
                    </label>
                  ) : (
                    <p className="conversion-download-note">
                      Pour choisir le dossier, activez « Toujours demander où enregistrer les
                      fichiers » dans les paramètres de téléchargement de votre navigateur.
                    </p>
                  )}
                </div>
              )}
              <div className="conversion-export-actions">
                <Button
                  variant="outline"
                  disabled={!model || !!busy || dirty || pendingCells.size > 0}
                  onClick={() => void showPreview()}
                >
                  <Eye size={17} />
                  {busy === 'preview' ? 'Vérification…' : 'Aperçu'}
                </Button>
                <Button
                  disabled={
                    !model?.isActive ||
                    !!busy ||
                    dirty ||
                    pendingCells.size > 0 ||
                    (!allowExportErrors &&
                      !!preview &&
                      preview.template.id === model.id &&
                      preview.errors.some((issue) => issue.code !== 'STATEMENT_NOT_VALIDATED'))
                  }
                  permission="documents.export"
                  onClick={() => void download()}
                >
                  {busy === 'download' ? (
                    <LoaderCircle size={17} className="animate-spin" />
                  ) : (
                    <Download size={17} />
                  )}
                  {busy === 'download'
                    ? 'Préparation…'
                    : allowExportErrors
                      ? 'Télécharger avec erreurs'
                      : 'Télécharger'}
                </Button>
              </div>
              <p className="conversion-download-note">
                {!can('documents.export')
                  ? 'Votre profil ne dispose pas de la permission de télécharger les exports.'
                  : !model
                    ? 'Sélectionnez un modèle Sage X3, Sage 100 ou CSV pour préparer votre fichier.'
                    : allowExportErrors
                      ? 'Le fichier sera archivé avec la mention « à vérifier ». Les diagnostics et le statut du relevé restent conservés.'
                      : textEdited
                        ? 'Le téléchargement utilisera le texte modifié visible dans l’aperçu.'
                        : needsAdaptation
                          ? 'Adaptez le relevé pour renseigner les mouvements et les soldes.'
                          : !validated
                            ? 'Vérifiez puis validez le relevé pour autoriser le téléchargement.'
                            : preview?.errors.length
                              ? 'Corrigez les erreurs indiquées pour télécharger le fichier.'
                              : 'Le fichier respecte les colonnes et réglages enregistrés. Le compte est masqué dans son nom.'}
              </p>
            </section>
          )}
        </div>
      </aside>
    </div>
  );
}
