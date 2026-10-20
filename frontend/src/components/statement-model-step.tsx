'use client';
import { useState } from 'react';
import { ArrowRight, FileCheck2, FileOutput, LoaderCircle } from 'lucide-react';
import { Button } from './ui/button';
import { ConversionSteps } from './conversion-steps';
import { StatementSourceText } from './statement-source-text';
import { ExportTemplate, request } from '@/services/api';
import { reuseValidatedAdaptation } from '@/services/statement-adaptation';
import type { StatementDetail, PreparedStatementExport } from '@/services/statement-exports';

export function StatementModelStep({
  detail,
  templates,
  templateId,
  onTemplate,
  onPrepared,
  loading,
  error,
  onRetry,
  onImport,
  extractMode = detail.statement.conversionType === 'EXTRACT',
  extractId,
}: {
  detail: StatementDetail;
  templates: ExportTemplate[];
  templateId: string;
  onTemplate: (id: string) => void;
  onPrepared: (result: PreparedStatementExport) => void;
  loading: boolean;
  error: string;
  onRetry: () => void;
  onImport?: () => void;
  extractMode?: boolean;
  extractId?: string;
}) {
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState('');
  const [sourceDetail, setSourceDetail] = useState(detail);
  const [showSource, setShowSource] = useState(false);
  const model = templates.find((item) => item.id === templateId && item.isActive);
  async function prepare() {
    if (!model || busy) return;
    setBusy(true);
    setFailure('');
    try {
      const adaptation = await reuseValidatedAdaptation(sourceDetail, model.fields);
      setSourceDetail(adaptation.detail);
      const result = await request<PreparedStatementExport>(
        `/statement-imports/${detail.id}/prepare`,
        {
          method: 'POST',
          body: JSON.stringify({
            statementVersion: adaptation.detail.version,
            templateId: model.id,
            ...(extractMode && adaptation.detail.statement.extracts?.length
              ? { extractId: extractId ?? adaptation.detail.statement.extracts.at(-1)?.id }
              : {}),
          }),
        },
      );
      if (adaptation.reused) {
        result.preview.warnings = [
          ...result.preview.warnings,
          {
            code: 'SAVED_ADAPTATION_REUSED',
            message:
              'Un modèle d’extraction enregistré ou un réglage vérifié de même format a été réutilisé.',
            transactionSequence: null,
            fieldName: null,
            sourceField: null,
          },
        ];
      }
      onPrepared(result);
    } catch (e) {
      setFailure((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="ui-card conversion-model-step" aria-label="Choix du modèle d’exportation">
      <ConversionSteps current={2} onNavigate={() => onImport?.()} disabled={busy} />
      {detail.extraction?.performance && (
        <p role="status" className="muted text-sm">
          Lecture en{' '}
          {(detail.extraction.performance.durationMs / 1000).toLocaleString('fr-FR', {
            maximumFractionDigits: 1,
          })}{' '}
          s · {detail.extraction.performance.nativePages} page(s) locales ·{' '}
          {detail.extraction.performance.paddlePages} PaddleOCR ·{' '}
          {detail.extraction.performance.aiPages} avec assistance IA. Les données restent à vérifier
          avant validation.
        </p>
      )}
      {['OPENROUTER', 'HYBRID'].includes(detail.extraction?.method ?? '') && (
        <details
          className="rounded-xl border p-3"
          onToggle={(event) => setShowSource(event.currentTarget.open)}
        >
          <summary className="cursor-pointer font-semibold">
            1. Consulter la transcription du relevé
          </summary>
          {showSource && <StatementSourceText id={detail.id} />}
        </details>
      )}
      <div className="model-step-heading">
        <span className="model-step-icon">
          <FileOutput size={26} />
        </span>
        <div>
          <p className="conversion-eyebrow">FORMAT DE SORTIE</p>
          <h1>Quel fichier souhaitez-vous obtenir ?</h1>
          <p>
            {extractMode
              ? 'Choisissez le même modèle que pour un relevé. Toutes les opérations de l’extrait PDF seront converties, puis présentées pour vérification.'
              : 'Le résultat sera adapté aux colonnes et aux formats du modèle choisi.'}
          </p>
        </div>
      </div>
      <div className="model-step-document">
        <FileCheck2 size={19} />
        <strong>{detail.sourceFileName || detail.statement.bankName}</strong>
        <span>
          {detail.statement.transactions.length} mouvements lus · {detail.statement.currency}
        </span>
      </div>
      <label className="conversion-template-label">
        Modèle d’exportation
        <select
          className="ui-input mt-2"
          aria-label="Modèle d’exportation"
          value={templateId}
          disabled={busy || loading}
          onChange={(event) => onTemplate(event.target.value)}
        >
          <option value="">Choisir un modèle…</option>
          {templates
            .filter((item) => item.isActive)
            .map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
              </option>
            ))}
        </select>
      </label>
      {loading && <p role="status">Chargement des modèles…</p>}
      {error && (
        <div role="alert" className="conversion-alert error">
          {error}
          <Button variant="outline" onClick={onRetry}>
            Réessayer
          </Button>
        </div>
      )}
      {!loading && !error && !templates.some((item) => item.isActive) && (
        <p className="conversion-alert warning">
          Activez ou créez un modèle dans « Modèles d’exportation » pour continuer.
        </p>
      )}
      {model && (
        <div className="model-step-fields">
          <div>
            <strong>{model.name}</strong>
            <span>
              {model.fields.length} colonnes · .{model.fileExtension}
            </span>
          </div>
          <ul aria-label="Colonnes du modèle">
            {[...model.fields]
              .sort((a, b) => a.position - b.position)
              .map((field) => (
                <li key={field.position}>
                  {field.fieldName}
                  {field.required && (
                    <span title="Obligatoire" aria-label="obligatoire">
                      {' '}
                      *
                    </span>
                  )}
                </li>
              ))}
          </ul>
          <p>
            Les champs marqués * sont obligatoires. Les valeurs absentes du PDF seront signalées
            dans le résultat. Le modèle définit les colonnes et les formats du fichier ; les données
            sont récupérées dans le relevé bancaire. Le PDF structuré conserve les champs du
            document source.
          </p>
        </div>
      )}
      {failure && (
        <p role="alert" className="conversion-alert error">
          {failure}
        </p>
      )}
      <div className="model-step-footer">
        <p>Dates, montants, sens et champs requis contrôlés avant le téléchargement.</p>
        <Button
          permission="documents.write"
          disabled={!model || busy || loading}
          onClick={() => void prepare()}
        >
          {busy ? <LoaderCircle size={16} className="animate-spin" /> : <ArrowRight size={16} />}
          {busy ? 'Adaptation au modèle…' : 'Afficher le résultat'}
        </Button>
      </div>
    </section>
  );
}
