'use client';

import { useState } from 'react';
import { Save } from 'lucide-react';
import { Button } from './ui/button';
import { request } from '@/services/api';
import {
  money,
  type StatementDetail,
  type StatementExtract,
  type StatementTransaction,
} from '@/services/statement-exports';
import { extractTotals } from '@/lib/statement-extract';

export function StatementExtractPanel({
  detail,
  rows,
  filter,
  onFilter,
  excludeText,
  onExclusion,
  selectedExtractId,
  onSelected,
  onBusy,
  disabled,
  dirty,
  onSaved,
}: {
  detail: StatementDetail;
  rows: StatementTransaction[];
  filter: string;
  onFilter: (text: string) => void;
  excludeText: string;
  onExclusion: (text: string) => void;
  selectedExtractId: string;
  onSelected: (extract: StatementExtract) => void;
  onBusy?: (busy: boolean) => void;
  disabled: boolean;
  dirty: boolean;
  onSaved: (reset?: boolean) => void;
}) {
  const extracts = detail.statement.extracts ?? [];
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [saved, setSaved] = useState<StatementExtract | null>(null);
  const selected = saved ?? extracts.find((extract) => extract.id === selectedExtractId);
  const totals = extractTotals(rows);
  function markBusy(value: boolean) {
    setBusy(value);
    onBusy?.(value);
  }

  async function save() {
    markBusy(true);
    setError('');
    try {
      const result = await request<StatementDetail>(`/statement-imports/${detail.id}/extracts`, {
        method: 'POST',
        body: JSON.stringify({ statementVersion: detail.version, filterText: filter, excludeText }),
      });
      setSaved(result.statement.extracts?.at(-1) ?? null);
      onSaved();
    } catch (reason) {
      setError((reason as Error).message);
    } finally {
      markBusy(false);
    }
  }

  async function resetSaved() {
    if (!selected) return;
    markBusy(true);
    setError('');
    try {
      await request(
        `/statement-imports/${detail.id}/extracts/${selected.id}?version=${detail.version}`,
        { method: 'DELETE' },
      );
      setSaved(null);
      onFilter('');
      onExclusion('');
      onSaved(true);
    } catch (reason) {
      setError((reason as Error).message);
    } finally {
      markBusy(false);
    }
  }

  return (
    <section
      className="row-editor-toolbar movement-filter-toolbar"
      aria-label="Filtrage et extraits"
    >
      <p className="muted text-sm">
        <strong>Filtrage facultatif.</strong> La conversion traite toutes les opérations du PDF
        fourni. Enregistrez un filtre uniquement pour exporter une sélection de mouvements.
      </p>
      <div className="extract-filter-bar">
        <label>
          Filtrer par texte dans le libellé
          <input
            className="ui-input mt-2"
            type="search"
            maxLength={2000}
            value={filter}
            placeholder="Ex. : virement reçu, frais de tenue, nom…"
            disabled={disabled || busy}
            onChange={(event) => onFilter(event.target.value)}
          />
        </label>
        <label>
          Exclure les lignes contenant
          <input
            className="ui-input mt-2"
            type="text"
            maxLength={2000}
            value={excludeText}
            placeholder="Ex. : frais, annulation, nom à exclure…"
            disabled={disabled || busy}
            onChange={(event) => onExclusion(event.target.value)}
          />
        </label>
        <Button
          variant="outline"
          size="sm"
          disabled={disabled || busy || (!filter && !excludeText)}
          onClick={() => {
            onFilter('');
            onExclusion('');
          }}
        >
          Réinitialiser
        </Button>
      </div>
      <div className="row-editor-actions">
        <Button
          size="sm"
          permission="documents.write"
          disabled={
            disabled ||
            busy ||
            dirty ||
            (!filter.trim() && !excludeText.split(/[,;\n\r]/).some((text) => text.trim())) ||
            !rows.length
          }
          onClick={() => void save()}
        >
          <Save size={15} />
          {busy ? 'Traitement…' : 'Enregistrer l’extrait'}
        </Button>
        {selected && (
          <Button
            variant="outline"
            size="sm"
            permission="documents.write"
            disabled={disabled || busy || dirty}
            onClick={() => void resetSaved()}
          >
            Réinitialiser cette sauvegarde
          </Button>
        )}
      </div>
      {extracts.length > 1 && (
        <label className="movement-saved-choice">
          Extrait enregistré
          <select
            className="ui-input mt-2"
            value={selected?.id ?? ''}
            disabled={disabled || busy}
            onChange={(event) => {
              const extract = extracts.find((item) => item.id === event.target.value);
              if (extract) {
                setSaved(null);
                onSelected(extract);
              }
            }}
          >
            {extracts.map((extract) => (
              <option key={extract.id} value={extract.id}>
                {extract.filterText || 'Tous les mouvements'}
                {extract.excludeText ? ` · sauf : ${extract.excludeText}` : ''} ·{' '}
                {extract.transactions.length} mouvement(s)
              </option>
            ))}
          </select>
        </label>
      )}
      <p className="muted text-sm">
        {rows.length} mouvement(s) affiché(s). Recherche libre, sans distinction de casse ou
        d’accents. Les textes à exclure sont séparés par des virgules ou des points-virgules. La
        réinitialisation d’une sauvegarde retire uniquement l’extrait sélectionné.
      </p>
      {dirty && (
        <p className="muted text-sm">
          Enregistrez les corrections du relevé avant de sauvegarder un extrait.
        </p>
      )}
      <dl className="extract-totals">
        <div>
          <dt>Total débit</dt>
          <dd>
            {money(totals.totalDebit)} {detail.statement.currency}
          </dd>
        </div>
        <div>
          <dt>Total crédit</dt>
          <dd>
            {money(totals.totalCredit)} {detail.statement.currency}
          </dd>
        </div>
        <div>
          <dt>Solde (crédit − débit)</dt>
          <dd>
            {money(totals.balance)} {detail.statement.currency}
          </dd>
        </div>
      </dl>
      {error && (
        <p role="alert" className="text-red-700">
          {error}
        </p>
      )}
    </section>
  );
}
