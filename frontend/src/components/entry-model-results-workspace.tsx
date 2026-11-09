'use client';
import { useEffect, useState } from 'react';
import Link from 'next/link';
import {
  BookOpen,
  Search,
  Download,
  Archive,
  ChartNoAxesCombined,
  CheckCircle2,
  PanelRightClose,
  PanelRightOpen,
  ArrowLeft,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { LoadState } from '@/components/ui/load-state';
import { ConversionSteps } from '@/components/conversion-steps';
import { EntryResultTable } from '@/components/entry-result-table';
import { StatementPdfPanel } from '@/components/statement-pdf-panel';
import { EntrySourceSummary } from '@/components/entry-source-summary';
import { useAll, useApi } from '@/services/use-api';
import { API_URL, apiError, request } from '@/services/api';
import { date, money, type StatementDetail } from '@/services/statement-exports';
import type {
  EntryModelDossier,
  EntryModelPreview,
  EntryModelRun,
  EntryModelRow,
  EntryOutputTemplate,
} from '@/services/entry-models';
import { saveReadyExportBlob } from '@/lib/save-export';
import { downloadExcel } from '@/lib/download-excel';
import { hasUnsavedChanges } from '@/lib/use-unsaved-changes';
import { useAuth } from '@/components/auth-context';
import '@/styles/entry-models.css';

type Source = {
  id: string;
  bankName: string;
  sourceFileName: string | null;
  statementNumber: string | null;
  clientId: string | null;
};
const normalizeLabel = (value: string) =>
  value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toUpperCase()
    .replace(/[^\p{L}\p{N}]+/gu, ' ')
    .trim();
export function EntryModelResultsWorkspace({
  clientId,
  initialSource,
  initialExtract,
  initialAbbreviation,
  guided = false,
  onNavigate,
  initialPreview,
  initialModelId = '',
  initialOutputTemplateId = '',
  initialRun,
}: {
  clientId: string;
  initialSource: string;
  initialExtract: string;
  initialFormat: string;
  initialAbbreviation: string;
  guided?: boolean;
  onNavigate?: (step: 1 | 2) => void;
  initialPreview?: EntryModelPreview;
  initialModelId?: string;
  initialOutputTemplateId?: string;
  initialRun?: EntryModelRun;
}) {
  const { can } = useAuth();
  const sources = useAll<Source>(`/client-workspace/${clientId}/statements`);
  const models = useApi<EntryModelDossier>(`/accounting/entry-models/${clientId}`);
  const formats = useApi<EntryOutputTemplate[]>('/accounting/entry-output-templates');
  const [sourceId, setSourceId] = useState(initialSource);
  const [extractId, setExtractId] = useState(initialExtract);
  const formatId = '00000000-0000-0000-0000-000000000000';
  const [selectedModelId, setSelectedModelId] = useState(initialModelId);
  const [abbreviation, setAbbreviation] = useState(initialAbbreviation);
  const detail = useApi<StatementDetail>(sourceId ? `/statement-imports/${sourceId}` : null);
  const [decisions, setDecisions] = useState<Record<string, string | null>>(
    initialRun?.decisions || {},
  );
  const [preview, setPreview] = useState<EntryModelPreview | null>(initialPreview || null);
  const [previewKey, setPreviewKey] = useState(
    initialPreview
      ? JSON.stringify({
          clientId,
          statementId: initialSource,
          exportTemplateId: '00000000-0000-0000-0000-000000000000',
          abbreviation: initialAbbreviation,
          ...(initialModelId ? { modelId: initialModelId } : {}),
          ...(initialOutputTemplateId ? { outputTemplateId: initialOutputTemplateId } : {}),
          ...(initialRun?.keywordOverrides
            ? { keywordOverrides: initialRun.keywordOverrides }
            : {}),
          ...(initialExtract ? { extractId: initialExtract } : {}),
          decisions: initialRun?.decisions || {},
        })
      : '',
  );
  const [reviewed, setReviewed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [onlyRetained, setOnlyRetained] = useState(false);
  const [phase, setPhase] = useState<4 | 5 | 6>(4);
  const [pdfCollapsed, setPdfCollapsed] = useState(false);
  const [analysisOpen, setAnalysisOpen] = useState(false);
  const [keywordDraft, setKeywordDraft] = useState<string | null>(
    initialRun?.keywordOverrides?.join('\n') ?? null,
  );
  const eligible = (models.data?.models || []).filter(
    (m) => m.active && m.bankAccountId === detail.data?.bankAccountId,
  );
  const abbreviations = [...new Set(eligible.map((m) => m.abbreviation))].sort();
  const matchingModels = eligible.filter((m) => m.abbreviation === abbreviation);
  const processingModel =
    matchingModels.find((m) => m.id === selectedModelId) ||
    (matchingModels.length === 1 ? matchingModels[0] : undefined);
  const runModelId = processingModel?.id || '';
  const displayFormat = formats.data?.find(
    (t) => t.id === (initialOutputTemplateId || processingModel?.outputTemplateId),
  );
  const activeWords =
    keywordDraft === null
      ? processingModel?.keywords || []
      : [
          ...new Set(
            keywordDraft
              .split(/\r?\n/)
              .map((w) => w.trim())
              .filter(Boolean),
          ),
        ];
  const sourceLabels =
    preview?.rows.map((r) => r.description) ||
    detail.data?.statement.transactions.map((t) => t.description || '') ||
    [];
  function changeWords(value: string | null) {
    setKeywordDraft(value);
    setDecisions({});
    setReviewed(false);
    setNotice('');
    if (guided) setPhase(5);
  }
  const input: EntryModelRun = {
    clientId,
    statementId: sourceId,
    exportTemplateId: formatId,
    abbreviation,
    ...(runModelId ? { modelId: runModelId } : {}),
    ...(initialOutputTemplateId ? { outputTemplateId: initialOutputTemplateId } : {}),
    ...(keywordDraft !== null ? { keywordOverrides: activeWords } : {}),
    ...(extractId ? { extractId } : {}),
    decisions,
  };
  const inputKey = JSON.stringify(input);
  const stale = inputKey !== previewKey;
  const loading = sources.loading || models.loading || detail.loading || formats.loading;
  const failure = error || sources.error || models.error || detail.error || formats.error;
  const canPreview =
    !!processingModel &&
    !!detail.data?.bankAccountId &&
    detail.data.clientId === clientId &&
    sources.data?.some((s) => s.id === sourceId) &&
    abbreviations.includes(abbreviation) &&
    (!processingModel || activeWords.length > 0);
  useEffect(() => {
    if (!guided || !canPreview || loading || busy || preview) return;
    const controller = new AbortController();
    request<EntryModelPreview>('/accounting/entry-models/preview', {
      method: 'POST',
      body: inputKey,
      signal: controller.signal,
    })
      .then((result) => {
        if (!controller.signal.aborted) {
          setPreview(result);
          setPreviewKey(inputKey);
        }
      })
      .catch((reason: Error) => {
        if (!controller.signal.aborted) setError(reason.message);
      });
    return () => controller.abort();
  }, [guided, canPreview, loading, busy, preview, inputKey]);
  function reset() {
    setKeywordDraft(null);
    setDecisions({});
    setReviewed(false);
    setNotice('');
  }
  async function inspect() {
    if (!canPreview || busy) return;
    if (guided && hasUnsavedChanges()) {
      setError('Enregistrez les modifications du document source avant de recalculer le résultat.');
      return;
    }
    setBusy(true);
    setError('');
    setReviewed(false);
    setNotice('');
    try {
      setPreview(
        await request<EntryModelPreview>('/accounting/entry-models/preview', {
          method: 'POST',
          body: JSON.stringify(input),
        }),
      );
      setPreviewKey(inputKey);
      if (guided && phase === 6) setPhase(5);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function download(confirmed = false, archiveOnly = false) {
    if (busy) return;
    if (!preview || !processingModel) {
      setError('Choisissez une écriture et affichez son résultat avant le téléchargement.');
      return;
    }
    if (stale) {
      setError(
        'Les paramètres ont changé. Cliquez sur « Appliquer le filtre » avant le téléchargement.',
      );
      return;
    }
    if (!reviewed && !confirmed) return;
    if (guided && hasUnsavedChanges()) {
      setError(
        'Enregistrez les modifications du document source et recalculez le résultat avant le téléchargement.',
      );
      return;
    }
    const needsValidation = preview.errors.some((issue) => issue.code === 'SOURCE_NOT_VALIDATED');
    const blocking = preview.errors.filter((issue) => issue.code !== 'SOURCE_NOT_VALIDATED');
    if (blocking.length || (!preview.canExport && !needsValidation)) {
      setError(
        blocking.map((issue) => issue.message).join(' ') ||
          'Recalculez le résultat avant le téléchargement.',
      );
      return;
    }
    if (needsValidation && !can('documents.write')) {
      setError(
        'Le document doit être validé par un utilisateur autorisé à modifier les documents avant le téléchargement.',
      );
      return;
    }
    setBusy(true);
    setError('');
    try {
      const extension =
        preview.output?.fileExtension || processingModel.output?.fileExtension || 'txt';
      const name = `ecritures_${abbreviation}_${sourceId.replaceAll('-', '').slice(0, 8)}.${extension}`;
      let exportPreview = preview;
      if (needsValidation) {
        const validated = await request<StatementDetail>(
          `/statement-imports/${sourceId}/validate`,
          {
            method: 'POST',
            body: JSON.stringify({ version: preview.sourceVersion }),
          },
        );
        detail.reload();
        if (validated.status !== 'VALIDATED' || validated.errors.length) {
          throw new Error(
            validated.errors.map((issue) => issue.message).join(' ') ||
              'Le document source doit encore être vérifié.',
          );
        }
        exportPreview = await request<EntryModelPreview>('/accounting/entry-models/preview', {
          method: 'POST',
          body: JSON.stringify(input),
        });
        setPreview(exportPreview);
        setPreviewKey(inputKey);
        if (
          exportPreview.settingsVersion !== preview.settingsVersion ||
          JSON.stringify(exportPreview.output) !== JSON.stringify(preview.output) ||
          JSON.stringify(exportPreview.resultRows) !== JSON.stringify(preview.resultRows)
        ) {
          throw new Error(
            'Le modèle ou le résultat a changé pendant la validation. Vérifiez le nouvel aperçu, puis téléchargez le fichier.',
          );
        }
        if (!exportPreview.canExport) {
          throw new Error(
            exportPreview.errors.map((issue) => issue.message).join(' ') ||
              'Le résultat doit encore être vérifié.',
          );
        }
      }
      const response = await fetch(`${API_URL}/accounting/entry-models/export`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json', 'X-Alias-Request': '1' },
        body: JSON.stringify({
          ...input,
          previewFingerprint: exportPreview.fingerprint,
          confirmed: true,
        }),
      });
      if (!response.ok) throw await apiError(response);
      const file = await response.blob();
      if (!archiveOnly) await saveReadyExportBlob(file, name, extension);
      setReviewed(false);
      if (guided) setPhase(6);
      setNotice(
        archiveOnly
          ? 'Écritures archivées dans l’espace client avec le modèle et les paramètres utilisés.'
          : 'Résultat enregistré dans l’espace client et téléchargé.',
      );
      try {
        setPreview(
          await request<EntryModelPreview>('/accounting/entry-models/preview', {
            method: 'POST',
            body: JSON.stringify(input),
          }),
        );
        setPreviewKey(inputKey);
      } catch {
        setPreview(null);
      }
    } catch (e) {
      if ((e as Error).name !== 'AbortError') setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  function downloadExcelResult() {
    if (!preview?.output || !preview.resultRows) return;
    const columns = preview.output.columns.filter((column) => column.export);
    const values: Record<string, keyof (typeof preview.resultRows)[number]> = {
      ACCOUNT: 'accountCode',
      TITLE: 'title',
      DESCRIPTION: 'description',
      BANK: 'bank',
      NET: 'net',
      DEBIT_HT: 'debitHt',
      CREDIT_HT: 'creditHt',
      GROSS: 'gross',
      TAX: 'tax',
      RATE: 'rate',
      DATE: 'date',
      REFERENCE: 'reference',
      ABBREVIATION: 'abbreviation',
      CURRENCY: 'currency',
    };
    downloadExcel(
      `ecritures_${abbreviation}_${sourceId.replaceAll('-', '').slice(0, 8)}.xlsx`,
      columns.map((column) => column.label),
      preview.resultRows.map((row) =>
        columns.map((column) => row[values[column.field]] ?? ''),
      ),
    );
    setNotice('Le fichier Excel des écritures a été téléchargé.');
    setError('');
  }
  async function confirmVerification() {
    if (busy || !preview || !processingModel || !can('documents.write')) return;
    if (stale || hasUnsavedChanges()) {
      setError('Enregistrez les changements et actualisez le résultat avant de confirmer.');
      return;
    }
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const validated = await request<StatementDetail>(
        `/statement-imports/${sourceId}/validate`,
        {
          method: 'POST',
          body: JSON.stringify({ version: preview.sourceVersion }),
        },
      );
      detail.reload();
      if (validated.status !== 'VALIDATED' || validated.errors.length) {
        throw new Error(
          validated.errors.map((issue) => issue.message).join(' ') ||
            'Le document source doit encore être vérifié.',
        );
      }
      const refreshed = await request<EntryModelPreview>('/accounting/entry-models/preview', {
        method: 'POST',
        body: JSON.stringify(input),
      });
      setPreview(refreshed);
      setPreviewKey(inputKey);
      setReviewed(true);
      setNotice('Vérification confirmée. Le résultat a été actualisé.');
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  const retained = preview?.rows.filter((r) => r.status === 'MATCHED').length || 0;
  const ambiguous =
    preview?.rows.filter((r) => r.status === 'AMBIGUOUS' || r.status === 'INVALID').length || 0;
  const excluded =
    preview?.rows.filter((r) => r.status === 'UNMATCHED' || r.status === 'EXCLUDED').length || 0;
  const visible = preview?.rows.filter((r) => !onlyRetained || r.status === 'MATCHED') || [];
  const resultRows = preview?.rows.filter((r) => r.status === 'MATCHED') || [];
  const analysisRows =
    preview?.resultRows ||
    resultRows.map((row) => ({ gross: row.total, net: row.net, tax: row.tax }));
  const totals = analysisRows.reduce<{ gross: number; net: number; tax: number }>(
    (sum, row) => ({
      gross: sum.gross + Math.round(Number(row.gross || 0) * 100),
      net: sum.net + Math.round(Number(row.net || 0) * 100),
      tax: sum.tax + Math.round(Number(row.tax || 0) * 100),
    }),
    { gross: 0, net: 0, tax: 0 },
  );
  const resultBank =
    sources.data?.find((s) => s.id === preview?.statementId)?.bankName ||
    (detail.data?.id === preview?.statementId ? detail.data?.statement.bankName : '') ||
    '—';
  function decision(row: EntryModelRow, value: string) {
    setDecisions({ ...decisions, [row.transactionId]: value === '__exclude__' ? null : value });
    setReviewed(false);
    if (guided) setPhase(5);
  }
  return (
    <div className="entry-model-results-workspace">
      {guided && detail.data && (
        <EntrySourceSummary
          detail={detail.data}
          clientId={clientId}
          modelName={
            processingModel
              ? `${processingModel.abbreviation} · ${processingModel.name}`
              : undefined
          }
          accountCode={processingModel?.accountCode}
        />
      )}
      {guided && !preview && !failure && (
        <p role="status" className="muted text-sm">
          Chargement du résultat…
        </p>
      )}
      {guided && (
        <section className="ui-card">
          <ConversionSteps
            current={phase === 6 ? 4 : 3}
            accountingMode
            extractMode={detail.data?.statement.conversionType === 'EXTRACT'}
            disabled={busy}
            onNavigate={(step) => {
              if (step <= 2) onNavigate?.(step as 1 | 2);
              else setPhase(step === 4 ? 6 : 4);
            }}
          />
          <div className="entry-model-toolbar mt-4">
            <h2 className="font-semibold">
              {phase === 4
                ? 'Vérifiez le résultat de l’écriture'
                : phase === 5
                  ? 'Paramétrez les mouvements retenus'
                  : 'Téléchargez vos écritures comptables'}
            </h2>
            <span className="ui-tag">
              {abbreviation} · {displayFormat?.name || processingModel?.name}
            </span>
          </div>
          <p className="muted text-sm mt-2">
            {phase === 4
              ? 'Contrôlez les libellés et les montants HT calculés à partir du TTC.'
              : phase === 5
                ? 'Confirmez les correspondances ou excluez les mouvements à ne pas exporter.'
                : 'Le fichier a été téléchargé et archivé dans l’espace client.'}
          </p>
        </section>
      )}
      {!sources.loading && !sources.error && sources.data?.length === 0 && (
        <section className="ui-card entry-model-empty">
          <BookOpen size={28} />
          <h2>Aucun document classé pour ce client</h2>
          <p>
            Importez un relevé ou un extrait et associez-le au client et à son compte bancaire pour
            appliquer ses modèles d’écritures.
          </p>
          <Button permission="documents.write" asChild>
            <Link href={`/conversions?import=1&type=accounting&clientId=${clientId}`}>
              Importer un document bancaire
            </Link>
          </Button>
        </section>
      )}
      <div
        className={
          guided
            ? `entry-conversion-review-layout${pdfCollapsed ? ' is-pdf-collapsed' : ''}`
            : undefined
        }
      >
        <div className="entry-conversion-results">
          <details
            className={`ui-card${guided ? ' entry-model-guided-controls' : ''}`}
            open={!guided || undefined}
          >
            <summary className="font-semibold">Filtrer les mouvements</summary>
            {guided && (
              <div className="entry-filter-selection">
                <label className="conversion-template-label">
                  Abréviation
                  <select
                    aria-label="Abréviation enregistrée"
                    className="ui-input mt-2"
                    value={abbreviation}
                    disabled={busy || loading}
                    onChange={(e) => {
                      setAbbreviation(e.target.value);
                      setSelectedModelId('');
                      reset();
                    }}
                  >
                    <option value="">Choisir une abréviation…</option>
                    {abbreviations.map((item) => (
                      <option key={item} value={item}>
                        {item}
                      </option>
                    ))}
                  </select>
                </label>
                {matchingModels.length > 1 && (
                  <label className="conversion-template-label">
                    Modèle
                    <select
                      aria-label="Modèle de l’abréviation"
                      className="ui-input mt-2"
                      value={selectedModelId}
                      disabled={busy || loading}
                      onChange={(e) => {
                        setSelectedModelId(e.target.value);
                        reset();
                      }}
                    >
                      <option value="">Choisir un modèle…</option>
                      {matchingModels.map((item) => (
                        <option key={item.id} value={item.id}>
                          {item.name} · {item.accountCode} · TVA {item.taxRate} %
                        </option>
                      ))}
                    </select>
                  </label>
                )}
                <p className="muted text-sm">
                  Les mots et la TVA du modèle sont appliqués au document.
                </p>
              </div>
            )}
            {processingModel && (
              <div className="entry-model-keyword-controls">
                <div className="entry-model-keywords" aria-label="Mots du modèle">
                  {activeWords.map((word) => (
                    <span key={word}>
                      {word} ·{' '}
                      {
                        sourceLabels.filter((label) =>
                          ` ${normalizeLabel(label)} `.includes(` ${normalizeLabel(word)} `),
                        ).length
                      }
                    </span>
                  ))}
                </div>
                <details className="entry-filter-advanced">
                  <summary>Ajuster les mots pour cet export</summary>
                  <label className="conversion-template-label">
                    Mots du libellé
                    <textarea
                      aria-label={`Mots du libellé pour ${abbreviation}`}
                      className="ui-input mt-2"
                      rows={2}
                      disabled={busy}
                      value={keywordDraft ?? processingModel.keywords.join('\n')}
                      onChange={(e) => changeWords(e.target.value)}
                    />
                  </label>
                  <p className="muted text-sm">
                    Une expression par ligne. Le modèle enregistré reste inchangé.
                  </p>
                  {keywordDraft !== null && (
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={busy}
                      onClick={() => changeWords(null)}
                    >
                      Rétablir les mots
                    </Button>
                  )}
                </details>
              </div>
            )}
            {!guided && (
              <>
                <div className="entry-model-fields">
                  <label>
                    Relevé ou extrait PDF
                    <select
                      aria-label="Document source"
                      className="ui-input mt-2"
                      disabled={busy || sources.loading}
                      value={sourceId}
                      onChange={(e) => {
                        setSourceId(e.target.value);
                        setExtractId('');
                        setAbbreviation('');
                        reset();
                      }}
                    >
                      <option value="">Choisir le document…</option>
                      {sources.data?.map((s) => (
                        <option key={s.id} value={s.id}>
                          {s.sourceFileName || s.statementNumber || s.bankName} · {s.bankName}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Modèle d’écritures
                    <select
                      className="ui-input mt-2"
                      aria-label="Modèle d’écritures"
                      value={processingModel?.id || ''}
                      disabled={busy || models.loading}
                      onChange={(e) => {
                        setSelectedModelId(e.target.value);
                        setAbbreviation(
                          eligible.find((m) => m.id === e.target.value)?.abbreviation || '',
                        );
                        reset();
                      }}
                    >
                      <option value="">Choisir un modèle du client…</option>
                      {eligible.map((t) => (
                        <option key={t.id} value={t.id}>
                          {t.abbreviation} · {t.name} · TVA {t.taxRate} %
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Périmètre
                    <select
                      className="ui-input mt-2"
                      value={extractId}
                      disabled={busy || !detail.data}
                      onChange={(e) => {
                        setExtractId(e.target.value);
                        reset();
                      }}
                    >
                      <option value="">Toutes les opérations du document</option>
                      {detail.data?.statement.extracts?.map((x) => (
                        <option key={x.id} value={x.id}>
                          {x.filterText || 'Extrait sauvegardé'} · {x.transactions.length}{' '}
                          mouvements
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Abréviation
                    <select
                      className="ui-input mt-2"
                      aria-label="Abréviation du traitement"
                      disabled={busy || !formatId || !detail.data}
                      value={abbreviation}
                      onChange={(e) => {
                        setAbbreviation(e.target.value);
                        setSelectedModelId('');
                        reset();
                      }}
                    >
                      <option value="">Choisir une abréviation…</option>
                      {abbreviations.map((a) => (
                        <option key={a} value={a}>
                          {a} · {eligible.filter((m) => m.abbreviation === a).length} modèle(s)
                        </option>
                      ))}
                    </select>
                  </label>
                </div>
              </>
            )}
            {sourceId && detail.data && !detail.data.bankAccountId && (
              <p className="conversion-alert warning mt-4">
                Associez le document à un compte du client avant le traitement.{' '}
                <Link className="underline" href={`/conversions?statementId=${sourceId}`}>
                  Ouvrir et classer le document
                </Link>
              </p>
            )}
            {formatId &&
              detail.data?.bankAccountId &&
              !models.loading &&
              !models.error &&
              !eligible.length && (
                <p className="conversion-alert warning mt-4">
                  Aucun modèle actif pour ce compte.{' '}
                  <Link className="underline" href={`/entry-models?clientId=${clientId}`}>
                    Configurer les modèles
                  </Link>
                </p>
              )}
            <div className="entry-model-actions mt-5">
              <p className="muted text-sm">Seuls les mouvements correspondants sont retenus.</p>
              <Button disabled={!canPreview || busy || loading} onClick={() => void inspect()}>
                <Search size={16} /> {busy ? 'Traitement…' : 'Appliquer le filtre'}
              </Button>
            </div>
          </details>
          <LoadState loading={loading} error="" />
          {failure && (
            <div className="conversion-alert error" role="alert">
              {failure}
              <Button
                variant="outline"
                disabled={busy}
                onClick={() => {
                  setError('');
                  sources.reload();
                  models.reload();
                  formats.reload();
                  detail.reload();
                }}
              >
                Réessayer
              </Button>
            </div>
          )}
          {notice && (
            <p className="ui-card" role="status">
              {notice}
            </p>
          )}
          {preview && (
            <>
              <div className="entry-model-summary">
                <div className="ui-card">
                  <span>Mouvements retenus</span>
                  <strong>{retained}</strong>
                </div>
                <div className="ui-card">
                  <span>À vérifier</span>
                  <strong>{ambiguous}</strong>
                </div>
                <div className="ui-card">
                  <span>Non retenus</span>
                  <strong>{excluded}</strong>
                </div>
              </div>
              {stale && (
                <p role="status" className="conversion-alert warning">
                  Les choix ont changé. Cliquez sur « Appliquer le filtre » pour recalculer l’aperçu
                  avant l’export.
                </p>
              )}
              <section
                className="ui-card entry-model-review"
                aria-labelledby="entry-model-output-title"
              >
                <div className="entry-model-toolbar">
                  <div>
                    <h2 id="entry-model-output-title" className="font-semibold">
                      Résultat des écritures
                    </h2>
                    {displayFormat && (
                      <p className="text-sm font-medium text-primary mt-2">
                        Modèle d’affichage : {displayFormat.name}
                      </p>
                    )}
                    <p className="muted text-sm mt-2">Montants HT après déduction de la TVA.</p>
                  </div>
                  <div className="flex flex-wrap items-center gap-3">
                    <span className="ui-tag">{resultRows.length} mouvement(s)</span>
                    <Button
                      variant="outline"
                      size="sm"
                      aria-expanded={analysisOpen}
                      aria-controls="entry-analysis"
                      onClick={() => {
                        setAnalysisOpen(!analysisOpen);
                        if (!analysisOpen) {
                          requestAnimationFrame(() =>
                            document.getElementById('entry-analysis')?.scrollIntoView({
                              behavior: window.matchMedia('(prefers-reduced-motion: reduce)')
                                .matches
                                ? 'instant'
                                : 'smooth',
                              block: 'start',
                            }),
                          );
                        }
                      }}
                    >
                      <ChartNoAxesCombined size={16} /> Analyse
                    </Button>
                  </div>
                </div>
                {error && (
                  <p className="conversion-alert error" role="alert">
                    {error}
                  </p>
                )}
                {preview.errors.length > 0 && !error && (
                  <p className="muted text-sm" role="status">
                    {preview.errors.every((issue) => issue.code === 'SOURCE_NOT_VALIDATED')
                      ? 'Terminer et télécharger valide le document source et archive le résultat.'
                      : preview.errors.map((issue) => issue.message).join(' ')}
                  </p>
                )}
                {preview.output && preview.resultRows ? (
                  <EntryResultTable output={preview.output} rows={preview.resultRows} />
                ) : (
                  <div className="entry-model-table-wrap">
                    <table
                      className="entry-model-results-table entry-model-output-table"
                      aria-label="Résultat du modèle après TVA"
                    >
                      <thead>
                        <tr>
                          <th scope="col">N° compte</th>
                          <th scope="col">Intitulé</th>
                          <th scope="col">Libellé</th>
                          <th scope="col">Banque</th>
                          <th scope="col" className="amount">
                            Montant
                          </th>
                        </tr>
                      </thead>
                      <tbody>
                        {resultRows.map((row) => {
                          const model = row.candidates.find((c) => c.id === row.modelId);
                          return (
                            <tr key={row.transactionId}>
                              <td data-label="N° compte" className="entry-model-output-account">
                                {model?.accountCode || '—'}
                              </td>
                              <td data-label="Intitulé" className="font-medium">
                                {model?.name || '—'}
                              </td>
                              <td data-label="Libellé">{row.description || '—'}</td>
                              <td data-label="Banque">{resultBank}</td>
                              <td data-label="Montant HT" className="amount">
                                {money(row.net)}
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                )}
                {!resultRows.length && (
                  <p className="muted text-sm" role="status">
                    {ambiguous > 0
                      ? 'Vérifiez les correspondances ci-dessous pour afficher les montants calculés.'
                      : 'Aucun mouvement retenu pour ce modèle.'}
                  </p>
                )}
              </section>
              <section
                id="entry-analysis"
                className="ui-card entry-model-result-details"
                hidden={!analysisOpen && ambiguous === 0}
              >
                <div className="entry-model-toolbar">
                  <h2 className="font-semibold">Analyse des montants</h2>
                  <span className="ui-tag">{resultRows.length} mouvements retenus</span>
                </div>
                <dl className="entry-analysis-totals">
                  <div>
                    <dt>Total TTC</dt>
                    <dd>{money(totals.gross / 100)}</dd>
                  </div>
                  <div>
                    <dt>Total HT</dt>
                    <dd>{money(totals.net / 100)}</dd>
                  </div>
                  <div>
                    <dt>TVA</dt>
                    <dd>{money(totals.tax / 100)}</dd>
                  </div>
                </dl>
                <div className="entry-model-review mt-4">
                  <div className="entry-model-toolbar">
                    <h2 className="font-semibold">Vérification des libellés et de la taxe</h2>
                    <label className="entry-model-check">
                      <input
                        type="checkbox"
                        checked={onlyRetained}
                        onChange={(e) => setOnlyRetained(e.target.checked)}
                      />{' '}
                      Mouvements retenus uniquement
                    </label>
                  </div>
                  <div className="entry-model-table-wrap">
                    <table className="entry-model-results-table">
                      <thead>
                        <tr>
                          <th>Date</th>
                          <th>Libellé original</th>
                          <th>Modèle / correspondances</th>
                          <th className="amount">TTC</th>
                          <th className="amount">HT</th>
                          <th className="amount">TVA</th>
                          <th>Statut</th>
                        </tr>
                      </thead>
                      <tbody>
                        {visible.map((row) => (
                          <tr key={row.transactionId}>
                            <td data-label="Date">{date(row.date)}</td>
                            <td data-label="Libellé original">
                              {row.description}
                              <p className="entry-model-row-note">{row.message}</p>
                            </td>
                            <td data-label="Modèle">
                              {row.candidates.length > 0 ? (
                                <>
                                  <select
                                    className="ui-input"
                                    aria-label={`Modèle pour la ligne ${row.sequence}`}
                                    disabled={busy}
                                    value={
                                      row.transactionId in decisions
                                        ? decisions[row.transactionId] || '__exclude__'
                                        : row.modelId || ''
                                    }
                                    onChange={(e) => decision(row, e.target.value)}
                                  >
                                    <option value="" disabled>
                                      Choisir après vérification…
                                    </option>
                                    {row.candidates.map((c) => (
                                      <option key={c.id} value={c.id}>
                                        {c.name} · {c.accountCode} · {c.taxRate} %
                                      </option>
                                    ))}
                                    <option value="__exclude__">Exclure ce mouvement</option>
                                  </select>
                                  <p className="entry-model-row-note">
                                    Mots trouvés :{' '}
                                    {[...new Set(row.candidates.flatMap((c) => c.keywords))].join(
                                      ', ',
                                    )}
                                  </p>
                                </>
                              ) : (
                                '—'
                              )}
                            </td>
                            <td data-label="TTC" className="amount">
                              {money(row.total)}
                            </td>
                            <td data-label="HT" className="amount">
                              {money(row.net)}
                            </td>
                            <td data-label="TVA" className="amount">
                              {money(row.tax)}
                            </td>
                            <td data-label="Statut">
                              <span
                                className={row.status === 'MATCHED' ? 'status-positive' : 'ui-tag'}
                              >
                                {row.status === 'MATCHED'
                                  ? 'Retenu'
                                  : row.status === 'AMBIGUOUS'
                                    ? 'Correspondances multiples'
                                    : row.status === 'INVALID'
                                      ? 'À corriger'
                                      : row.status === 'EXCLUDED'
                                        ? 'Exclu'
                                        : 'Sans correspondance'}
                              </span>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                  {!visible.length && (
                    <p className="muted text-sm">Aucun mouvement dans cette vue.</p>
                  )}
                </div>
              </section>
              {preview.lines.length > 0 && (
                <details className="ui-card entry-model-result-details">
                  <summary>Écritures comptables détaillées · {preview.lines.length} lignes</summary>
                  <div className="entry-model-table-wrap mt-4">
                    <table className="entry-model-results-table">
                      <thead>
                        <tr>
                          <th>Journal / pièce</th>
                          <th>Compte</th>
                          <th>Libellé</th>
                          <th className="amount">Débit</th>
                          <th className="amount">Crédit</th>
                        </tr>
                      </thead>
                      <tbody>
                        {preview.lines.map((line, index) => (
                          <tr key={index}>
                            <td data-label="Journal / pièce">
                              {line.journal} · {line.pieceNumber}
                            </td>
                            <td data-label="Compte">{line.accountCode}</td>
                            <td data-label="Libellé">{line.description}</td>
                            <td data-label="Débit" className="amount">
                              {money(line.debit)}
                            </td>
                            <td data-label="Crédit" className="amount">
                              {money(line.credit)}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </details>
              )}
              <section
                className="ui-card entry-completion-bar"
                aria-label="Finaliser le traitement"
              >
                <Button
                  variant="outline"
                  disabled={busy}
                  onClick={() => (guided ? onNavigate?.(2) : window.history.back())}
                >
                  <ArrowLeft size={16} /> Retour
                </Button>
                <p className="muted text-sm">Le fichier sera archivé dans l’espace client.</p>
                <Button
                  permission="documents.export"
                  disabled={busy || loading || stale || ambiguous > 0 || !resultRows.length}
                  variant="outline"
                  onClick={() => void download(true, true)}
                >
                  <Archive size={16} /> {busy ? 'Traitement…' : 'Terminer et archiver'}
                </Button>
                <Button
                  permission="documents.export"
                  variant="outline"
                  disabled={
                    busy ||
                    loading ||
                    stale ||
                    ambiguous > 0 ||
                    !resultRows.length ||
                    !preview?.output?.columns.some((column) => column.export)
                  }
                  onClick={downloadExcelResult}
                >
                  <Download size={16} /> Télécharger Excel
                </Button>
                <Button
                  permission="documents.export"
                  disabled={busy || loading || stale || ambiguous > 0 || !resultRows.length}
                  onClick={() => void download(true, false)}
                >
                  <Download size={16} /> {busy ? 'Traitement…' : 'Terminer et télécharger'}
                </Button>
                {detail.data?.status === 'VALIDATED' ? (
                  <span className="status-positive inline-flex items-center gap-2" role="status">
                    <CheckCircle2 size={16} /> Vérification confirmée
                  </span>
                ) : (
                  <Button
                    permission="documents.write"
                    variant="outline"
                    disabled={busy || loading || stale || !preview || !can('documents.write')}
                    onClick={() => void confirmVerification()}
                  >
                    {busy ? 'Confirmation…' : 'Confirmer la vérification'}
                  </Button>
                )}
              </section>
            </>
          )}
        </div>
        {guided && detail.data && (
          <div className="entry-conversion-document">
            <div className="entry-document-heading">
              <h2 className="font-semibold">Document source</h2>
              <Button
                variant="ghost"
                size="sm"
                aria-label={pdfCollapsed ? 'Afficher le PDF' : 'Réduire le PDF'}
                aria-expanded={!pdfCollapsed}
                aria-controls="entry-source-pdf"
                onClick={() => setPdfCollapsed(!pdfCollapsed)}
              >
                {pdfCollapsed ? <PanelRightOpen size={16} /> : <PanelRightClose size={16} />}
                {pdfCollapsed ? 'Afficher' : 'Réduire'}
              </Button>
            </div>
            <div id="entry-source-pdf" hidden={pdfCollapsed}>
              <StatementPdfPanel
                id={detail.data.id}
                version={detail.data.version}
                hasRows={detail.data.statement.transactions.length > 0}
                extractId={extractId || undefined}
              />
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
