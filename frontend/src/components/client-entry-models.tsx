'use client';
import { useState } from 'react';
import { useSearchParams } from 'next/navigation';
import Link from 'next/link';
import {
  BookOpen,
  Plus,
  Pencil,
  Trash2,
  Search,
  ArrowRight,
  Download,
  Landmark,
  ScanText,
} from 'lucide-react';
import { PageHeader } from './page-header';
import { Button } from './ui/button';
import { BankDialog } from './bank-dialogs';
import { LoadState } from './ui/load-state';
import { useApi, useAll } from '@/services/use-api';
import { request, documentBlob, type Account, type Client } from '@/services/api';
import {
  defaultEntryOutput,
  type EntryModel,
  type EntryModelDossier,
  type EntryOutputTemplate,
} from '@/services/entry-models';
import { createUuid } from '@/lib/create-uuid';
import { useUnsavedChanges } from '@/lib/use-unsaved-changes';
import { chooseExportDestination, saveExportBlob } from '@/lib/save-export';
import '@/styles/entry-models.css';
import '@/styles/model-catalogs.css';

export function ClientEntryModels({
  embedded = false,
  initialClientId,
}: {
  embedded?: boolean;
  initialClientId?: string;
}) {
  const params = useSearchParams();
  const [clientId, setClientId] = useState(initialClientId ?? params.get('clientId') ?? '');
  const clients = useAll<Client>('/clients');
  const accounts = useAll<Account>('/bank-accounts');
  const formats = useApi<EntryOutputTemplate[]>('/accounting/entry-output-templates');
  const models = useApi<EntryModelDossier>(
    clientId ? `/accounting/entry-models/${clientId}` : null,
  );
  const [editing, setEditing] = useState<EntryModel | null>(null);
  const [removing, setRemoving] = useState<EntryModel | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [query, setQuery] = useState('');
  const clientAccounts = (accounts.data || []).filter((a) => a.clientId === clientId);
  const entries = models.data?.models || [];
  const visible = entries.filter((m) =>
    [m.name, m.abbreviation, m.accountCode, ...m.keywords]
      .join(' ')
      .toLocaleLowerCase('fr')
      .includes(query.toLocaleLowerCase('fr')),
  );
  const loading = clients.loading || accounts.loading || models.loading || formats.loading;
  const failure = error || clients.error || accounts.error || models.error || formats.error;
  function reload() {
    models.reload();
    accounts.reload();
    formats.reload();
    setError('');
  }
  async function save(next: EntryModel[]) {
    if (!models.data || busy) return;
    setBusy(true);
    setError('');
    setNotice('');
    try {
      await request(`/accounting/entry-models/${clientId}`, {
        method: 'PUT',
        body: JSON.stringify({ version: models.data.version, models: next }),
      });
      setEditing(null);
      setRemoving(null);
      reload();
      setNotice('Modèles d’écritures enregistrés dans le dossier client.');
    } catch (e) {
      setError((e as Error).message);
      throw e;
    } finally {
      setBusy(false);
    }
  }
  async function downloadModel(m: EntryModel) {
    setBusy(true);
    setError('');
    try {
      const extension = m.output?.fileExtension || 'txt';
      const destination = await chooseExportDestination(
        `modele_${m.abbreviation}.${extension}`,
        extension,
      );
      await saveExportBlob(
        await documentBlob(`/accounting/entry-models/${clientId}/${m.id}/file`),
        destination,
      );
    } catch (e) {
      if ((e as Error).name !== 'AbortError') setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  const createButton = (
    <Button
      permission="documents.write"
      disabled={!clientId || loading || !models.data}
      onClick={() => {
        setError('');
        const format =
          formats.data?.find((t) => t.active && t.id === params.get('outputTemplateId')) ||
          formats.data?.find((t) => t.active && t.id === '9d2e4a78-fb36-4820-9c04-87b370743915') ||
          formats.data?.find((t) => t.active);
        setEditing({
          id: createUuid(),
          name: '',
          abbreviation: '',
          keywords: [],
          bankAccountId: '',
          accountCode: '',
          exportTemplateId: '00000000-0000-0000-0000-000000000000',
          taxRate: 0,
          taxAccountCode: '',
          active: true,
          bankAccountBased: true,
          output: format?.output || defaultEntryOutput(),
          outputTemplateId: format?.id || null,
        });
      }}
    >
      <Plus size={17} /> Nouveau modèle
    </Button>
  );
  return (
    <div
      className={embedded ? 'entry-models-page entry-models-embedded' : 'ui-page entry-models-page'}
    >
      {embedded ? (
        <div className="entry-model-toolbar">
          <div>
            <h3 className="font-semibold">Modèles d’écritures de l’entreprise</h3>
            <p className="muted text-sm mt-2">Règles de classement et de TVA du dossier client.</p>
          </div>
          {createButton}
        </div>
      ) : (
        <PageHeader
          icon={BookOpen}
          eyebrow="Règles de traitement"
          title="Modèles d’écritures par client"
          description="Un client, une banque, des mots à reconnaître et un taux de TVA."
          actions={createButton}
        />
      )}
      {(!embedded || !initialClientId) && (
        <section className={embedded ? 'entry-model-context' : 'ui-card entry-model-context'}>
          <label>
            Dossier client
            <select
              aria-label="Client des modèles d’écritures"
              className="ui-input mt-2"
              value={clientId}
              disabled={busy || clients.loading}
              onChange={(e) => {
                setClientId(e.target.value);
                setQuery('');
                setError('');
                setNotice('');
              }}
            >
              <option value="">Choisir un client…</option>
              {clients.data?.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </label>
          {!embedded && (
            <Button variant="outline" asChild>
              <Link
                href={`/client-workspace?section=statements${clientId ? '&clientId=' + clientId : ''}`}
              >
                Ouvrir l’espace client
              </Link>
            </Button>
          )}
        </section>
      )}
      <LoadState loading={loading} error="" />
      {failure && !editing && !removing && (
        <div className="conversion-alert error" role="alert">
          {failure}
          <Button variant="outline" onClick={reload}>
            Réessayer
          </Button>
        </div>
      )}
      {notice && (
        <p className="ui-card" role="status">
          {notice}
        </p>
      )}
      {!clientId ? (
        <section className="ui-card entry-model-empty">
          <span className="model-empty-icon">
            <BookOpen size={26} aria-hidden="true" />
          </span>
          <h2>Retrouvez les modèles d’un client</h2>
          <p>
            Sélectionnez son dossier ci-dessus pour consulter ses règles ou créer un modèle
            d’écritures.
          </p>
        </section>
      ) : (
        !loading &&
        models.data && (
          <>
            <div className="entry-model-toolbar">
              <label className="entry-model-search">
                <Search size={17} />
                <input
                  className="ui-input"
                  aria-label="Rechercher un modèle"
                  placeholder="Abréviation, intitulé, compte ou mot…"
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                />
              </label>
              <span className="muted text-sm">{visible.length} modèle(s)</span>
            </div>
            {!visible.length && (
              <section className="ui-card entry-model-empty">
                <BookOpen size={28} />
                <h3>{query ? 'Aucun modèle trouvé' : 'Aucun modèle configuré pour ce client'}</h3>
                <p>
                  Créez un modèle avec ses champs, ses mots de reconnaissance et ses paramètres TXT.
                </p>
              </section>
            )}
            <div className="entry-model-grid">
              {visible.map((m) => {
                const bank = clientAccounts.find((a) => a.id === m.bankAccountId);
                const output = m.output ?? defaultEntryOutput();
                return (
                  <article key={m.id} className="ui-card entry-model-card">
                    <div className="entry-model-card-heading">
                      <span className="entry-model-code">{m.abbreviation}</span>
                      <span className={m.active ? 'status-positive' : 'ui-tag'}>
                        {m.active ? 'Actif' : 'Inactif'}
                      </span>
                    </div>
                    <h2>{m.name}</h2>
                    <p className="muted text-sm">
                      {bank?.bank || 'Banque indisponible'} · {bank?.accountName}
                    </p>
                    <dl>
                      <div>
                        <dt>N° de compte</dt>
                        <dd>{m.accountCode}</dd>
                      </div>
                      <div>
                        <dt>TVA sur TTC</dt>
                        <dd>{m.taxRate} %</dd>
                      </div>
                      <div>
                        <dt>Compte bancaire</dt>
                        <dd>{bank?.accountNumber || 'À vérifier'}</dd>
                      </div>
                      <div>
                        <dt>Fichier d’exportation</dt>
                        <dd>
                          .{output.fileExtension || 'txt'} · {output.encoding} ·{' '}
                          {output.columns.filter((c) => c.export).length} colonnes
                        </dd>
                      </div>
                    </dl>
                    <div className="entry-model-keywords">
                      {output.columns
                        .filter((c) => c.display)
                        .map((c) => (
                          <span key={c.field}>{c.label}</span>
                        ))}
                    </div>
                    <p className="muted text-sm">Mots : {m.keywords.join(', ')}</p>
                    <div className="entry-model-actions">
                      <Button permission="documents.write" size="sm" asChild>
                        <Link
                          href={`/conversions?import=1&type=accounting&clientId=${clientId}&entryBankAccountId=${m.bankAccountId}&entryAbbreviation=${encodeURIComponent(m.abbreviation)}&entryModelId=${m.id}`}
                        >
                          <ArrowRight size={15} /> Utiliser ce modèle
                        </Link>
                      </Button>
                      <Button
                        permission="documents.export"
                        size="sm"
                        variant="outline"
                        disabled={busy}
                        onClick={() => void downloadModel(m)}
                      >
                        <Download size={15} /> Modèle{' '}
                        {(output.fileExtension || 'txt').toUpperCase()}
                      </Button>
                      <Button
                        permission="documents.write"
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setError('');
                          setEditing({ ...m, output });
                        }}
                      >
                        <Pencil size={15} /> Modifier
                      </Button>
                      <Button
                        permission="documents.write"
                        size="sm"
                        variant="danger"
                        onClick={() => {
                          setError('');
                          setRemoving(m);
                        }}
                      >
                        <Trash2 size={15} /> Supprimer
                      </Button>
                    </div>
                  </article>
                );
              })}
            </div>
          </>
        )
      )}
      {editing && (
        <EntryModelEditor
          key={editing.id}
          initial={editing}
          accounts={clientAccounts}
          formats={formats.data || []}
          busy={busy || models.loading}
          error={error}
          onClose={() => setEditing(null)}
          onReload={reload}
          onSave={(m) =>
            save(
              entries.some((old) => old.id === m.id)
                ? entries.map((old) => (old.id === m.id ? m : old))
                : [...entries, m],
            )
          }
        />
      )}
      {removing && (
        <BankDialog title="Supprimer le modèle" onClose={() => setRemoving(null)} busy={busy}>
          <p>
            Supprimer « {removing.name} » ? Les résultats archivés et leur modèle historique seront
            conservés.
          </p>
          {error && (
            <p className="conversion-alert error" role="alert">
              {error}
            </p>
          )}
          <div className="entry-model-actions">
            <Button variant="outline" disabled={busy} onClick={() => setRemoving(null)}>
              Annuler
            </Button>
            <Button
              variant="danger"
              disabled={busy}
              onClick={() => void save(entries.filter((m) => m.id !== removing.id)).catch(() => {})}
            >
              Supprimer le modèle
            </Button>
          </div>
        </BankDialog>
      )}
    </div>
  );
}
function EntryModelEditor({
  initial,
  accounts,

  formats,
  busy,
  error,
  onClose,
  onSave,
  onReload,
}: {
  initial: EntryModel;
  accounts: Account[];

  formats: EntryOutputTemplate[];
  busy: boolean;
  error: string;
  onClose: () => void;
  onSave: (m: EntryModel) => Promise<void>;
  onReload: () => void;
}) {
  const initialAccount =
    accounts.find((a) => a.id === initial.bankAccountId) ||
    (!initial.bankAccountId && accounts.length === 1 ? accounts[0] : undefined);
  const initialDraft = {
    ...initial,
    bankAccountId: initialAccount?.id || '',
    accountCode: initialAccount?.accountCode || '',
    name: initial.name || initialAccount?.accountName || initialAccount?.bank || '',
    exportTemplateId: '00000000-0000-0000-0000-000000000000',
    taxAccountCode: '',
    bankAccountBased: true,
    output: initial.output ?? defaultEntryOutput(),
  };
  const [draft, setDraft] = useState(initialDraft);
  const [words, setWords] = useState(initial.keywords.join('\n'));
  const [bankFilter, setBankFilter] = useState(
    initialAccount?.bankId ||
      (new Set(accounts.map((a) => a.bankId)).size === 1 ? accounts[0]?.bankId : '') ||
      '',
  );
  const selected = accounts.find((a) => a.id === draft.bankAccountId);
  const bankAccounts = accounts.filter((a) => a.bankId === bankFilter);
  function selectAccount(account: Account | undefined) {
    setDraft({
      ...draft,
      bankAccountId: account?.id || '',
      accountCode: account?.accountCode || '',
      name: account?.accountName || account?.bank || '',
      taxAccountCode: '',
      bankAccountBased: true,
    });
  }
  useUnsavedChanges(
    JSON.stringify(draft) !== JSON.stringify(initialDraft) || words !== initial.keywords.join('\n'),
  );
  return (
    <BankDialog
      title={initial.name ? 'Modifier le modèle d’écritures' : 'Nouveau modèle d’écritures'}
      onClose={onClose}
      busy={busy}
      wide
      className="entry-rule-dialog"
      closeButtonLabel="Fermer le formulaire"
    >
      <form
        className="entry-model-form"
        onSubmit={(e) => {
          e.preventDefault();
          void onSave({
            ...draft,
            taxAccountCode: '',
            name: draft.name.trim(),
            abbreviation: draft.abbreviation.trim().toUpperCase(),
            keywords: [
              ...new Set(
                words
                  .split(/\r?\n/)
                  .map((w) => w.trim())
                  .filter(Boolean),
              ),
            ],
          }).catch(() => {});
        }}
      >
        <p className="entry-rule-intro">
          Associez une banque, puis définissez la reconnaissance des mouvements.
        </p>
        <fieldset className="entry-rule-bank" disabled={busy}>
          <legend>
            <Landmark size={16} aria-hidden="true" /> Banque et compte
          </legend>
          <div className="entry-rule-bank-choice">
            <label>
              Banque
              <select
                required
                aria-label="Banque"
                autoFocus
                className="ui-input mt-2"
                value={bankFilter}
                onChange={(e) => {
                  const matches = accounts.filter((a) => a.bankId === e.target.value);
                  setBankFilter(e.target.value);
                  selectAccount(matches.length === 1 ? matches[0] : undefined);
                }}
              >
                <option value="">Choisir une banque du client…</option>
                {[...new Map(accounts.map((a) => [a.bankId, a])).values()].map((a) => (
                  <option key={a.bankId} value={a.bankId}>
                    {a.bank}
                  </option>
                ))}
              </select>
            </label>
            {bankAccounts.length > 1 && (
              <label>
                Compte bancaire du client
                <select
                  required
                  aria-label="Compte bancaire du client"
                  className="ui-input mt-2"
                  value={draft.bankAccountId}
                  onChange={(e) => selectAccount(accounts.find((a) => a.id === e.target.value))}
                >
                  <option value="">Choisir un compte…</option>
                  {bankAccounts.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.accountName} · {a.accountNumber}
                    </option>
                  ))}
                </select>
              </label>
            )}
          </div>
          <div className="entry-model-fields entry-rule-account-fields">
            <label>
              N° de compte
              <input
                required
                readOnly
                className="ui-input mt-2"
                value={draft.accountCode}
                placeholder="Renseigné automatiquement"
              />
            </label>
            <label>
              Intitulé
              <input
                required
                disabled={!selected}
                maxLength={100}
                className="ui-input mt-2"
                value={draft.name}
                onChange={(e) => setDraft({ ...draft, name: e.target.value })}
                placeholder="Intitulé du compte"
              />
            </label>
          </div>
          {selected && bankAccounts.length === 1 && (
            <p className="entry-rule-account-note">
              <span>Compte bancaire</span>
              <strong>{selected.accountNumber}</strong>
              <span className="ui-tag">{selected.currency}</span>
            </p>
          )}
        </fieldset>
        <fieldset className="entry-rule-recognition" disabled={busy || !selected}>
          <legend>
            <ScanText size={16} aria-hidden="true" /> Reconnaissance des mouvements
          </legend>
          <div className="entry-model-fields">
            <label>
              Abréviation
              <input
                required
                maxLength={16}
                pattern="(?:[A-Z0-9_]|-)+"
                className="ui-input mt-2"
                placeholder="Ex. ACH"
                value={draft.abbreviation}
                onChange={(e) => setDraft({ ...draft, abbreviation: e.target.value.toUpperCase() })}
              />
            </label>
            <label>
              Taux de taxe
              <select
                aria-label="Taux de taxe"
                className="ui-input mt-2"
                value={String(draft.taxRate)}
                onChange={(e) =>
                  setDraft({ ...draft, taxRate: Number(e.target.value), taxAccountCode: '' })
                }
              >
                {[0, 7, 10, 14, 20].map((r) => (
                  <option key={r} value={r}>
                    {r} %
                  </option>
                ))}
              </select>
              <span className="entry-model-field-help">TVA calculée sur le TTC.</span>
            </label>
          </div>
          <label>
            Mots originaux
            <textarea
              required
              aria-label="Mots originaux"
              className="ui-input mt-2"
              rows={2}
              value={words}
              onChange={(e) => setWords(e.target.value)}
              placeholder={'ACHAT\nPAIEMENT CARTE\nFOURNISSEUR'}
            />
            <span className="entry-model-field-help">
              Une expression par ligne pour retrouver les mouvements du relevé.
            </span>
          </label>
        </fieldset>
        <div className="entry-rule-options">
          <label>
            Format du résultat
            <select
              aria-label="Format du résultat"
              className="ui-input mt-2"
              disabled={busy}
              value={draft.outputTemplateId || ''}
              onChange={(e) => {
                const format = formats.find((t) => t.id === e.target.value);
                setDraft({
                  ...draft,
                  outputTemplateId: format?.id || null,
                  output: format?.output || draft.output,
                });
              }}
            >
              <option value="">Réglages enregistrés dans l’écriture</option>
              {formats
                .filter((t) => t.active || t.id === draft.outputTemplateId)
                .map((t) => (
                  <option key={t.id} value={t.id} disabled={!t.active}>
                    {t.name}
                    {!t.active ? ' — Inactif' : ''}
                  </option>
                ))}
            </select>
          </label>
          <label className="entry-model-check">
            <input
              type="checkbox"
              disabled={busy}
              checked={draft.active}
              onChange={(e) => setDraft({ ...draft, active: e.target.checked })}
            />
            Modèle actif
          </label>
        </div>
        {(!draft.output.columns.some((c) => c.display) ||
          !draft.output.columns.some((c) => c.export)) && (
          <p className="conversion-alert warning" role="status">
            Choisissez un format avec des colonnes d’affichage et d’export pour enregistrer.
          </p>
        )}
        {error && (
          <p role="alert" className="conversion-alert error">
            {error}{' '}
            <Button type="button" variant="outline" disabled={busy} onClick={onReload}>
              Recharger le dossier
            </Button>
          </p>
        )}
        {selected && !draft.accountCode && (
          <p className="conversion-alert warning">
            Renseignez le compte comptable de cette banque dans « Banques et comptes », puis
            rechargez le dossier.
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
              !draft.bankAccountId ||
              !draft.accountCode ||
              !draft.output.columns.some((c) => c.display) ||
              !draft.output.columns.some((c) => c.export)
            }
          >
            {busy ? 'Enregistrement…' : 'Enregistrer le modèle'}
          </Button>
        </div>
      </form>
    </BankDialog>
  );
}
