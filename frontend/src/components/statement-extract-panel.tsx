'use client';

import { useEffect, useState } from 'react';
import { Columns3, LoaderCircle, RotateCcw, Save, Search } from 'lucide-react';
import { Button } from './ui/button';
import { request, type ExportTemplate } from '@/services/api';
import {
  money,
  type ExportColumnFilter,
  type ExportPreview,
  type StatementDetail,
  type StatementExtract,
} from '@/services/statement-exports';
import { exportFilterRows, extractTotals } from '@/lib/statement-extract';

export function StatementExtractPanel({
  detail,
  model,
  filters,
  onFilters,
  selectedExtractId,
  onSelected,
  disabled,
  dirty,
  onSaved,
  onBusy,
}: {
  detail: StatementDetail;
  model?: ExportTemplate;
  filters: ExportColumnFilter[];
  onFilters: (filters: ExportColumnFilter[]) => void;
  selectedExtractId: string;
  onSelected: (extract: StatementExtract | null) => void;
  disabled: boolean;
  dirty: boolean;
  onSaved: () => void;
  onBusy: (busy: boolean) => void;
}) {
  const [result, setResult] = useState<{
    preview?: ExportPreview;
    selection?: ExportPreview;
    error?: string;
  }>({});
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState('');
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const extracts = detail.statement.extracts ?? [];
  const selected = extracts.find((extract) => extract.id === selectedExtractId);
  const modelId = model?.id;
  const fields = [...(model?.fields ?? [])].sort((a, b) => a.position - b.position);
  function columnClass(index: number) {
    const source = fields[index]?.sourceField ?? '';
    if (source.includes('Description')) return 'is-description';
    if (
      ['Debit', 'Credit', 'Amount', 'Balance', 'OpeningBalance', 'ClosingBalance'].includes(source)
    )
      return 'is-number';
    return undefined;
  }
  const schema = JSON.stringify(model);
  useEffect(() => {
    if (!modelId) return;
    const controller = new AbortController();
    const read = (extractId?: string) =>
      request<ExportPreview>(`/statement-imports/${detail.id}/exports/${modelId}/preview`, {
        method: 'POST',
        body: JSON.stringify({ statementVersion: detail.version, extractId }),
        signal: controller.signal,
      });
    Promise.all([read(), selectedExtractId ? read(selectedExtractId) : Promise.resolve(undefined)])
      .then(([preview, selection]) => {
        if (!controller.signal.aborted) setResult({ preview, selection });
      })
      .catch((error: Error) => {
        if (!controller.signal.aborted) setResult({ error: error.message });
      });
    return () => controller.abort();
  }, [modelId, schema, detail.id, detail.version, revision, selectedExtractId]);
  const preview = result.preview?.template.id === modelId ? result.preview : undefined;
  const active = filters.filter((filter) => filter.value.trim());
  const savedSchemaChanged = !!(
    selected?.columnFilters?.length &&
    selected.templateId === modelId &&
    preview &&
    selected.templateFingerprint !== preview.templateFingerprint
  );
  const storedSelection =
    !!selected &&
    (!active.length ||
      savedSchemaChanged ||
      (selected.templateId === modelId &&
        JSON.stringify(filters) === JSON.stringify(selected.columnFilters ?? [])));
  const selection =
    result.selection?.extractId === selectedExtractId ? result.selection : undefined;
  const loadingSelection = storedSelection && !selection;
  const entries =
    preview && !loadingSelection
      ? exportFilterRows(storedSelection ? selection! : preview, storedSelection ? [] : filters)
      : [];
  const sequences = new Set(entries.map((entry) => entry.sequence));
  const totals = extractTotals(
    storedSelection
      ? selected!.transactions
      : detail.statement.transactions.filter((row) => sequences.has(row.sequence)),
  );
  const pages = Math.max(1, Math.ceil(entries.length / 50));
  const currentPage = Math.min(page, pages);
  function change(next: ExportColumnFilter[]) {
    setPage(1);
    setSaveError('');
    onFilters(next);
    if (!next.some((filter) => filter.value.trim()) && selected) onSelected(null);
  }
  async function save() {
    if (
      !preview ||
      dirty ||
      savedSchemaChanged ||
      storedSelection ||
      !active.length ||
      !entries.length
    )
      return;
    setSaving(true);
    onBusy(true);
    setSaveError('');
    try {
      await request(`/statement-imports/${detail.id}/extracts`, {
        method: 'POST',
        body: JSON.stringify({
          statementVersion: detail.version,
          templateId: modelId,
          columnFilters: active,
          templateFingerprint: preview.templateFingerprint,
        }),
      });
      onSaved();
    } catch (reason) {
      setSaveError((reason as Error).message);
    } finally {
      setSaving(false);
      onBusy(false);
    }
  }
  return (
    <section className="export-column-filter" aria-label="Filtres des colonnes du modèle">
      <div className="export-filter-heading">
        <div className="export-filter-title">
          <span>
            <Columns3 size={18} />
          </span>
          <div>
            <h3>{model?.name ?? 'Choisir un modèle d’exportation'}</h3>
            <p>Un filtre par colonne · les champs suivent votre modèle.</p>
          </div>
        </div>
        <span className="export-field-count">
          {preview?.columns.length ?? model?.fields.length ?? 0} champs
        </span>
      </div>
      <div className="export-filter-toolbar">
        <p id="export-filter-help">
          <Search size={15} />
          Recherchez dans les cellules ci-dessous. Les filtres se combinent, sans distinction de
          casse ou d’accents.
        </p>
        <div className="export-filter-actions">
          <Button
            size="sm"
            variant="outline"
            disabled={disabled || saving || (!filters.length && !selected)}
            onClick={() => {
              change([]);
              onSelected(null);
            }}
          >
            <RotateCcw size={14} />
            Tout afficher
          </Button>
          <Button
            size="sm"
            permission="documents.write"
            disabled={
              disabled ||
              saving ||
              dirty ||
              !preview ||
              !active.length ||
              !entries.length ||
              savedSchemaChanged ||
              storedSelection
            }
            onClick={() => void save()}
          >
            {saving ? <LoaderCircle size={14} className="animate-spin" /> : <Save size={14} />}
            Enregistrer la sélection
          </Button>
        </div>
      </div>
      {extracts.length > 0 && (
        <label className="export-saved-selection">
          Sélection enregistrée
          <select
            className="ui-input"
            value={selected?.id ?? ''}
            disabled={disabled || saving}
            onChange={(event) => {
              const extract = extracts.find((item) => item.id === event.target.value) ?? null;
              change(
                extract && extract.templateId === modelId ? (extract.columnFilters ?? []) : [],
              );
              onSelected(extract);
            }}
          >
            <option value="">Tous les mouvements</option>
            {extracts.map((extract) => (
              <option key={extract.id} value={extract.id}>
                {extract.filterText || 'Sélection'} · {extract.transactions.length} mouvements
              </option>
            ))}
          </select>
        </label>
      )}
      {savedSchemaChanged && (
        <p className="export-filter-notice" role="status">
          Le modèle a changé depuis cette sauvegarde. La sélection enregistrée est conservée.
          Cliquez sur « Tout afficher » pour définir de nouveaux filtres.
        </p>
      )}
      {selected && selected.templateId !== modelId && (
        <p className="export-filter-notice">
          Sélection enregistrée conservée. Utilisez « Tout afficher » pour filtrer les colonnes du
          modèle actuel.
        </p>
      )}
      {dirty && (
        <p className="export-filter-notice" role="status">
          Enregistrez les corrections des données source pour actualiser les valeurs et filtrer.
        </p>
      )}
      {!model ? (
        <p className="export-filter-empty">
          Choisissez un modèle pour afficher ses colonnes et ses filtres.
        </p>
      ) : (!preview || loadingSelection) && !result.error ? (
        <p className="export-filter-empty" role="status">
          <LoaderCircle size={18} className="animate-spin" />
          Chargement des colonnes du modèle…
        </p>
      ) : (
        preview && (
          <>
            <div
              className="export-filter-scroll"
              tabIndex={0}
              role="region"
              aria-label="Tableau filtrable du modèle d’exportation"
            >
              <table className="export-filter-table">
                <caption className="sr-only">
                  Mouvements au format {model.name}. Filtres sur toutes les lignes, avant
                  pagination.
                </caption>
                <thead>
                  <tr>
                    {preview.columns.map((column, index) => (
                      <th scope="col" key={index} className={columnClass(index)}>
                        <input
                          type="search"
                          className="ui-input"
                          aria-label={`Filtrer ${column}, colonne ${index + 1}`}
                          aria-describedby="export-filter-help"
                          placeholder="Rechercher…"
                          maxLength={2000}
                          value={
                            filters.find((filter) => filter.columnIndex === index)?.value ?? ''
                          }
                          disabled={disabled || saving || dirty || savedSchemaChanged}
                          onChange={(event) =>
                            change(
                              [
                                ...filters.filter((filter) => filter.columnIndex !== index),
                                ...(event.target.value
                                  ? [{ columnIndex: index, value: event.target.value }]
                                  : []),
                              ].sort((a, b) => a.columnIndex - b.columnIndex),
                            )
                          }
                        />
                        <span>{column}</span>
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {entries.slice((currentPage - 1) * 50, currentPage * 50).map((entry) => (
                    <tr key={entry.sequence}>
                      {preview.columns.map((_, index) => (
                        <td key={index} className={columnClass(index)}>
                          {entry.cells[entry.columns.indexOf(index)] || (
                            <span className="export-filter-blank">—</span>
                          )}
                        </td>
                      ))}
                    </tr>
                  ))}
                  {!entries.length && (
                    <tr>
                      <td
                        colSpan={Math.max(1, preview.columns.length)}
                        className="export-filter-empty"
                      >
                        Aucun mouvement ne correspond à ces filtres. Modifiez une recherche ou
                        affichez tout.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
            <div className="export-filter-footer">
              <span role="status">
                {storedSelection
                  ? `${entries.length} mouvements · sélection enregistrée`
                  : `${entries.length} / ${detail.statement.transactions.length} mouvements`}
                {active.length > 0 && ` · ${active.length} filtre${active.length > 1 ? 's' : ''}`}
              </span>
              {pages > 1 && (
                <nav aria-label="Pagination de la sélection">
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={currentPage === 1}
                    onClick={() => setPage(currentPage - 1)}
                  >
                    Précédent
                  </Button>
                  <span>
                    {currentPage} / {pages}
                  </span>
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={currentPage === pages}
                    onClick={() => setPage(currentPage + 1)}
                  >
                    Suivant
                  </Button>
                </nav>
              )}
            </div>
            <dl className="export-filter-totals">
              <div>
                <dt>Total débit</dt>
                <dd>
                  {money(totals.totalDebit)} <small>{detail.statement.currency}</small>
                </dd>
              </div>
              <div>
                <dt>Total crédit</dt>
                <dd>
                  {money(totals.totalCredit)} <small>{detail.statement.currency}</small>
                </dd>
              </div>
              <div>
                <dt>Solde de la sélection</dt>
                <dd>
                  {money(totals.balance)} <small>{detail.statement.currency}</small>
                </dd>
              </div>
            </dl>
          </>
        )
      )}
      {(result.error || saveError) && (
        <div role="alert" className="export-filter-error">
          <p>{saveError || result.error}</p>
          <Button
            size="sm"
            variant="outline"
            onClick={() => {
              setResult({});
              setSaveError('');
              onFilters([]);
              onSelected(null);
              setRevision((value) => value + 1);
            }}
          >
            Actualiser les colonnes
          </Button>
        </div>
      )}
    </section>
  );
}
