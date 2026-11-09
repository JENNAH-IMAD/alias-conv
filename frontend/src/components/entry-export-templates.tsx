'use client';
import { useState } from 'react';
import Link from 'next/link';
import { Columns3, Copy, FileOutput, Pencil, Plus, Trash2 } from 'lucide-react';
import { request, documentBlob } from '@/services/api';
import { useApi } from '@/services/use-api';
import { defaultEntryOutput, type EntryOutputTemplate } from '@/services/entry-models';
import { Button } from './ui/button';
import { BankDialog } from './bank-dialogs';
import { LoadState } from './ui/load-state';
import { EntryOutputSettings } from './entry-output-settings';
import { chooseExportDestination, saveExportBlob } from '@/lib/save-export';
import { useUnsavedChanges } from '@/lib/use-unsaved-changes';
import '@/styles/entry-models.css';

export function EntryExportTemplates() {
  const templates = useApi<EntryOutputTemplate[]>('/accounting/entry-output-templates');
  const [editing, setEditing] = useState<EntryOutputTemplate | null>(null);
  const [removing, setRemoving] = useState<EntryOutputTemplate | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const empty = (): EntryOutputTemplate => ({
    id: '',
    name: '',
    description: '',
    version: 0,
    active: true,
    output: defaultEntryOutput(),
  });
  async function save(value: EntryOutputTemplate) {
    setBusy(true);
    setError('');
    try {
      await request(`/accounting/entry-output-templates${value.id ? '/' + value.id : ''}`, {
        method: value.id ? 'PUT' : 'POST',
        body: JSON.stringify(value),
      });
      setEditing(null);
      setNotice('Modèle d’exportation des écritures enregistré.');
      templates.reload();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function remove() {
    if (!removing) return;
    setBusy(true);
    setError('');
    try {
      await request(
        `/accounting/entry-output-templates/${removing.id}?version=${removing.version}`,
        { method: 'DELETE' },
      );
      setRemoving(null);
      setNotice('Modèle d’exportation supprimé. Les résultats archivés sont conservés.');
      templates.reload();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function structure(item: EntryOutputTemplate) {
    setBusy(true);
    setError('');
    try {
      const extension = item.output.fileExtension || 'txt';
      const destination = await chooseExportDestination(`modele_ecritures.${extension}`, extension);
      await saveExportBlob(
        await documentBlob(`/accounting/entry-output-templates/${item.id}/file`),
        destination,
      );
    } catch (e) {
      if ((e as Error).name !== 'AbortError') setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="space-y-5" aria-label="Modèles d’exportation des écritures comptables">
      <div className="entry-model-toolbar">
        <div>
          <h2 className="text-xl font-semibold">Formats des écritures comptables</h2>
          <p className="muted text-sm mt-2">
            Définissez les colonnes et le fichier à télécharger après le calcul de la TVA.
          </p>
        </div>
        <Button
          permission="templates.write"
          onClick={() => {
            setError('');
            setEditing(empty());
          }}
        >
          <Plus size={16} /> Nouveau format d’écritures
        </Button>
      </div>
      <LoadState loading={templates.loading} error={templates.error} />
      {error && !editing && !removing && (
        <p className="conversion-alert error" role="alert">
          {error}
        </p>
      )}
      {notice && (
        <p className="ui-card" role="status">
          {notice}
        </p>
      )}
      <div className="entry-model-grid">
        {templates.data?.map((item) => (
          <article key={item.id} className="ui-card entry-model-card">
            <div className="entry-model-card-heading">
              <span className="statement-card-icon">
                <FileOutput size={24} />
              </span>
              <span className={item.active ? 'status-positive' : 'ui-tag'}>
                {item.active ? 'Actif' : 'Inactif'}
              </span>
            </div>
            <h3 className="text-lg font-semibold">{item.name}</h3>
            <p className="muted text-sm">{item.description}</p>
            <p className="text-sm font-medium text-primary">
              .{item.output.fileExtension || 'txt'} · {item.output.encoding} · Séparateur :{' '}
              {item.output.delimiter === '\t' ? 'Tabulation' : item.output.delimiter} ·{' '}
              {item.output.includeHeader ? 'Avec en-têtes' : 'Sans en-têtes'}
            </p>
            <div className="rounded-xl border border-border bg-muted/40 p-4">
              <p className="flex items-center gap-2 text-sm font-medium mb-3">
                <Columns3 size={16} />
                {item.output.columns.filter((c) => c.export).length} colonnes exportées
              </p>
              <div className="entry-model-keywords">
                {item.output.columns
                  .filter((c) => c.export)
                  .map((c) => (
                    <span key={c.field}>{c.label}</span>
                  ))}
              </div>
            </div>
            <div className="entry-model-actions">
              <Button
                permission="templates.write"
                variant="outline"
                size="sm"
                onClick={() => {
                  setError('');
                  setEditing(item);
                }}
              >
                <Pencil size={15} /> Paramétrer
              </Button>
              <Button
                permission="templates.write"
                variant="outline"
                size="sm"
                onClick={() => {
                  setError('');
                  setEditing({ ...item, id: '', version: 0, name: item.name + ' — copie' });
                }}
              >
                <Copy size={15} /> Dupliquer
              </Button>
              <Button
                permission="documents.export"
                variant="outline"
                size="sm"
                disabled={busy}
                onClick={() => void structure(item)}
              >
                Télécharger le modèle
              </Button>
              <Button
                permission="templates.write"
                variant="danger"
                size="sm"
                onClick={() => {
                  setError('');
                  setRemoving(item);
                }}
              >
                <Trash2 size={15} /> Supprimer
              </Button>
            </div>
            <Link
              className="text-sm text-primary underline"
              href={`/entry-models?outputTemplateId=${item.id}`}
            >
              Utiliser ce format pour une écriture du client
            </Link>
          </article>
        ))}
      </div>
      {!templates.loading && !templates.error && !templates.data?.length && (
        <p className="ui-card muted">
          Créez un format d’écritures : les cinq colonnes demandées sont proposées automatiquement.
        </p>
      )}
      {editing && (
        <EntryExportTemplateEditor
          key={editing.id || 'new'}
          initial={editing}
          busy={busy}
          error={error}
          onClose={() => setEditing(null)}
          onSave={save}
        />
      )}
      {removing && (
        <BankDialog
          title="Supprimer le format d’écritures"
          busy={busy}
          onClose={() => setRemoving(null)}
        >
          <p>
            Supprimer « {removing.name} » ? Un format utilisé par une écriture doit d’abord être
            remplacé dans celle-ci.
          </p>
          {error && (
            <p className="conversion-alert error mt-4" role="alert">
              {error}
            </p>
          )}
          <div className="entry-model-actions mt-5">
            <Button variant="outline" disabled={busy} onClick={() => setRemoving(null)}>
              Annuler
            </Button>
            <Button variant="danger" disabled={busy} onClick={() => void remove()}>
              Supprimer le format
            </Button>
          </div>
        </BankDialog>
      )}
    </section>
  );
}

function EntryExportTemplateEditor({
  initial,
  busy,
  error,
  onClose,
  onSave,
}: {
  initial: EntryOutputTemplate;
  busy: boolean;
  error: string;
  onClose: () => void;
  onSave: (value: EntryOutputTemplate) => Promise<void>;
}) {
  const [draft, setDraft] = useState(initial);
  useUnsavedChanges(JSON.stringify(draft) !== JSON.stringify(initial));
  return (
    <BankDialog
      wide
      title={
        initial.id
          ? 'Paramétrer le format d’écritures'
          : 'Nouveau format d’exportation des écritures'
      }
      busy={busy}
      onClose={onClose}
    >
      <form
        className="entry-model-form"
        onSubmit={(e) => {
          e.preventDefault();
          void onSave(draft);
        }}
      >
        <fieldset disabled={busy}>
          <label>
            Nom du modèle
            <input
              className="ui-input mt-2"
              required
              maxLength={100}
              value={draft.name}
              onChange={(e) => setDraft({ ...draft, name: e.target.value })}
            />
          </label>
          <label>
            Description
            <textarea
              className="ui-input mt-2"
              maxLength={1000}
              value={draft.description}
              onChange={(e) => setDraft({ ...draft, description: e.target.value })}
            />
          </label>
        </fieldset>
        <EntryOutputSettings
          value={draft.output}
          disabled={busy}
          onChange={(output) => setDraft({ ...draft, output })}
        />
        <label className="entry-model-check">
          <input
            type="checkbox"
            checked={draft.active}
            disabled={busy}
            onChange={(e) => setDraft({ ...draft, active: e.target.checked })}
          />{' '}
          Format actif
        </label>
        {error && (
          <p className="conversion-alert error" role="alert">
            {error}
          </p>
        )}
        <div className="entry-model-actions">
          <Button type="button" variant="outline" disabled={busy} onClick={onClose}>
            Annuler
          </Button>
          <Button
            type="submit"
            disabled={
              busy ||
              !draft.output.columns.some((c) => c.display) ||
              !draft.output.columns.some((c) => c.export)
            }
          >
            Enregistrer le format
          </Button>
        </div>
      </form>
    </BankDialog>
  );
}
