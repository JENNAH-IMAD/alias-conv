'use client';
import { Suspense, useState } from 'react';
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
import { StatementImportDialog } from '@/components/statement-import-dialog';
import { StatementWorkspace } from '@/components/statement-workspace';
import { StatementModelStep } from '@/components/statement-model-step';
import { ExportTemplate } from '@/services/api';
import { useApi, useAll } from '@/services/use-api';
import { StatementDetail, PreparedStatementExport } from '@/services/statement-exports';
import { confirmDiscard, hasUnsavedChanges } from '@/lib/use-unsaved-changes';

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
  const queryImport = searchParams.get('import') === '1';
  const selectedId = searchParams.get('statementId');
  const [templateId, setTemplateId] = useState(searchParams.get('templateId') || '');
  const [prepared, setPrepared] = useState<PreparedStatementExport | null>(null);
  const [savedTab, setSavedTab] = useState<{ id: string; tab: 'transactions' | 'preview' } | null>(
    null,
  );
  const flowStep = searchParams.get('step');
  const templates = useAll<ExportTemplate>('/export-templates');
  const detail = useApi<StatementDetail>(selectedId ? `/statement-imports/${selectedId}` : null);
  const currentDetail =
    prepared?.detail.id === selectedId &&
    (!detail.data || prepared.detail.version >= detail.data.version)
      ? prepared.detail
      : detail.data;
  return (
    <div className="conversion-page">
      {importing || queryImport ? (
        <StatementImportDialog
          initialClientId={searchParams.get('clientId') || ''}
          onClose={() => {
            setImporting(false);
            if (queryImport) {
              const params = new URLSearchParams(searchParams.toString());
              params.delete('import');
              router.replace(`/conversions${params.size ? `?${params}` : ''}`, { scroll: false });
            }
          }}
          onImported={(record) => {
            setImporting(false);
            setPrepared(null);
            setTemplateId('');
            router.replace(`/conversions?statementId=${record.id}&step=model`, { scroll: false });
          }}
        />
      ) : (
        <>
          {!selectedId && (
            <section className="conversion-hero">
              <div className="conversion-hero-copy">
                <p className="conversion-eyebrow">
                  <span /> CONVERSION & EXPORT
                </p>
                <h1>Convertissez vos relevés en toute simplicité.</h1>
                <p>Un PDF, un modèle et un résultat contrôlé avant le téléchargement.</p>
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
                    Vérifier
                  </span>
                  <ArrowRight size={14} />
                  <span>
                    <FileOutput size={15} />
                    Télécharger
                  </span>
                </div>
              </div>
              <div className="conversion-hero-action">
                <Button
                  permission="documents.write"
                  size="lg"
                  onClick={() => {
                    if (confirmDiscard(hasUnsavedChanges())) setImporting(true);
                  }}
                >
                  <Plus size={17} />
                  Importer un relevé
                </Button>
                <span>Relevés bancaires PDF · jusqu’à 20 Mo</span>
              </div>
              <ArrowRightLeft className="conversion-hero-art" aria-hidden="true" />
            </section>
          )}
          <div className="conversion-studio conversion-studio-full">
            <div className="conversion-content">
              {selectedId ? (
                detail.loading ? (
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
                  (flowStep === 'model' ? (
                    <StatementModelStep
                      detail={currentDetail}
                      templates={templates.data || []}
                      templateId={templateId}
                      onTemplate={setTemplateId}
                      loading={templates.loading}
                      error={templates.error}
                      onRetry={templates.reload}
                      onImport={() => setImporting(true)}
                      onPrepared={(result) => {
                        setPrepared(result);
                        router.replace(
                          `/conversions?statementId=${result.detail.id}&step=result&templateId=${result.preview.template.id}`,
                          { scroll: false },
                        );
                      }}
                    />
                  ) : (
                    <StatementWorkspace
                      key={`${currentDetail.id}-${currentDetail.version}`}
                      detail={currentDetail}
                      guided={flowStep === 'result'}
                      onImport={() => {
                        if (confirmDiscard(hasUnsavedChanges())) setImporting(true);
                      }}
                      initialTab={savedTab?.id === currentDetail.id ? savedTab.tab : undefined}
                      initialPreview={
                        prepared?.detail.version === currentDetail.version
                          ? prepared.preview
                          : undefined
                      }
                      onChooseTemplate={() => {
                        if (confirmDiscard(hasUnsavedChanges()))
                          router.replace(
                            `/conversions?statementId=${currentDetail.id}&step=model`,
                            { scroll: false },
                          );
                      }}
                      templates={templates.data || []}
                      templateId={templateId}
                      onTemplate={setTemplateId}
                      onTemplatesChanged={(id) => {
                        setTemplateId(id);
                        setPrepared(null);
                        detail.reload();
                        templates.reload();
                        if (flowStep === 'result')
                          router.replace(
                            `/conversions?statementId=${currentDetail.id}&step=result&templateId=${id}`,
                            { scroll: false },
                          );
                      }}
                      onSaved={(tab) => {
                        if (tab) setSavedTab({ id: currentDetail.id, tab });
                        setPrepared(null);
                        detail.reload();
                      }}
                    />
                  ))
                )
              ) : (
                <section className="conversion-empty conversion-start">
                  <div className="conversion-empty-illustration">
                    <span>
                      <FileText size={38} />
                    </span>
                    <ArrowRight size={22} />
                    <span>
                      <FileOutput size={38} />
                    </span>
                  </div>
                  <p className="conversion-eyebrow">VOTRE ESPACE DE CONVERSION</p>
                  <h2>Votre conversion commence ici.</h2>
                  <p>
                    Ajoutez votre relevé bancaire. Vérifiez les mouvements et les soldes avant de
                    les exporter. Retrouvez vos documents dans l’espace clients.
                  </p>
                  <Button permission="documents.write" size="lg" onClick={() => setImporting(true)}>
                    <Plus size={16} />
                    Importer un nouveau relevé
                  </Button>
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
  );
}
