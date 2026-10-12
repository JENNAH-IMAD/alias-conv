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
export function ClientConversions({ clientId }: { clientId: string }) {
  const router = useRouter();
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
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h2 className="font-semibold">
            Relevés & exports <span className="ui-tag ml-2">{statements.data?.total ?? '—'}</span>
          </h2>
          <p className="muted text-sm mt-2">
            Les 5 derniers relevés ajoutés par compte bancaire. Au 6e ajout, le plus ancien est
            supprimé définitivement ; sa trace reste dans l’historique.
          </p>
        </div>
        <Button
          permission="documents.write"
          onClick={() =>
            router.push(`/conversions?import=1${clientId ? `&clientId=${clientId}` : ''}`)
          }
        >
          <Plus size={16} />
          Importer et convertir
        </Button>
      </div>
      <form
        className="ui-card statement-archive-filters"
        onSubmit={(event) => {
          event.preventDefault();
          setTerm(search.trim());
          setPage(1);
        }}
      >
        <label className="statement-archive-search">
          Rechercher un relevé
          <div className="flex gap-2 mt-2">
            <input
              aria-label="Rechercher un relevé"
              className="ui-input"
              placeholder="Banque, numéro, PDF ou client…"
              maxLength={150}
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
            <Button type="submit" size="icon" variant="outline" aria-label="Rechercher les relevés">
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
              <option value="all">Tous les relevés</option>
              <option value="unassigned">À classer dans un client</option>
            </select>
          </label>
        )}
        <label>
          Statut du relevé
          <select
            aria-label="Statut du relevé"
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
          Les relevés sans client apparaissent aussi ici. Ouvrez un relevé puis utilisez « Classer »
          pour le rattacher au bon client et à son compte.
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
            <span className="muted text-sm">{selected.length} relevé(s) sélectionné(s)</span>
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
                    <h3>{first.accountName || 'Relevés sans compte'}</h3>
                    <p className="muted text-sm">
                      {first.bankName} ·{' '}
                      {first.accountNumber
                        ? accountSuffix(first.accountNumber)
                        : 'Choisissez un dossier client'}
                    </p>
                  </div>
                  <span className="ui-tag">
                    {items.length} relevé(s){accountId !== 'unassigned' ? ' · 5 maximum' : ''}
                  </span>
                </header>
                <div className="client-archive-grid">
                  {items.map((item) => (
                    <article key={item.id} className="ui-card client-archive-card">
                      <div className="flex justify-between items-start gap-3">
                        <div className="flex items-center gap-3">
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
                          <FileText className="accent-text" size={23} />
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
                      <div>
                        <h3>{item.sourceFileName || item.bankName}</h3>
                        <p className="statement-archive-owner">
                          {item.clientName ||
                            (item.clientId ? 'Client associé' : 'À classer · aucun client associé')}
                        </p>
                        <p>
                          {item.bankName}
                          {item.accountName ? ` · ${item.accountName}` : ''}
                          {item.accountNumber ? ` · ${accountSuffix(item.accountNumber)}` : ''}
                        </p>
                        {item.statementNumber && (
                          <p>
                            {item.statementNumber}
                            {item.currency ? ` · ${item.currency}` : ''}
                          </p>
                        )}
                        <p>
                          {date(item.periodStart)} — {date(item.periodEnd)}
                        </p>
                      </div>
                      <div className="client-archive-meta">
                        <span>{item.transactionCount} mouvements</span>
                        <span>Version {item.version}</span>
                        <span>{item.exportCount} export(s)</span>
                      </div>
                      <p>
                        Importé {new Date(item.createdAt).toLocaleString('fr-FR')}
                        {item.importedByName ? ` par ${item.importedByName}` : ''}
                        {item.modifiedByName ? ` · modifié par ${item.modifiedByName}` : ''}
                      </p>
                      {item.modifiedByName && (
                        <p className="muted text-xs">
                          Dernière modification : {new Date(item.updatedAt).toLocaleString('fr-FR')}{' '}
                          · {item.modifiedByName}
                        </p>
                      )}
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
                          onClick={() => router.push(`/conversions?statementId=${item.id}`)}
                        >
                          Ouvrir le relevé
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
                        {item.status === 'VALIDATED' && (
                          <Button
                            permission="documents.export"
                            size="sm"
                            variant="outline"
                            disabled={!!busy}
                            onClick={() => void file(item, 'standard')}
                          >
                            <Download size={14} />
                            CSV standard
                          </Button>
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
