'use client';
import { Suspense, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { EntryExportTemplates } from '@/components/entry-export-templates';
import { Button } from '@/components/ui/button';
import { BankDialog } from '@/components/bank-dialogs';
import { ExportTemplateEditor } from '@/components/export-template-editor';
import { pageTheme as theme } from '@/components/page-theme';
import { ExportTemplate, Page, request } from '@/services/api';
import { useApi } from '@/services/use-api';
import { Copy, FileOutput, Pencil, Plus, Trash2, Columns3 } from 'lucide-react';
import { PageHeader } from '@/components/page-header';
import { LoadState } from '@/components/ui/load-state';
import { Pagination } from '@/components/ui/pagination';
import { exportTypeLabel } from '@/services/export-template-defaults';
import '@/styles/model-catalogs.css';

export default function TemplatesPage() {
  return (
    <Suspense fallback={<p role="status">Chargement des modèles…</p>}>
      <TemplateSections />
    </Suspense>
  );
}
function TemplateSections() {
  const params = useSearchParams();
  const router = useRouter();
  const entries = params.get('section') === 'entries';
  return (
    <div className={`${theme.page} templates-page`}>
      <nav className="ui-card model-section-switcher" aria-label="Types de modèles d’exportation">
        <Button
          variant={!entries ? 'default' : 'outline'}
          aria-pressed={!entries}
          onClick={() => router.replace('/templates')}
        >
          Relevés et extraits bancaires
        </Button>
        <Button
          variant={entries ? 'default' : 'outline'}
          aria-pressed={entries}
          onClick={() => router.replace('/templates?section=entries')}
        >
          Écritures comptables
        </Button>
      </nav>
      {entries ? (
        <>
          <PageHeader
            icon={FileOutput}
            eyebrow="Modèles d’exportation · Écritures comptables"
            title="Formats d’écritures comptables"
            description="Définissez les colonnes, le séparateur et l’encodage des fichiers de vos clients."
          />
          <EntryExportTemplates />
        </>
      ) : (
        <LegacyTemplatesPage />
      )}
    </div>
  );
}
function LegacyTemplatesPage() {
  const [page, setPage] = useState(1);
  const { data, loading, error, reload } = useApi<Page<ExportTemplate>>(
    `/export-templates?page=${page}&pageSize=20`,
  );
  const [editing, setEditing] = useState<ExportTemplate | null | undefined>(undefined);
  const [duplicate, setDuplicate] = useState(false);
  const [defaultType, setDefaultType] = useState<ExportTemplate['type']>('CUSTOMCSV');
  const [deleting, setDeleting] = useState<ExportTemplate | null>(null);
  const [busy, setBusy] = useState(false);
  const [deleteError, setDeleteError] = useState('');
  const [notice, setNotice] = useState('');
  async function remove() {
    if (!deleting || busy) return;
    setBusy(true);
    setDeleteError('');
    try {
      await request(`/export-templates/${deleting.id}`, { method: 'DELETE' });
      setDeleting(null);
      setNotice('Modèle supprimé.');
      if (data?.items.length === 1 && page > 1) setPage(page - 1);
      else reload();
    } catch (e) {
      setDeleteError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="model-catalog-content">
      {editing !== undefined && (
        <ExportTemplateEditor
          initial={editing}
          duplicate={duplicate}
          defaultType={defaultType}
          onClose={() => setEditing(undefined)}
          onSaved={() => {
            setNotice('Modèle enregistré.');
            reload();
          }}
        />
      )}
      {deleting && (
        <BankDialog title="Supprimer le modèle" onClose={() => setDeleting(null)} busy={busy}>
          <p>Supprimer le modèle « {deleting.name} » et ses colonnes ?</p>
          {deleteError && (
            <p role="alert" className="mt-3 text-destructive">
              {deleteError}
            </p>
          )}
          <div className="mt-6 flex justify-end gap-3">
            <Button
              permission="templates.write"
              variant="outline"
              disabled={busy}
              onClick={() => setDeleting(null)}
            >
              Annuler
            </Button>
            <Button disabled={busy} onClick={remove}>
              Supprimer définitivement
            </Button>
          </div>
        </BankDialog>
      )}
      <PageHeader
        icon={FileOutput}
        eyebrow="Formats de fichiers"
        title="Modèles d’exportation"
        description="Gérez les formats CSV et Sage, leurs colonnes et leurs réglages."
        actions={
          <Button
            permission="templates.write"
            onClick={() => {
              setDuplicate(false);
              setDefaultType('CUSTOMCSV');
              setEditing(null);
            }}
          >
            <Plus size={17} /> Ajouter un modèle
          </Button>
        }
      />
      {notice && (
        <p role="status" className="rounded-xl bg-success/10 p-4">
          {notice}
        </p>
      )}
      <LoadState loading={loading} error="" />
      {error && (
        <p role="alert">
          {error}{' '}
          <Button variant="outline" onClick={reload}>
            Réessayer
          </Button>
        </p>
      )}
      {!loading && !error && data?.items.length === 0 && (
        <section className={theme.card}>Aucun modèle enregistré.</section>
      )}
      <section aria-label="Liste des modèles" className="catalog-grid template-grid">
        {data?.items.map((model) => (
          <article key={model.id} className={`${theme.card} template-card`}>
            <div className="template-card-top">
              <span className="template-format-icon">
                <FileOutput size={23} />
              </span>
              <span className={model.isActive ? 'status-positive' : 'ui-tag'}>
                {model.isActive ? 'Actif' : 'Inactif'}
              </span>
            </div>
            <div className="flex items-start justify-between gap-3">
              <h2 className="text-lg font-semibold">{model.name}</h2>
            </div>
            <p className="template-format-meta">
              {exportTypeLabel(model.type)} · .{model.fileExtension} · {model.encoding}
            </p>
            <p className="template-description">
              {model.description || 'Format personnalisé prêt à être configuré.'}
            </p>
            <div className="template-field-summary">
              <strong>
                <Columns3 size={15} />
                {model.fields.length} colonne{model.fields.length > 1 ? 's' : ''}
              </strong>
              <div>
                {model.fields
                  .slice()
                  .sort((a, b) => a.position - b.position)
                  .slice(0, 5)
                  .map((field) => (
                    <span key={field.position}>{field.fieldName}</span>
                  ))}
                {model.fields.length > 5 && <span>+{model.fields.length - 5} autres</span>}
              </div>
            </div>
            <div className="catalog-card-actions">
              <Button
                permission="templates.write"
                variant="outline"
                onClick={() => {
                  setDuplicate(false);
                  setEditing(model);
                }}
              >
                <Pencil size={15} /> Modifier
              </Button>
              <Button
                permission="templates.write"
                variant="outline"
                onClick={() => {
                  setDuplicate(true);
                  setEditing(model);
                }}
              >
                <Copy size={15} /> Dupliquer
              </Button>
              <Button
                permission="templates.write"
                variant="outline"
                size="icon"
                aria-label={`Supprimer ${model.name}`}
                title="Supprimer le modèle"
                className="danger-button"
                onClick={() => {
                  setDeleteError('');
                  setDeleting(model);
                }}
              >
                <Trash2 size={16} />
              </Button>
            </div>
          </article>
        ))}
      </section>
      {data && (
        <Pagination
          label="Pagination des modèles"
          itemLabel="modèle(s)"
          page={page}
          total={data.total}
          loading={loading}
          onChange={setPage}
        />
      )}
    </div>
  );
}
