'use client';
import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import {
  Download,
  Archive,
  History,
  FileText,
  Plus,
  Eye,
  ArrowUpRight,
  RefreshCw,
  Search,
  Trash2,
} from 'lucide-react';
import { documentBlob, Page, saveBlob, request } from '@/services/api';
import { useAuth } from './auth-context';
import { useApi } from '@/services/use-api';
import { accountSuffix, date } from '@/services/statement-exports';
import { Button } from './ui/button';
import { LoadState } from './ui/load-state';
import { Pagination } from './ui/pagination';
import { ExportArchive } from './export-archive';
import { BankDialog } from './bank-dialogs';
type ArchiveStatement = {
  id: string;
  clientId: string | null;
  clientName: string | null;
  bankAccountId: string | null;
  bankName: string;
  sourceFileName: string | null;
  accountName: string | null;
  statementNumber: string | null;
  currency: string | null;
  accountNumber: string | null;
  periodStart: string | null;
  periodEnd: string | null;
  status: string;
  version: number;
  transactionCount: number;
  createdAt: string;
  updatedAt: string;
  importedByName: string | null;
  modifiedByName: string | null;
  exportCount: number;
  lastExportAt: string | null;
};
export function ClientConversions({
  clientId,
  conversionType = 'STATEMENT',
}: {
  clientId: string;
  conversionType?: 'STATEMENT' | 'EXTRACT';
}) {
  const router = useRouter();
  const documentLabel = conversionType === 'EXTRACT' ? 'extrait' : 'relevé';
  const { can } = useAuth();
  const [selection, setSelection] = useState<ArchiveStatement[]>([]);
  const [validating, setValidating] = useState<ArchiveStatement | null>(null);
  const [deleting, setDeleting] = useState<ArchiveStatement[] | null>(null);
  function select(items: ArchiveStatement[]) {
    setSelectedScope(selectionScope);
    setSelection(items);
  }
  async function removeSelected() {
    if (!deleting || busy) return;
    setBusy('delete');
    setError('');
    try {
      await request('/statement-imports/delete-selection', {
        method: 'POST',
        body: JSON.stringify(deleting.map(({ id, version }) => ({ id, version }))),
      });
      select([]);
      setDeleting(null);
      setArchive(null);
      if (statements.data?.items.length === deleting.length && page > 1) setPage(page - 1);
      else statements.reload();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy('');
    }
  }
  const [page, setPage] = useState(1);
  const [archive, setArchive] = useState<ArchiveStatement | null>(null);
  const [scope, setScope] = useState('all');
  const [status, setStatus] = useState('');
  const [search, setSearch] = useState('');
  const [term, setTerm] = useState('');
  const [busy, setBusy] = useState('');
  const [error, setError] = useState('');
  const [original, setOriginal] = useState<{ name: string; url: string } | null>(null);
  const selectionScope = [page, scope, status, term].join('|');
  const [selectedScope, setSelectedScope] = useState(selectionScope);
  const selected = selectedScope === selectionScope ? selection : [];
  const query = new URLSearchParams({ page: String(page), pageSize: '20' });
  query.set('conversionType', conversionType);
  if (!clientId && scope === 'unassigned') query.set('unassigned', 'true');
  if (status) query.set('status', status);
  if (term) query.set('search', term);
  const statements = useApi<Page<ArchiveStatement>>(
    `/client-workspace/${clientId ? `${clientId}/` : ''}statements?${query}`,
  );
  useEffect(
    () => () => {
      if (original) URL.revokeObjectURL(original.url);
    },
    [original],
  );
  async function file(item: ArchiveStatement, kind: 'preview' | 'original' | 'standard') {
    setBusy(item.id);
    setError('');
    try {
      const blob = await documentBlob(
        `/statement-imports/${item.id}/${kind === 'standard' ? 'standard' : `original${kind === 'original' ? '?download=true' : ''}`}`,
      );
      const name =
        kind === 'standard'
          ? `${(item.sourceFileName ?? 'releve').replace(/\.pdf$/i, '')}_standard.csv`
          : (item.sourceFileName ?? 'releve.pdf');
      if (kind === 'preview') setOriginal({ name, url: URL.createObjectURL(blob) });
      else saveBlob(blob, name);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy('');
    }
  }
  return (
    <div className="statement-archive space-y-5">
      <div className="statement-archive-heading">
        <div>
          <h2 className="font-semibold">
            {conversionType === 'EXTRACT'
              ? 'Extraits bancaires convertis'
              : 'Relevés bancaires & exports'}{' '}
            <span className="ui-tag ml-2">{statements.data?.total ?? '—'}</span>
          </h2>
          <p className="muted text-sm mt-2">
            {conversionType === 'EXTRACT'
              ? 'Vos extraits convertis et leurs fichiers d’export.'
              : 'Vos relevés convertis et leurs fichiers d’export.'}
          </p>
        </div>
        <Button
          permission="documents.write"
          onClick={() =>
            router.push(
              `/conversions?import=1${conversionType === 'EXTRACT' ? '&type=extract' : ''}${clientId ? `&clientId=${clientId}` : ''}`,
            )
          }
        >
          <Plus size={16} />
          {conversionType === 'EXTRACT' ? 'Convertir un extrait bancaire' : 'Importer et convertir'}
        </Button>
      </div>
      <p className="statement-retention-note">
        Conservation : les 5 derniers documents par compte. Au 6e ajout, le plus ancien est supprimé
        définitivement ; son historique est conservé.
      </p>
      <form
        className="ui-card statement-archive-filters"
        onSubmit={(event) => {
          event.preventDefault();
          setTerm(search.trim());
          setPage(1);
        }}
      >
        <label className="statement-archive-search">
          Rechercher un {documentLabel}
          <div className="flex gap-2 mt-2">
            <input
              aria-label={`Rechercher un ${documentLabel}`}
              className="ui-input"
              placeholder="Banque, numéro, PDF ou client…"
              maxLength={150}
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
            <Button
              type="submit"
              size="icon"
              variant="outline"
              aria-label={`Rechercher les ${documentLabel}s`}
            >
              <Search size={17} />
            </Button>
          </div>
        </label>
        {!clientId && (
          <label>
            Classement
            <select
              aria-label="Classement"
              className="ui-input mt-2"
              value={scope}
              onChange={(event) => {
                setScope(event.target.value);
                setPage(1);
              }}
            >
              <option value="all">Tous les {documentLabel}s</option>
              <option value="unassigned">À classer dans un client</option>
            </select>
          </label>
        )}
        <label>
          Statut du {documentLabel}
          <select
            aria-label={`Statut du ${documentLabel}`}
            className="ui-input mt-2"
            value={status}
            onChange={(event) => {
              setStatus(event.target.value);
              setPage(1);
            }}
          >
            <option value="">Tous les statuts</option>
            <option value="NEEDS_REVIEW">À vérifier</option>
            <option value="EXPORTED">Exportés</option>
            <option value="VALIDATED">Validés</option>
          </select>
        </label>
        <Button
          type="button"
          variant="outline"
          disabled={statements.loading}
          onClick={statements.reload}
          aria-label="Actualiser les archives"
        >
          <RefreshCw size={16} />
        </Button>
      </form>
      {!clientId && (
        <p className="muted text-sm">
          Les {documentLabel}s sans client apparaissent aussi ici. Ouvrez un {documentLabel} puis
          utilisez « Classer » pour le rattacher au bon client et à son compte.
        </p>
      )}
      <>
        <LoadState
          loading={statements.loading}
          error={statements.error}
          empty={!statements.data?.items.length}
        />
        {error && (
          <p role="alert" className="conversion-alert error">
            {error}
          </p>
        )}
        {can('documents.write') && !!statements.data?.items.length && (
          <div className="ui-card flex flex-wrap items-center justify-between gap-3">
            <label className="flex items-center gap-2 text-sm font-medium">
              <input
                type="checkbox"
                className="size-4 accent-blue-600"
                disabled={!!busy || statements.loading}
                checked={statements.data.items.every((item) =>
                  selected.some((s) => s.id === item.id),
                )}
                onChange={(event) => select(event.target.checked ? statements.data!.items : [])}
              />
              Tout sélectionner sur cette page
            </label>
            <span className="muted text-sm">
              {selected.length} {documentLabel}(s) sélectionné(s)
            </span>
            <Button
              variant="danger"
              size="sm"
              disabled={!selected.length || !!busy || statements.loading}
              onClick={() => {
                setError('');
                setDeleting(selected);
              }}
            >
              <Trash2 size={14} /> Supprimer la sélection
            </Button>
          </div>
        )}
        <div className="account-statement-groups">
          {Array.from(
            new Set(statements.data?.items.map((item) => item.bankAccountId || 'unassigned')),
          ).map((accountId) => {
            const items = statements.data!.items.filter(
              (item) => (item.bankAccountId || 'unassigned') === accountId,
            );
            const first = items[0];
            return (
              <section key={accountId} className="account-statement-group">
                <header className="account-group-heading">
                  <div>
                    <p className="conversion-eyebrow">{first.clientName || 'À CLASSER'}</p>
                    <h3>
                      {first.accountName ||
                        (conversionType === 'EXTRACT'
                          ? 'Extraits sans compte'
                          : 'Relevés sans compte')}
                    </h3>
                    <p className="muted text-sm">
                      {first.bankName} ·{' '}
                      {first.accountNumber
                        ? accountSuffix(first.accountNumber)
                        : 'Choisissez un dossier client'}
                    </p>
                  </div>
                  <span className="ui-tag">
                    {items.length} {documentLabel}(s)
                    {accountId !== 'unassigned' ? ' · 5 maximum' : ''}
                  </span>
                </header>
                <div className="client-archive-grid">
                  {items.map((item) => (
                    <article key={item.id} className="ui-card client-archive-card">
                      <div className="statement-card-heading">
                        <div className="statement-card-identity">
                          {can('documents.write') && (
                            <input
                              type="checkbox"
                              className="size-4 accent-blue-600"
                              aria-label={`Sélectionner ${item.sourceFileName || item.bankName}`}
                              disabled={!!busy || statements.loading}
                              checked={selected.some((s) => s.id === item.id)}
                              onChange={(event) =>
                                select(
                                  event.target.checked
                                    ? [...selected, item]
                                    : selected.filter((s) => s.id !== item.id),
                                )
                              }
                            />
                          )}
                          <span className="statement-card-icon">
                            <FileText size={22} />
                          </span>
                          <div>
                            <h3>{item.sourceFileName || item.bankName}</h3>
                            <p className="statement-archive-owner">
                              {item.clientName ||
                                (item.clientId
                                  ? 'Client associé'
                                  : 'À classer · aucun client associé')}
                            </p>
                          </div>
                        </div>
                        <span
                          className={item.status === 'VALIDATED' ? 'status-positive' : 'ui-tag'}
                        >
                          {item.status === 'VALIDATED'
                            ? 'Validé'
                            : item.status === 'EXPORTED'
                              ? 'Exporté'
                              : 'À vérifier'}
                        </span>
                      </div>
                      <dl className="statement-card-details">
                        <div>
                          <dt>Banque</dt>
                          <dd>
                            {item.bankName}
                            {item.currency && (
                              <span className="statement-currency">{item.currency}</span>
                            )}
                          </dd>
                        </div>
                        <div>
                          <dt>Référence du relevé</dt>
                          <dd>{item.statementNumber || 'Non renseignée'}</dd>
                        </div>
                        {(item.accountName || item.accountNumber) && (
                          <div className="statement-detail-wide">
                            <dt>Compte bancaire</dt>
                            <dd>
                              {[
                                item.accountName,
                                item.accountNumber ? accountSuffix(item.accountNumber) : null,
                              ]
                                .filter(Boolean)
                                .join(' · ')}
                            </dd>
                          </div>
                        )}
                        <div className="statement-detail-wide">
                          <dt>Période du relevé</dt>
                          <dd>
                            {date(item.periodStart)} — {date(item.periodEnd)}
                          </dd>
                        </div>
                      </dl>
                      <div className="client-archive-meta">
                        <span>
                          <strong>{item.transactionCount.toLocaleString('fr-FR')}</strong>{' '}
                          mouvements
                        </span>
                        <span>
                          Version <strong>{item.version}</strong>
                        </span>
                        <span>
                          <strong>{item.exportCount}</strong> export(s)
                        </span>
                      </div>
                      <dl className="statement-card-audit">
                        <div>
                          <dt>Importé le</dt>
                          <dd>
                            {new Date(item.createdAt).toLocaleString('fr-FR')}
                            {item.importedByName && <span>par {item.importedByName}</span>}
                          </dd>
                        </div>
                        {item.modifiedByName && (
                          <div>
                            <dt>Dernière modification</dt>
                            <dd>
                              {new Date(item.updatedAt).toLocaleString('fr-FR')}
                              <span>par {item.modifiedByName}</span>
                            </dd>
                          </div>
                        )}
                      </dl>
                      <div className="client-archive-actions">
                        {item.status !== 'VALIDATED' && (
                          <Button
                            permission="documents.write"
                            size="sm"
                            variant="outline"
                            disabled={!!busy}
                            onClick={() => {
                              setError('');
                              setValidating(item);
                            }}
                          >
                            Confirmer la vérification
                          </Button>
                        )}
                        <Button
                          permission="documents.write"
                          size="sm"
                          variant="danger"
                          disabled={!!busy || statements.loading}
                          onClick={() => {
                            setError('');
                            setDeleting([item]);
                          }}
                        >
                          <Trash2 size={14} /> Supprimer
                        </Button>

                        <Button
                          size="sm"
                          className="statement-card-open"
                          onClick={() =>
                            router.push(
                              `/conversions?statementId=${item.id}${conversionType === 'EXTRACT' ? '&type=extract' : ''}`,
                            )
                          }
                        >
                          {conversionType === 'EXTRACT'
                            ? 'Ouvrir la conversion'
                            : 'Ouvrir le relevé'}
                          <ArrowUpRight size={14} />
                        </Button>

                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => router.push(`/history?statementId=${item.id}`)}
                        >
                          <History size={14} />
                          Historique
                        </Button>
                        <Button size="sm" variant="outline" onClick={() => setArchive(item)}>
                          <Archive size={14} />
                          Exports & versions
                        </Button>
                        {item.sourceFileName && (
                          <>
                            <Button
                              size="sm"
                              variant="outline"
                              disabled={!!busy}
                              onClick={() => void file(item, 'preview')}
                            >
                              <Eye size={14} />
                              PDF
                            </Button>
                            <Button
                              permission="documents.export"
                              size="sm"
                              variant="outline"
                              disabled={!!busy}
                              onClick={() => void file(item, 'original')}
                            >
                              <Download size={14} />
                              Original
                            </Button>
                          </>
                        )}
                      </div>
                    </article>
                  ))}
                </div>
              </section>
            );
          })}
        </div>
        {statements.data && (
          <Pagination page={page} total={statements.data.total} onChange={setPage} />
        )}
      </>
      {validating && (
        <BankDialog
          title="Confirmer la vérification"
          busy={!!busy}
          onClose={() => {
            if (!busy) setValidating(null);
          }}
        >
          <p>
            Je confirme avoir vérifié les données du relevé «{' '}
            {validating.sourceFileName || validating.bankName} » avec le PDF original.
          </p>
          {error && (
            <p role="alert" className="text-destructive">
              {error}
            </p>
          )}
          <div className="mt-5 flex justify-end gap-3">
            <Button variant="outline" disabled={!!busy} onClick={() => setValidating(null)}>
              Annuler
            </Button>
            <Button
              disabled={!!busy}
              onClick={async () => {
                setBusy('validate');
                setError('');
                try {
                  await request(`/statement-imports/${validating.id}/validate`, {
                    method: 'POST',
                    body: JSON.stringify({ version: validating.version }),
                  });
                  setValidating(null);
                  statements.reload();
                } catch (e) {
                  setError((e as Error).message);
                } finally {
                  setBusy('');
                }
              }}
            >
              Confirmer et valider
            </Button>
          </div>
        </BankDialog>
      )}
      {deleting && (
        <BankDialog
          title="Supprimer les relevés"
          busy={!!busy}
          onClose={() => {
            if (!busy) setDeleting(null);
          }}
        >
          <p>
            Supprimer définitivement {deleting.length} relevé(s), leurs PDF et leurs exports ? La
            suppression est définitive.
          </p>
          <ul className="my-4 max-h-48 overflow-auto text-sm space-y-1">
            {deleting.map((item) => (
              <li key={item.id}>{item.sourceFileName || item.bankName}</li>
            ))}
          </ul>
          {error && (
            <p role="alert" className="text-destructive">
              {error}
            </p>
          )}
          <div className="mt-5 flex justify-end gap-3">
            <Button variant="outline" disabled={!!busy} onClick={() => setDeleting(null)}>
              Annuler
            </Button>
            <Button variant="destructive" disabled={!!busy} onClick={() => void removeSelected()}>
              {busy === 'delete' ? 'Suppression…' : 'Supprimer définitivement'}
            </Button>
          </div>
        </BankDialog>
      )}
      {archive && (
        <BankDialog
          title={`Exports · ${archive.sourceFileName || archive.bankName}`}
          wide
          onClose={() => setArchive(null)}
        >
          <ExportArchive statementId={archive.id} />
        </BankDialog>
      )}
      {original && (
        <BankDialog title={original.name} wide onClose={() => setOriginal(null)}>
          <iframe title={original.name} src={original.url} className="h-[70vh] w-full" />
        </BankDialog>
      )}
    </div>
  );
}
