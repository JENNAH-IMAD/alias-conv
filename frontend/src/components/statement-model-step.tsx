'use client';
import { useState } from 'react';
import { ArrowRight, FileCheck2, FileOutput, LoaderCircle } from 'lucide-react';
import { Button } from './ui/button';
import { ConversionSteps } from './conversion-steps';
import { StatementSourceText } from './statement-source-text';
import { ExportTemplate } from '@/services/api';
import { prepareStatementModel } from '@/services/prepare-statement-model';
import type { StatementDetail, PreparedStatementExport } from '@/services/statement-exports';
import { isAccountingTemplate } from '@/services/export-template-defaults';
import {
  AccountingEntryModelFlow,
  type EntryFlowSelection,
} from './accounting-entry-model-flow';
import type { EntryModelPreview } from '@/services/entry-models';
import type { EntryOutputTemplate } from '@/services/entry-models';
import { useApi } from '@/services/use-api';

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
  initialEntrySelection,
  onEntryReview,
  onReloadEntryModels,
  extractMode = detail.statement.conversionType === 'EXTRACT',
  extractId,
  accountingMode = false,
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
  initialEntrySelection?: EntryFlowSelection;
  onEntryReview?: (selection: EntryFlowSelection, preview: EntryModelPreview) => void;
  onReloadEntryModels?: () => void;
  extractMode?: boolean;
  extractId?: string;
  accountingMode?: boolean;
}) {
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState('');
  const [sourceDetail, setSourceDetail] = useState(detail);
  const [showSource, setShowSource] = useState(false);
  const [modelKind, setModelKind] = useState<'export' | 'entry'>('export');
  const [entryOutputTemplateId, setEntryOutputTemplateId] = useState('');
  const entryOutputTemplates = useApi<EntryOutputTemplate[]>('/accounting/entry-output-templates');
  const combinedModelValue = modelKind === 'entry'
    ? `entry:${entryOutputTemplateId}`
    : templateId ? `export:${templateId}` : '';
  const model = templates.find((item) => item.id === templateId && item.isActive);
  async function prepare() {
    if (!model || busy) return;
    setBusy(true);
    setFailure('');
    try {
      const result = await prepareStatementModel(
        sourceDetail,
        model,
        extractMode,
        extractId,
        setSourceDetail,
      );
      setSourceDetail(result.detail);
      onPrepared(result);
    } catch (e) {
      setFailure((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="ui-card conversion-model-step" aria-label="Choix du modèle d’exportation">
      <ConversionSteps
        current={2}
        accountingMode={accountingMode || (!!model && isAccountingTemplate(model.type))}
        extractMode={extractMode}
        onNavigate={() => onImport?.()}
        disabled={busy}
      />
      {detail.extraction?.performance && (
        <p role="status" className="muted text-sm">
          Lecture en{' '}
          {(detail.extraction.performance.durationMs / 1000).toLocaleString('fr-FR', {
            maximumFractionDigits: 1,
          })}{' '}
          s · {detail.extraction.performance.nativePages} page(s) locales ·{' '}
          {detail.extraction.performance.paddlePages} PaddleOCR. Les données restent à vérifier
          avant validation.
        </p>
      )}
      {detail.extraction?.method === 'HYBRID' && (
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
          <h2>Choisissez un modèle</h2>
          <p>
            {extractMode
              ? 'Choisissez un format d’export ou une écriture enregistrée pour ce client.'
              : 'Choisissez un format d’export ou une écriture comptable enregistrée pour ce client.'}
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
          aria-label="Modèle d’exportation ou d’écriture comptable"
          value={combinedModelValue}
          disabled={busy || loading || entryOutputTemplates.loading}
          onChange={(event) => {
            const value = event.target.value;
            if (value.startsWith('entry:')) {
              setEntryOutputTemplateId(value.slice('entry:'.length));
              setModelKind('entry');
            } else {
              setModelKind('export');
              setEntryOutputTemplateId('');
              onTemplate(value.startsWith('export:') ? value.slice('export:'.length) : '');
            }
          }}
        >
          <option value="">Choisir un modèle…</option>
          <optgroup label="Relevés et extraits bancaires">
            {templates
              .filter((item) => item.isActive && !isAccountingTemplate(item.type))
              .map((item) => (
                <option key={item.id} value={`export:${item.id}`}>
                  {item.name}
                </option>
              ))}
          </optgroup>
          <optgroup label="Écritures comptables">
            {entryOutputTemplates.data
              ?.filter((item) => item.active)
              .map((item) => (
                <option key={item.id} value={`entry:${item.id}`}>
                  {item.name}
                </option>
              ))}
          </optgroup>
        </select>
      </label>
      {modelKind === 'export' && loading && <p role="status">Chargement des modèles…</p>}
      {modelKind === 'export' && error && (
        <div role="alert" className="conversion-alert error">
          {error}
          <Button variant="outline" onClick={onRetry}>
            Réessayer
          </Button>
        </div>
      )}
      {modelKind === 'export' && !loading && !error && !templates.some((item) => item.isActive) && (
        <p className="conversion-alert warning">
          {accountingMode
            ? 'Activez ou créez un modèle Sage 100 — écritures ou Sage X3 — écritures dans « Modèles d’exportation » pour continuer.'
            : 'Activez ou créez un modèle dans « Modèles d’exportation » pour continuer.'}
        </p>
      )}
      {modelKind === 'export' && model && (
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
      {modelKind === 'entry' && initialEntrySelection && onEntryReview && (
        <AccountingEntryModelFlow
          detail={detail}
          initial={{ ...initialEntrySelection, outputTemplateId: entryOutputTemplateId }}
          extractId={extractId}
          embedded
          onReview={onEntryReview}
          onImport={onImport || (() => undefined)}
          onReload={onReloadEntryModels || onRetry}
          onBack={() => {
            setModelKind('export');
            setEntryOutputTemplateId('');
          }}
        />
      )}
      {modelKind === 'export' && failure && (
        <p role="alert" className="conversion-alert error">
          {failure}
        </p>
      )}
      {modelKind === 'export' && (
        <div className="model-step-footer">
          <p>Les données seront contrôlées avant l’export.</p>
          <Button
            permission="documents.write"
            disabled={!model || busy || loading}
            onClick={() => void prepare()}
          >
            {busy ? <LoaderCircle size={16} className="animate-spin" /> : <ArrowRight size={16} />}
            {busy
              ? 'Préparation…'
              : model && isAccountingTemplate(model.type)
                ? 'Vérifier le résultat'
                : 'Afficher le résultat'}
          </Button>
        </div>
      )}
    </section>
  );
}
