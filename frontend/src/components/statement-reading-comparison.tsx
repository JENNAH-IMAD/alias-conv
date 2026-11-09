'use client';
import { useEffect, useRef, useState } from 'react';
import type { StatementDetail } from '@/services/statement-exports';
import { request } from '@/services/api';
import { Button } from './ui/button';
import { ScanLine, ChevronDown } from 'lucide-react';

const fields: Record<string, string> = {
  Date: 'Date opération',
  ValueDate: 'Date valeur',
  Description: 'Libellé',
  Reference: 'Référence',
  Debit: 'Débit',
  Credit: 'Crédit',
  OpeningBalance: 'Solde initial',
  ClosingBalance: 'Solde final',
  RowCount: 'Nombre de mouvements',
};
function engine(method: string) {
  if (method.includes('VL') || method === 'PADDLE_OCR_V6') return 'PaddleOCR AI Studio';
  if (method.startsWith('PADDLE')) return 'PaddleOCR local';
  if (method === 'NATIVE_PDF') return 'Texte du PDF';
  return 'Lecture automatique';
}
export function StatementReadingComparison({
  detail,
  disabled,
  onSaved,
  onBusy,
}: {
  detail: StatementDetail;
  disabled: boolean;
  onSaved: () => void;
  onBusy: (value: boolean) => void;
}) {
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [rereading, setRereading] = useState(false);
  const readingRequest = useRef<AbortController | null>(null);
  useEffect(() => () => readingRequest.current?.abort(), []);
  const comparisons = detail.extraction?.comparisons ?? [];
  const pending = detail.errors.some((e) => e.code === 'READING_REVIEW_REQUIRED');
  const blocked = detail.errors.some((e) => e.code !== 'READING_REVIEW_REQUIRED');
  const secondaryFailure = detail.warnings.find((warning) =>
    [
      'EXTRACTION_PADDLE_UNAVAILABLE',
      'EXTRACTION_PADDLE_TIMEOUT',
      'EXTRACTION_TIME_BUDGET',
    ].includes(warning.code),
  );
  if (
    !detail.sourceFileName &&
    !detail.extraction?.sources?.length &&
    !comparisons.length &&
    !pending
  )
    return null;
  return (
    <section className="reading-comparison-panel" aria-label="Origine et comparaison des lectures">
      <details
        className="reading-disclosure"
        open={pending || !!secondaryFailure || rereading || undefined}
      >
        <summary>
          <ScanLine size={17} />
          <span>Lecture du document</span>
          <small>
            {detail.extraction?.readingsReviewed
              ? 'Vérifiée'
              : `${detail.extraction?.sources?.length ?? 0} ${(detail.extraction?.sources?.length ?? 0) === 1 ? 'page analysée' : 'pages analysées'}`}
          </small>
          <ChevronDown size={15} />
        </summary>
        <div className="reading-details-body">
          <ul className="reading-sources">
            {detail.extraction?.sources?.map((source) => (
              <li key={source.page}>
                Page {source.page} : {engine(source.method)}
                {source.model ? ` · ${source.model}` : ''}
              </li>
            ))}
          </ul>
          {secondaryFailure && (
            <p role="status">Seconde lecture indisponible : {secondaryFailure.message}</p>
          )}
          {pending && (
            <p role="status">
              Comparez les lectures avec le PDF original, puis corrigez les valeurs dans le tableau
              des mouvements avant de confirmer.
            </p>
          )}
          {detail.sourceFileName && (
            <div className="mt-3">
              <p>
                Chaque nouvelle lecture repart du PDF original, sans réutiliser une ancienne
                extraction. Elle remplace les mouvements et les corrections de cette version.
              </p>
              <Button
                permission="documents.write"
                variant="outline"
                size="sm"
                disabled={disabled || saving || rereading}
                onClick={async () => {
                  setRereading(true);
                  setError('');
                  onBusy(true);
                  const controller = new AbortController();
                  readingRequest.current = controller;
                  try {
                    await request(`/statement-imports/${detail.id}/reread`, {
                      method: 'POST',
                      body: JSON.stringify({ version: detail.version }),
                      signal: controller.signal,
                    });
                    onSaved();
                  } catch (cause) {
                    if ((cause as Error).name !== 'AbortError') setError((cause as Error).message);
                  } finally {
                    readingRequest.current = null;
                    setRereading(false);
                    onBusy(false);
                  }
                }}
              >
                {rereading ? 'Nouvelle lecture en cours…' : 'Nouvelle lecture du PDF'}
              </Button>
              {rereading && (
                <Button variant="ghost" size="sm" onClick={() => readingRequest.current?.abort()}>
                  Annuler la relecture
                </Button>
              )}
            </div>
          )}
          {comparisons.map((comparison) => (
            <details key={comparison.page} open={pending || undefined}>
              <summary>
                Page {comparison.page} · {engine(comparison.primaryMethod)} /{' '}
                {engine(comparison.secondaryMethod)} · {comparison.differences.length} différence(s)
              </summary>
              <p>
                Lecture retenue : {engine(comparison.acceptedMethod)}. Cette comparaison conserve
                les lectures initiales ; les corrections se font dans le tableau des mouvements.
              </p>
              {comparison.secondaryModel && (
                <p>Modèle de seconde lecture : {comparison.secondaryModel}</p>
              )}
              {comparison.differences.length ? (
                <div
                  className="reading-comparison-scroll"
                  tabIndex={0}
                  aria-label={`Différences de lecture de la page ${comparison.page}`}
                >
                  <table>
                    <thead>
                      <tr>
                        <th>Ligne / champ</th>
                        <th>{engine(comparison.primaryMethod)}</th>
                        <th>{engine(comparison.secondaryMethod)}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {comparison.differences.map((difference, index) => (
                        <tr key={index} data-conflict={difference.kind === 'conflict'}>
                          <th scope="row">
                            {difference.row ? `Ligne ${difference.row} · ` : ''}
                            {fields[difference.field] ?? difference.field}
                          </th>
                          <td>{difference.primary ?? 'Non lu'}</td>
                          <td>{difference.secondary ?? 'Non lu'}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <p>Les deux lectures concordent sur les champs comparés.</p>
              )}
            </details>
          ))}
          {pending && (
            <>
              {blocked && (
                <p>Corrigez d’abord les anomalies bloquantes signalées dans les diagnostics.</p>
              )}
              <Button
                permission="documents.write"
                variant="outline"
                size="sm"
                disabled={disabled || saving || rereading || blocked}
                onClick={async () => {
                  setSaving(true);
                  setError('');
                  onBusy(true);
                  try {
                    await request(`/statement-imports/${detail.id}/review-readings`, {
                      method: 'POST',
                      body: JSON.stringify({ version: detail.version }),
                    });
                    onSaved();
                  } catch (cause) {
                    setError((cause as Error).message);
                  } finally {
                    setSaving(false);
                    onBusy(false);
                  }
                }}
              >
                {saving
                  ? 'Enregistrement…'
                  : 'Confirmer les valeurs affichées après vérification du PDF'}
              </Button>
            </>
          )}
          {error && (
            <p role="alert" className="text-destructive">
              {error}
            </p>
          )}
        </div>
      </details>
    </section>
  );
}
