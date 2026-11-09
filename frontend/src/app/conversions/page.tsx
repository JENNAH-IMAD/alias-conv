'use client';
import { Suspense, startTransition, useEffect, useState, ViewTransition } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import {
  ArrowRight,
  ArrowRightLeft,
  CheckCircle2,
  FileText,
  FileOutput,
  Plus,
  ShieldCheck,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/page-header';
import { ConversionDetailHeader } from '@/components/route-detail-transition';
import { StatementImportDialog } from '@/components/statement-import-dialog';
import { StatementWorkspace } from '@/components/statement-workspace';
import { StatementModelStep } from '@/components/statement-model-step';
import {
  AccountingEntryModelFlow,
  type EntryFlowSelection,
} from '@/components/accounting-entry-model-flow';
import { EntryModelResultsWorkspace } from '@/components/entry-model-results-workspace';
import { isAccountingTemplate } from '@/services/export-template-defaults';
import { ExportTemplate } from '@/services/api';
import { useApi, useAll } from '@/services/use-api';
import { StatementDetail, PreparedStatementExport } from '@/services/statement-exports';
import { confirmDiscard, hasUnsavedChanges } from '@/lib/use-unsaved-changes';
import type { ConversionIntent } from '@/lib/conversion-intent';
import type { EntryModelPreview } from '@/services/entry-models';
import type { SavedTreatment } from '@/services/saved-treatment';

export default function ConversionsPage() {
  return (
    <Suspense fallback={<p role="status">Chargement des conversions…</p>}>
      <ConversionStudio />
    </Suspense>
  );
}
function ConversionStudio() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const [importing, setImporting] = useState(false);
  const [importType, setImportType] = useState<ConversionIntent>('STATEMENT');
  const queryImport = searchParams.get('import') === '1';
  const selectedId = searchParams.get('statementId');
  const [templateChoice, setTemplateChoice] = useState<{
    statementId: string | null;
    value: string;
  } | null>(null);
  const templateId =
    templateChoice?.statementId === selectedId
      ? templateChoice.value
      : searchParams.get('templateId') || '';
  function setTemplateId(value: string) {
    setTemplateChoice({ statementId: selectedId, value });
  }
  const [prepared, setPrepared] = useState<PreparedStatementExport | null>(null);
  const [entryPrepared, setEntryPrepared] = useState<{
    sourceId: string;
    selection: string;
    preview: EntryModelPreview;
  } | null>(null);
  const [savedTab, setSavedTab] = useState<{ id: string; tab: 'transactions' | 'preview' } | null>(
    null,
  );
  const flowStep = searchParams.get('step');
  const templates = useAll<ExportTemplate>('/export-templates');
  const accountingMode = searchParams.get('type') === 'accounting';
  useEffect(() => {
    if (!accountingMode || flowStep !== 'journal') return;
    const params = new URLSearchParams(searchParams.toString());
    params.set('step', 'model');
    router.replace(`/conversions?${params}`, { scroll: false });
  }, [accountingMode, flowStep, searchParams, router]);
  const availableTemplates = (templates.data || []).filter(
    (item) => !accountingMode || isAccountingTemplate(item.type),
  );
  const detail = useApi<StatementDetail>(selectedId ? `/statement-imports/${selectedId}` : null);
  const savedTreatment = useApi<SavedTreatment | null>(
    selectedId && !queryImport
      ? `/statement-imports/${selectedId}/treatment${searchParams.get('extractId') ? `?extractId=${encodeURIComponent(searchParams.get('extractId')!)}` : ''}`
      : null,
  );
  const restored =
    savedTreatment.data?.statementId === selectedId &&
    ['ENTRY', 'STATEMENT'].includes(savedTreatment.data.kind)
      ? savedTreatment.data
      : null;
  useEffect(() => {
    if (!restored || queryImport) return;
    const formatChange =
      restored.kind === 'ENTRY' &&
      accountingMode &&
      flowStep === 'model' &&
      searchParams.has('outputTemplateId') &&
      !searchParams.has('entryModelId');
    const resumeChangedEntry =
      restored.kind === 'ENTRY' &&
      flowStep === 'entry-result' &&
      !entryPrepared &&
      (searchParams.get('entryModelId') !== restored.selection.modelId ||
        searchParams.get('entryAbbreviation') !== restored.selection.abbreviation);
    if (flowStep && !formatChange && !resumeChangedEntry) return;
    if (
      !formatChange &&
      !resumeChangedEntry &&
      searchParams.has('templateId') &&
      (restored.kind !== 'STATEMENT' ||
        searchParams.get('templateId') !== restored.selection.templateId)
    )
      return;
    const params = new URLSearchParams(searchParams.toString());
    if (restored.kind === 'ENTRY') {
      const run = restored.selection;
      if (!run.modelId || !run.clientId) return;
      params.set('type', 'accounting');
      params.set('step', 'entry-result');
      params.set('clientId', run.clientId);
      params.set('entryModelId', run.modelId);
      params.set('entryAbbreviation', run.abbreviation);
      params.set('entryTemplateId', run.exportTemplateId);
      if (restored.bankAccountId) params.set('entryBankAccountId', restored.bankAccountId);
      if (!formatChange && run.outputTemplateId)
        params.set('outputTemplateId', run.outputTemplateId);
    } else {
      params.set('step', 'result');
      params.set('templateId', restored.selection.templateId);
    }
    router.replace(`/conversions?${params}`, { scroll: false });
  }, [restored, queryImport, accountingMode, flowStep, searchParams, router, entryPrepared]);
  const currentDetail =
    prepared?.detail.id === selectedId &&
    (!detail.data || prepared.detail.version >= detail.data.version)
      ? prepared.detail
      : detail.data;
  const selectedTemplate = availableTemplates.find(
    (item) => item.id === templateId && item.isActive,
  );
  function finishPreparation(result: PreparedStatementExport) {
    setPrepared(result);
    const params = new URLSearchParams(searchParams.toString());
    params.set('statementId', result.detail.id);
    params.set('step', 'result');
    params.set('templateId', result.preview.template.id);
    router.replace(`/conversions?${params}`, { scroll: false });
  }
  function chooseStep(step: string) {
    const params = new URLSearchParams(searchParams.toString());
    params.set('step', step);
    if (templateId) params.set('templateId', templateId);
    router.replace(`/conversions?${params}`, { scroll: false });
  }
  function entryStep(
    selection: EntryFlowSelection,
    step: 'model' | 'entry-result',
    preview?: EntryModelPreview,
  ) {
    if (!confirmDiscard(hasUnsavedChanges())) return;
    setTemplateId(selection.exportTemplateId);
    setEntryPrepared(
      preview && selectedId
        ? { sourceId: selectedId, selection: JSON.stringify(selection), preview }
        : null,
    );
    const params = new URLSearchParams(searchParams.toString());
    params.set('type', 'accounting');
    params.set('step', step);
    params.set('clientId', selection.clientId);
    params.set('entryBankAccountId', selection.bankAccountId);
    params.set('entryTemplateId', selection.exportTemplateId);
    params.set('entryAbbreviation', selection.abbreviation);
    params.set('entryModelId', selection.modelId);
    if (selection.outputTemplateId) params.set('outputTemplateId', selection.outputTemplateId);
    else params.delete('outputTemplateId');
    params.delete('processing');
    if (selection.exportTemplateId) params.set('templateId', selection.exportTemplateId);
    router.replace(`/conversions?${params}`, { scroll: false });
  }
  const entrySelection: EntryFlowSelection = {
    clientId: currentDetail?.clientId || searchParams.get('clientId') || '',
    bankAccountId: searchParams.get('entryBankAccountId') || currentDetail?.bankAccountId || '',
    exportTemplateId: searchParams.get('entryTemplateId') || '',
    abbreviation: searchParams.get('entryAbbreviation') || '',
    modelId: searchParams.get('entryModelId') || '',
    ...(searchParams.get('outputTemplateId')
      ? { outputTemplateId: searchParams.get('outputTemplateId')! }
      : {}),
  };
  function startConversion(type: ConversionIntent) {
    if (!confirmDiscard(hasUnsavedChanges())) return;
    setImportType(type);
    startTransition(() => setImporting(true));
  }
  return (
    <div className="conversion-page">
      <ViewTransition
        key={
          importing || queryImport
            ? 'import'
            : `${selectedId || 'start'}:${flowStep || 'review'}:${searchParams.get('extractId') || ''}`
        }
        name="conversion-stage"
        share="workspace-page"
        enter="workspace-page"
        exit="workspace-page"
        default="none"
      >
        <div className="conversion-stage">
          {importing || queryImport ? (
            <StatementImportDialog
              initialConversionType={
                importing
                  ? importType
                  : accountingMode
                    ? 'ACCOUNTING'
                    : searchParams.get('type') === 'extract'
                      ? 'EXTRACT'
                      : importType
              }
              initialClientId={searchParams.get('clientId') || ''}
              onClose={() => {
                startTransition(() => setImporting(false));
                if (queryImport) {
                  const params = new URLSearchParams(searchParams.toString());
                  params.delete('import');
                  params.delete('type');
                  router.replace(`/conversions${params.size ? `?${params}` : ''}`, {
                    scroll: false,
                  });
                }
              }}
              onImported={(record, clientId, intent) => {
                startTransition(() => setImporting(false));
                setPrepared(null);
                setTemplateId('');
                const entryParams = new URLSearchParams();
                if (intent === 'ACCOUNTING')
                  for (const key of [
                    'entryBankAccountId',
                    'entryTemplateId',
                    'entryAbbreviation',
                    'entryModelId',
                  ]) {
                    const value = searchParams.get(key);
                    if (value) entryParams.set(key, value);
                  }
                router.replace(
                  `/conversions?statementId=${record.id}&step=model${intent === 'ACCOUNTING' ? '&type=accounting' : record.statement.conversionType === 'EXTRACT' ? '&type=extract' : ''}${clientId ? '&clientId=' + encodeURIComponent(clientId) : ''}${entryParams.size ? '&' + entryParams : ''}`,
                  { scroll: false },
                );
              }}
            />
          ) : (
            <>
              {!selectedId && (
                <PageHeader
                  eyebrow="Conversion & export"
                  title="Relevés, extraits et écritures comptables"
                  description="Importez un relevé ou un extrait bancaire, puis choisissez un format de conversion ou une écriture comptable du client."
                  icon={ArrowRightLeft}
                >
                  <div className="conversion-hero-steps">
                    <span>
                      <FileText size={15} />
                      Importer
                    </span>
                    <ArrowRight size={14} />
                    <span>
                      <FileOutput size={15} />
                      Choisir le modèle
                    </span>
                    <ArrowRight size={14} />
                    <span>
                      <ShieldCheck size={15} />
                      Vérifier et paramétrer
                    </span>
                    <ArrowRight size={14} />
                    <span>
                      <FileOutput size={15} />
                      Télécharger
                    </span>
                  </div>
                </PageHeader>
              )}
              {selectedId &&
                !(
                  accountingMode &&
                  flowStep === 'entry-result' &&
                  entrySelection.clientId &&
                  entrySelection.modelId
                ) && (
                  <ConversionDetailHeader
                    id={selectedId}
                    extractId={searchParams.get('extractId')}
                    title={currentDetail?.sourceFileName}
                    subtitle={currentDetail?.statement.bankName}
                  />
                )}
              <div className="conversion-studio conversion-studio-full">
                <div className="conversion-content">
                  {selectedId ? (
                    detail.loading || savedTreatment.loading ? (
                      <div className="conversion-empty" role="status">
                        <div className="conversion-loading-dot" />
                        <p>Ouverture du relevé…</p>
                      </div>
                    ) : detail.error ? (
                      <div className="conversion-alert error" role="alert">
                        {detail.error}
                        <Button variant="outline" onClick={detail.reload}>
                          Réessayer
                        </Button>
                      </div>
                    ) : (
                      currentDetail &&
                      ((accountingMode || selectedTemplate?.type === 'SAGE100ENTRIES') &&
                      ['model', 'journal', 'entry-result'].includes(flowStep || '') ? (
                        flowStep === 'entry-result' &&
                        entrySelection.clientId &&
                        entrySelection.abbreviation &&
                        entrySelection.modelId ? (
                          <EntryModelResultsWorkspace
                            key={`${currentDetail.id}:${entrySelection.clientId}:${entrySelection.bankAccountId}:${entrySelection.exportTemplateId}:${entrySelection.abbreviation}:${entrySelection.modelId}:${entrySelection.outputTemplateId || ''}`}
                            guided
                            clientId={entrySelection.clientId}
                            initialSource={currentDetail.id}
                            initialExtract={searchParams.get('extractId') || ''}
                            initialFormat={entrySelection.exportTemplateId}
                            initialAbbreviation={entrySelection.abbreviation}
                            initialModelId={entrySelection.modelId}
                            initialOutputTemplateId={entrySelection.outputTemplateId}
                            initialRun={
                              restored?.kind === 'ENTRY' &&
                              restored.selection.modelId === entrySelection.modelId &&
                              !(
                                entryPrepared?.sourceId === currentDetail.id &&
                                entryPrepared.selection === JSON.stringify(entrySelection)
                              )
                                ? restored.selection
                                : undefined
                            }
                            initialPreview={
                              entryPrepared?.sourceId === currentDetail.id &&
                              entryPrepared.selection === JSON.stringify(entrySelection)
                                ? entryPrepared.preview
                                : restored?.kind === 'ENTRY' &&
                                    restored.current &&
                                    restored.sourceVersion === currentDetail.version &&
                                    restored.selection.modelId === entrySelection.modelId &&
                                    (restored.selection.outputTemplateId || '') ===
                                      (entrySelection.outputTemplateId || '')
                                  ? restored.preview || undefined
                                  : undefined
                            }
                            onNavigate={(step) =>
                              step === 1
                                ? startConversion('ACCOUNTING')
                                : entryStep(entrySelection, 'model')
                            }
                          />
                        ) : (
                          <AccountingEntryModelFlow
                            key={`${currentDetail.id}:${flowStep}:${entrySelection.clientId}`}
                            detail={currentDetail}
                            initial={entrySelection}
                            onReview={(selection, preview) =>
                              entryStep(selection, 'entry-result', preview)
                            }
                            extractId={searchParams.get('extractId') || undefined}
                            onImport={() => startConversion('ACCOUNTING')}
                            onReload={() => {
                              detail.reload();
                              templates.reload();
                            }}
                          />
                        )
                      ) : flowStep === 'model' || flowStep === 'journal' ? (
                        <StatementModelStep
                          extractId={searchParams.get('extractId') ?? undefined}
                          detail={currentDetail}
                          extractMode={
                            currentDetail.statement.conversionType === 'EXTRACT' ||
                            searchParams.get('type') === 'extract'
                          }
                          templates={availableTemplates}
                          accountingMode={accountingMode}
                          templateId={templateId}
                          onTemplate={setTemplateId}
                          loading={templates.loading}
                          error={templates.error}
                          onRetry={templates.reload}
                          onImport={() =>
                            startConversion(
                              accountingMode
                                ? 'ACCOUNTING'
                                : (currentDetail.statement.conversionType ?? 'STATEMENT'),
                            )
                          }
                          initialEntrySelection={entrySelection}
                          onEntryReview={(selection, preview) =>
                            entryStep(selection, 'entry-result', preview)
                          }
                          onReloadEntryModels={() => {
                            detail.reload();
                            templates.reload();
                          }}
                          onPrepared={finishPreparation}
                        />
                      ) : (
                        <StatementWorkspace
                          key={`${currentDetail.id}-${currentDetail.version}`}
                          detail={currentDetail}
                          extractMode={
                            currentDetail.statement.conversionType === 'EXTRACT' ||
                            searchParams.get('type') === 'extract'
                          }
                          initialExtractId={
                            searchParams.get('extractId') ??
                            (restored?.kind === 'STATEMENT' ? '' : undefined)
                          }
                          guided={flowStep === 'result'}
                          onImport={() => {
                            if (confirmDiscard(hasUnsavedChanges())) {
                              setImportType(
                                accountingMode ||
                                  (selectedTemplate && isAccountingTemplate(selectedTemplate.type))
                                  ? 'ACCOUNTING'
                                  : (currentDetail.statement.conversionType ?? 'STATEMENT'),
                              );
                              startTransition(() => setImporting(true));
                            }
                          }}
                          initialTab={
                            savedTab?.id === currentDetail.id
                              ? savedTab.tab
                              : restored?.kind === 'STATEMENT' &&
                                  restored.selection.templateId === templateId
                                ? 'preview'
                                : undefined
                          }
                          initialPreview={
                            prepared?.detail.version === currentDetail.version
                              ? prepared.preview
                              : restored?.kind === 'STATEMENT' &&
                                  restored.current &&
                                  restored.sourceVersion === currentDetail.version &&
                                  restored.selection.templateId === templateId
                                ? restored.preview || undefined
                                : undefined
                          }
                          onChooseTemplate={() => {
                            if (confirmDiscard(hasUnsavedChanges())) chooseStep('model');
                          }}
                          templates={availableTemplates}
                          templateId={templateId}
                          onTemplate={(id) => {
                            setTemplateId(id);
                            setPrepared(null);
                            if (id) {
                              const params = new URLSearchParams(searchParams.toString());
                              params.set('templateId', id);
                              params.set('step', 'result');
                              router.replace(`/conversions?${params}`, { scroll: false });
                            }
                          }}
                          onTemplatesChanged={(id) => {
                            setTemplateId(id);
                            setPrepared(null);
                            detail.reload();
                            templates.reload();
                            if (flowStep === 'result') {
                              const params = new URLSearchParams(searchParams.toString());
                              params.set('step', 'result');
                              params.set('templateId', id);
                              router.replace(`/conversions?${params}`, { scroll: false });
                            }
                          }}
                          onSaved={(tab) => {
                            if (searchParams.has('extractId')) {
                              const params = new URLSearchParams(searchParams.toString());
                              params.delete('extractId');
                              router.replace(`/conversions?${params}`, { scroll: false });
                            }
                            if (tab) setSavedTab({ id: currentDetail.id, tab });
                            setPrepared(null);
                            detail.reload();
                          }}
                        />
                      ))
                    )
                  ) : (
                    <section className="conversion-empty conversion-start">
                      <div
                        className="conversion-empty-illustration conversion-file-transfer"
                        aria-hidden="true"
                      >
                        <span
                          className="conversion-file-card conversion-file-source"
                          aria-hidden="true"
                        >
                          <span className="conversion-file-card-back" aria-hidden="true" />
                          <FileText size={38} aria-hidden="true" />
                        </span>
                        <ArrowRight className="conversion-file-arrow" size={22} aria-hidden="true" />
                        <span
                          className="conversion-file-card conversion-file-result"
                          aria-hidden="true"
                        >
                          <span className="conversion-file-card-back" aria-hidden="true" />
                          <FileOutput size={38} aria-hidden="true" />
                        </span>
                      </div>
                      <p className="conversion-eyebrow">CONVERSION BANCAIRE</p>
                      <h2>Quel document souhaitez-vous importer ?</h2>
                      <p>
                        Importez un relevé complet ou un extrait bancaire. Après l’import, choisissez
                        un modèle de conversion ou une écriture comptable enregistrée pour le client.
                      </p>
                      <div className="conversion-start-actions">
                        <Button
                          permission="documents.write"
                          size="lg"
                          onClick={() => startConversion('STATEMENT')}
                        >
                          <Plus size={17} />
                          Importer un relevé
                        </Button>
                        <Button
                          permission="documents.write"
                          variant="outline"
                          size="lg"
                          onClick={() => startConversion('EXTRACT')}
                        >
                          <Plus size={17} />
                          Importer un extrait
                        </Button>
                        <span>PDF bancaires · Relevés et extraits · Écritures comptables du client</span>
                      </div>
                      <div className="conversion-empty-features">
                        <span>
                          <ShieldCheck size={15} />
                          Validation des données
                        </span>
                        <span>
                          <FileOutput size={15} />
                          CSV & Sage
                        </span>
                        <span>
                          <CheckCircle2 size={15} />
                          Modèles préservés
                        </span>
                      </div>
                    </section>
                  )}
                </div>
              </div>
            </>
          )}
        </div>
      </ViewTransition>
    </div>
  );
}
