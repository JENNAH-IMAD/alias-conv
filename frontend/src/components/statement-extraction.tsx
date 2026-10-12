'use client';
import { useRef, useState } from 'react';
import { AlertTriangle, ArrowDown, CheckCircle2, Settings2, WandSparkles } from 'lucide-react';
import { Button } from './ui/button';
import { request } from '@/services/api';
import { useApi } from '@/services/use-api';
import { PdfTextDocument, StatementDetail } from '@/services/statement-exports';
import { ExtractionProfiles, type ExtractionMapping } from './extraction-profiles';
import { PaddleDocumentReading } from './paddle-document-reading';
import { useAuth } from './auth-context';
import type { PdfSelection } from '@/lib/pdf-selection';

export function StatementExtraction({
  detail,
  dirty,
  onAdapted,
  pdfSelection,
}: {
  detail: StatementDetail;
  dirty: boolean;
  onAdapted: () => void;
  pdfSelection?: PdfSelection | null;
}) {
  const { can } = useAuth();
  const raw = useApi<PdfTextDocument>(`/statement-imports/${detail.id}/extraction`);
  const [pageIndex, setPageIndex] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [anchorChoices, setAnchorChoices] = useState<Record<string, string>>({});
  const mappingRef = useRef<HTMLDetailsElement>(null);
  function openMapping() {
    if (mappingRef.current) {
      mappingRef.current.open = true;
      mappingRef.current.scrollIntoView({ behavior: 'smooth', block: 'start' });
      mappingRef.current.querySelector<HTMLElement>('summary')?.focus({ preventScroll: true });
    }
  }
  const defaultMapping = {
    date: '8',
    valueDate: '20',
    description: '32',
    debit: '75',
    credit: '88',
    headerTop: '30',
  };
  const savedMapping = raw.data?.lastMapping;
  const defaults = savedMapping
    ? (Object.fromEntries(
        Object.entries(savedMapping)
          .filter(([key]) => key in defaultMapping)
          .map(([key, value]) => [key, value === null ? '' : String(value)]),
      ) as typeof defaultMapping)
    : defaultMapping;
  const [overrides, setMapping] = useState<typeof defaultMapping | null>(null);
  const mapping = overrides ?? defaults;
  const page = raw.data?.pages[pageIndex];
  async function automaticAdapt() {
    setBusy(true);
    setError('');
    try {
      await request(`/statement-imports/${detail.id}/adapt`, {
        method: 'POST',
        body: JSON.stringify({ statementVersion: detail.version }),
      });
      onAdapted();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  const structured =
    detail.extraction?.method === 'PADDLE_OCR_VL_1_6' ||
    !!raw.data?.pages.some((page) => page.tables?.length);
  const tableDefaults = {
    date: '1',
    valueDate: '2',
    description: '3',
    debit: '4',
    credit: '5',
    startRow: '1',
  };
  const [tableOverrides, setTableMapping] = useState<typeof tableDefaults | null>(null);
  const savedTable = savedMapping?.tableColumns;
  const tableMapping =
    tableOverrides ??
    (savedTable
      ? (Object.fromEntries(
          Object.keys(tableDefaults).map((key) => {
            const value = savedTable[key as keyof typeof tableDefaults];
            return [key, value === null ? '' : String(value + (key === 'startRow' ? 0 : 1))];
          }),
        ) as typeof tableDefaults)
      : tableDefaults);
  async function adapt(event: React.FormEvent) {
    event.preventDefault();
    if (busy || dirty || !raw.data || !can('documents.write')) return;
    if (!structured) {
      const columns = [mapping.date, mapping.description, mapping.debit, mapping.credit].map(
        Number,
      );
      const positions = [
        ...columns,
        Number(mapping.headerTop),
        ...(mapping.valueDate ? [Number(mapping.valueDate)] : []),
      ];
      if (
        positions.some((value) => !Number.isFinite(value) || value < 0 || value >= 100) ||
        columns.some((value, i) => i > 0 && value <= columns[i - 1])
      ) {
        setError(
          'Les colonnes doivent suivre l’ordre du PDF : date, libellé, débit, crédit, de gauche à droite. Choisissez un repère différent pour chacune.',
        );
        return;
      }
    }
    setBusy(true);
    setError('');
    try {
      await request(`/statement-imports/${detail.id}/adapt`, {
        method: 'POST',
        body: JSON.stringify({
          statementVersion: detail.version,
          mapping: {
            ...Object.fromEntries(
              Object.entries(mapping).map(([key, value]) => [
                key,
                key === 'valueDate' && value === '' ? null : Number(value),
              ]),
            ),
            ...(structured
              ? {
                  tableColumns: Object.fromEntries(
                    Object.entries(tableMapping).map(([key, value]) => [
                      key,
                      key === 'valueDate' && value === ''
                        ? null
                        : Number(value) - (key === 'startRow' ? 0 : 1),
                    ]),
                  ),
                }
              : {}),
          },
        }),
      });
      onAdapted();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="extraction-workbench p-5 space-y-5">
      {raw.data && (
        <ExtractionProfiles
          detail={detail}
          disabled={busy || dirty}
          mapping={
            {
              ...Object.fromEntries(
                Object.entries(mapping).map(([key, value]) => [
                  key,
                  value === '' ? null : Number(value),
                ]),
              ),
              ...(structured
                ? {
                    tableColumns: Object.fromEntries(
                      Object.entries(tableMapping).map(([key, value]) => [
                        key,
                        value === '' ? null : Number(value) - (key === 'startRow' ? 0 : 1),
                      ]),
                    ),
                  }
                : {}),
            } as ExtractionMapping
          }
          onSelect={(chosen) => {
            setMapping(
              Object.fromEntries(
                Object.keys(defaultMapping).map((key) => [
                  key,
                  chosen[key as keyof ExtractionMapping] == null
                    ? ''
                    : String(chosen[key as keyof ExtractionMapping]),
                ]),
              ) as typeof defaultMapping,
            );
            if (chosen.tableColumns)
              setTableMapping(
                Object.fromEntries(
                  Object.keys(tableDefaults).map((key) => {
                    const value = chosen.tableColumns![key as keyof typeof tableDefaults];
                    return [key, value == null ? '' : String(value + (key === 'startRow' ? 0 : 1))];
                  }),
                ) as typeof tableDefaults,
              );
          }}
          onApplied={onAdapted}
        />
      )}
      <div>
        <h3 className="font-semibold">Lecture du PDF et réglage des colonnes</h3>
        <p className="muted text-sm mt-2">
          {structured
            ? 'Lecture structurée par PaddleOCR-VL-1.6'
            : detail.extraction?.method === 'PADDLE_OCR_V6'
              ? 'Lecture par PaddleOCR PP-OCRv6'
              : detail.extraction?.method === 'PADDLE_OCR'
                ? 'Lecture par PaddleOCR'
                : 'Lecture du texte natif'}{' '}
          · {detail.extraction?.pageCount} page(s). Les blocs de texte sont conservés avant leur
          adaptation en mouvements bancaires.
        </p>
        <div className="extraction-header-actions">
          <Button
            permission="documents.write"
            className="mt-3"
            variant="outline"
            type="button"
            disabled={busy || dirty || !raw.data}
            onClick={() => void automaticAdapt()}
          >
            <WandSparkles size={16} />
            {busy ? 'Adaptation…' : 'Relancer l’adaptation automatique'}
          </Button>
          <Button
            type="button"
            variant="outline"
            className="mt-3"
            onClick={openMapping}
            disabled={busy || raw.loading || !raw.data}
          >
            <Settings2 size={16} /> Régler les colonnes
          </Button>
        </div>
        {error && (
          <div role="alert" className="extraction-recovery mt-3">
            <AlertTriangle size={21} />
            <div>
              <strong>L’adaptation n’a pas pu aboutir</strong>
              <p>{error}</p>
              <p>
                Le texte du PDF reste disponible
                {detail.statement.transactions.length
                  ? ' et les mouvements existants sont conservés'
                  : ''}
                . Repérez les colonnes dans le tableau ci-dessous, puis appliquez vos réglages.
              </p>
              <Button type="button" size="sm" variant="outline" onClick={openMapping}>
                Corriger le réglage des colonnes <ArrowDown size={15} />
              </Button>
            </div>
          </div>
        )}
      </div>
      {raw.error && (
        <p role="alert" className="conversion-alert error">
          {raw.error}
          <Button type="button" size="sm" variant="outline" onClick={raw.reload}>
            Réessayer
          </Button>
        </p>
      )}
      {raw.loading && <p role="status">Chargement du texte extrait…</p>}
      {page && (
        <label className="text-sm">
          Page du document
          <select
            className="ui-input mt-2"
            value={pageIndex}
            onChange={(e) => setPageIndex(Number(e.target.value))}
          >
            {raw.data?.pages.map((p, i) => (
              <option key={p.number} value={i}>
                Page {p.number} · {p.boxes.length} blocs lus
              </option>
            ))}
          </select>
        </label>
      )}
      <details
        ref={mappingRef}
        className="conversion-details extraction-mapping"
        open={detail.extraction?.needsMapping || !!error}
      >
        <summary>Adapter les colonnes du tableau</summary>
        {structured ? (
          <p className="muted text-sm mt-3">
            L’adaptation utilise les cellules et les en-têtes reconnus par PaddleOCR. Pour un autre
            tableau, indiquez les numéros de colonnes affichés ci-dessus. Laissez la date de valeur
            vide si elle est absente. Ces réglages s’appliquent à tous les tableaux ; vérifiez
            chaque page.
          </p>
        ) : (
          <p className="muted text-sm mt-3">
            Choisissez un texte repère pour chaque colonne dans les listes ci-dessous. Les positions
            sont remplies automatiquement et restent ajustables manuellement. Ces réglages
            s’appliquent à toutes les pages ; vérifiez que leur disposition est identique. Indiquez
            si la date de valeur est absente.
          </p>
        )}
        <form onSubmit={adapt} className="mt-4 space-y-4">
          {!structured && page && (
            <fieldset
              disabled={busy || dirty || !can('documents.write')}
              className="extraction-guided-grid"
            >
              <legend>Choisissez les repères lus dans le PDF</legend>
              <p>
                Pour chaque colonne, choisissez son en-tête ou un texte placé au début de cette
                colonne. Pour le début du tableau, choisissez la ligne d’en-tête. Les positions sont
                remplies automatiquement.
              </p>
              {(
                [
                  ['date', 'Date opération'],
                  ['valueDate', 'Date valeur (facultative)'],
                  ['description', 'Libellé'],
                  ['debit', 'Débit'],
                  ['credit', 'Crédit'],
                  ['headerTop', 'Début du tableau'],
                ] as const
              ).map(([key, label]) => (
                <label key={key}>
                  {label}
                  <select
                    className="ui-input mt-2"
                    aria-label={`Repère pour ${label}`}
                    value={anchorChoices[`${page.number}-${key}`] ?? ''}
                    onChange={(event) => {
                      if (event.target.value === '') return;
                      setAnchorChoices((previous) => ({
                        ...previous,
                        [`${page.number}-${key}`]: event.target.value,
                      }));
                      if (event.target.value === 'absent') {
                        setMapping((old) => ({ ...(old ?? defaults), valueDate: '' }));
                        return;
                      }
                      const box = page.boxes[Number(event.target.value)];
                      if (!box) return;
                      const percent =
                        key === 'headerTop'
                          ? (box.bottom / page.height) * 100
                          : (box.left / page.width) * 100;
                      setMapping((old) => ({
                        ...(old ?? defaults),
                        [key]: String(Math.max(0, Math.min(99.9, Math.round(percent * 10) / 10))),
                      }));
                    }}
                  >
                    <option value="">Choisir un texte de la page…</option>
                    {key === 'valueDate' && (
                      <option value="absent">Cette colonne est absente</option>
                    )}
                    {page.boxes
                      .map((box, index) => ({ box, index }))
                      .sort((a, b) => a.box.top - b.box.top || a.box.left - b.box.left)
                      .map(({ box, index }) => (
                        <option key={index} value={index}>
                          {box.text.slice(0, 80)} · {((box.left / page.width) * 100).toFixed(1)} %
                        </option>
                      ))}
                  </select>
                  <small>
                    {mapping[key] === ''
                      ? 'Colonne absente'
                      : `Position retenue : ${mapping[key]} %`}
                  </small>
                  {pdfSelection && (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      className="mt-2"
                      onClick={() =>
                        setMapping((old) => ({
                          ...(old ?? defaults),
                          [key]: String(
                            Math.round(
                              (key === 'headerTop' ? pdfSelection.bottom : pdfSelection.left) * 10,
                            ) / 10,
                          ),
                        }))
                      }
                    >
                      Utiliser la sélection du PDF
                    </Button>
                  )}
                </label>
              ))}
            </fieldset>
          )}
          <details className="extraction-numeric-settings" open={structured}>
            <summary>
              {structured
                ? 'Correspondance des colonnes reconnues'
                : 'Ajuster les positions manuellement'}
            </summary>
            <fieldset
              disabled={busy || dirty || raw.loading || !can('documents.write')}
              className="grid gap-4 sm:grid-cols-2"
            >
              {structured
                ? (
                    [
                      ['date', 'Colonne date opération'],
                      ['valueDate', 'Colonne date valeur'],
                      ['description', 'Colonne libellé'],
                      ['debit', 'Colonne débit'],
                      ['credit', 'Colonne crédit'],
                      ['startRow', 'Lignes à ignorer au début du tableau'],
                    ] as const
                  ).map(([key, label]) => (
                    <label key={key} className="text-sm">
                      {label}
                      {key === 'startRow' ? '' : ' (numéro)'}
                      <input
                        className="ui-input mt-2"
                        type="number"
                        min={key === 'startRow' ? 0 : 1}
                        max={key === 'startRow' ? 200 : 64}
                        step="1"
                        required={key !== 'valueDate'}
                        value={tableMapping[key]}
                        onChange={(e) =>
                          setTableMapping((old) => ({
                            ...(old ?? tableMapping),
                            [key]: e.target.value,
                          }))
                        }
                      />
                    </label>
                  ))
                : (
                    [
                      ['date', 'Colonne date opération'],
                      ['valueDate', 'Colonne date valeur'],
                      ['description', 'Colonne libellé'],
                      ['debit', 'Colonne débit'],
                      ['credit', 'Colonne crédit'],
                      ['headerTop', 'Début du tableau'],
                    ] as const
                  ).map(([key, label]) => (
                    <label key={key} className="text-sm">
                      {label} (%)
                      <input
                        type="number"
                        min="0"
                        max="99.9"
                        step="0.1"
                        required={key !== 'valueDate'}
                        className="ui-input mt-2"
                        value={mapping[key]}
                        onChange={(e) =>
                          setMapping((old) => ({ ...(old ?? defaults), [key]: e.target.value }))
                        }
                      />
                    </label>
                  ))}
            </fieldset>
          </details>
          {dirty && (
            <p className="conversion-alert warning">
              Enregistrez les corrections du relevé avant de changer l’adaptation.
            </p>
          )}
          <Button permission="documents.write" disabled={busy || dirty || !raw.data}>
            <CheckCircle2 size={16} />
            {busy ? 'Adaptation…' : 'Appliquer aux mouvements'}
          </Button>
          <p className="muted text-xs">
            Ces réglages remplacent les mouvements après application. Vérifiez ensuite les dates,
            les montants et les soldes avec le PDF, puis validez le relevé. Après validation, ce
            réglage pourra être réutilisé automatiquement pour de nouveaux relevés de même banque et
            de même disposition.
          </p>
        </form>
      </details>
      {page && (
        <>
          {structured && raw.data ? (
            <PaddleDocumentReading document={raw.data} pageIndex={pageIndex} />
          ) : (
            <>
              <p className="muted text-xs">
                Gauche et haut indiquent la position en pourcentage de la page. La confiance porte
                sur la lecture du texte ; vérifiez les montants et les dates avec le PDF original.
              </p>
              <div
                className="conversion-table-scroll max-h-96"
                tabIndex={0}
                aria-label="Texte extrait du PDF"
              >
                <table className="conversion-table">
                  <thead>
                    <tr>
                      <th>Texte lu</th>
                      <th>Gauche %</th>
                      <th>Haut %</th>
                      <th>Confiance</th>
                    </tr>
                  </thead>
                  <tbody>
                    {[...page.boxes]
                      .sort((a, b) => a.top - b.top || a.left - b.left)
                      .map((box, index) => (
                        <tr key={index}>
                          <td className="whitespace-pre-wrap min-w-64">{box.text}</td>
                          <td>{((box.left / page.width) * 100).toFixed(1)}</td>
                          <td>{((box.top / page.height) * 100).toFixed(1)}</td>
                          <td>
                            <span
                              className={
                                box.confidence !== null && box.confidence < 0.9
                                  ? 'conversion-status review'
                                  : ''
                              }
                            >
                              {box.confidence === null
                                ? '—'
                                : `${Math.round(box.confidence * 100)} %`}
                            </span>
                          </td>
                        </tr>
                      ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </>
      )}
    </div>
  );
}
