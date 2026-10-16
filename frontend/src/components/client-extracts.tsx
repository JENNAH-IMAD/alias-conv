'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { Download, Eye, RefreshCw, Search, FileText } from 'lucide-react';
import { documentBlob, saveBlob, type Page } from '@/services/api';
import { useApi } from '@/services/use-api';
import { money, date, accountSuffix, type StatementExtract } from '@/services/statement-exports';
import { Button } from './ui/button';
import { LoadState } from './ui/load-state';
import { Pagination } from './ui/pagination';
import { BankDialog } from './bank-dialogs';

type ArchivedExtract = Omit<StatementExtract, 'transactions' | 'sourceVersion'> & {
  statementId: string;
  clientName: string | null;
  bankName: string;
  sourceFileName: string | null;
  accountName: string | null;
  accountNumber: string | null;
  transactionCount: number;
};

export function ClientExtracts({ clientId }: { clientId: string }) {
  const router = useRouter();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [term, setTerm] = useState('');
  const [selected, setSelected] = useState<ArchivedExtract | null>(null);
  const [previewPage, setPreviewPage] = useState(1);
  const [busy, setBusy] = useState('');
  const [error, setError] = useState('');
  const query = new URLSearchParams({ page: String(page), pageSize: '20', search: term });
  const extracts = useApi<Page<ArchivedExtract>>(
    `/client-workspace/${clientId ? `${clientId}/` : ''}extracts?${query}`,
  );
  const preview = useApi<StatementExtract>(
    selected ? `/statement-imports/${selected.statementId}/extracts/${selected.id}` : null,
  );

  async function download(item: ArchivedExtract) {
    setBusy(item.id);
    setError('');
    try {
      saveBlob(
        await documentBlob(`/statement-imports/${item.statementId}/extracts/${item.id}/file`),
        `extrait_${item.id}.csv`,
      );
    } catch (reason) {
      setError((reason as Error).message);
    } finally {
      setBusy('');
    }
  }

  function totals(
    item: Pick<StatementExtract, 'totalDebit' | 'totalCredit' | 'balance' | 'currency'>,
  ) {
    return (
      <dl className="extract-totals">
        <div>
          <dt>Total débit</dt>
          <dd>
            {money(item.totalDebit)} {item.currency}
          </dd>
        </div>
        <div>
          <dt>Total crédit</dt>
          <dd>
            {money(item.totalCredit)} {item.currency}
          </dd>
        </div>
        <div>
          <dt>Solde (crédit − débit)</dt>
          <dd>
            {money(item.balance)} {item.currency}
          </dd>
        </div>
      </dl>
    );
  }

  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h2 className="font-semibold">
            Extraits de relevés bancaires{' '}
            <span className="ui-tag ml-2">{extracts.data?.total ?? '—'}</span>
          </h2>
          <p className="muted text-sm mt-2">
            Les lignes sauvegardées avec le texte du filtre, les totaux débit et crédit et le solde.
          </p>
        </div>
        <Button
          permission="documents.write"
          onClick={() =>
            router.push(
              `/conversions?import=1&type=extract${clientId ? `&clientId=${encodeURIComponent(clientId)}` : ''}`,
            )
          }
        >
          Convertir un extrait bancaire
        </Button>
      </header>
      <form
        className="ui-card flex flex-wrap items-end gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          setTerm(search.trim());
          setPage(1);
        }}
      >
        <label className="flex-1">
          Rechercher un extrait
          <input
            className="ui-input mt-2"
            value={search}
            maxLength={150}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Texte du filtre, banque, PDF ou client…"
          />
        </label>
        <Button type="submit" variant="outline">
          <Search size={16} />
          Rechercher
        </Button>
        <Button
          type="button"
          variant="outline"
          aria-label="Actualiser les extraits"
          disabled={extracts.loading}
          onClick={extracts.reload}
        >
          <RefreshCw size={16} />
        </Button>
      </form>
      <LoadState loading={extracts.loading} error={extracts.error} />
      {!extracts.loading && !extracts.error && !extracts.data?.items.length && (
        <div className="ui-card">
          <p>Aucun extrait enregistré pour cette sélection.</p>
          <p className="muted text-sm mt-2">
            Les filtres sauvegardés apparaissent ici. Les extraits PDF importés et convertis sont
            disponibles dans la liste des conversions ci-dessous.
          </p>
        </div>
      )}
      {error && (
        <p role="alert" className="conversion-alert error">
          {error}
        </p>
      )}
      <div className="client-archive-grid">
        {extracts.data?.items.map((item) => (
          <article key={item.id} className="ui-card client-archive-card">
            <div className="flex items-center gap-3">
              <FileText size={23} className="accent-text" />
              <span className="ui-tag">Extrait enregistré</span>
            </div>
            <div>
              <h3>{item.filterText || 'Tous les mouvements'}</h3>
              {item.excludeText && <p>Exclus : {item.excludeText}</p>}
              <p>
                {item.clientName || 'À classer'} · {item.bankName}
              </p>
              <p>
                {item.accountName || 'Sans compte bancaire'}{' '}
                {item.accountNumber ? `· ${accountSuffix(item.accountNumber)}` : ''}
              </p>
              <p>{item.sourceFileName || 'Relevé bancaire'}</p>
              <p className="muted text-sm">
                {item.transactionCount} mouvement(s) ·{' '}
                {new Date(item.createdAt).toLocaleString('fr-FR', { timeZone: 'UTC' })} UTC
              </p>
            </div>
            {totals(item)}
            <div className="flex flex-wrap gap-2">
              <Button
                size="sm"
                variant="outline"
                onClick={() => {
                  setSelected(item);
                  setPreviewPage(1);
                }}
              >
                <Eye size={15} />
                Aperçu du fichier
              </Button>
              <Button
                size="sm"
                permission="documents.export"
                disabled={!!busy}
                onClick={() => void download(item)}
              >
                <Download size={15} />
                Télécharger CSV
              </Button>
              <Link
                className="conversion-text-action"
                href={`/conversions?statementId=${item.statementId}&type=extract&extractId=${item.id}`}
              >
                Ouvrir la conversion
              </Link>
            </div>
          </article>
        ))}
      </div>
      {extracts.data && <Pagination page={page} total={extracts.data.total} onChange={setPage} />}
      {selected && (
        <BankDialog
          wide
          title="Aperçu de l’extrait de relevé bancaire"
          onClose={() => setSelected(null)}
        >
          <LoadState loading={preview.loading} error={preview.error} />
          {preview.data && (
            <div className="space-y-4">
              <p>
                Texte du filtre :{' '}
                <strong>{preview.data.filterText || 'Tous les mouvements'}</strong>
              </p>
              {preview.data.excludeText && (
                <p>
                  Textes exclus : <strong>{preview.data.excludeText}</strong>
                </p>
              )}
              <div className="conversion-table-scroll">
                <table className="conversion-table" aria-label="Mouvements de l’extrait">
                  <thead>
                    <tr>
                      <th>Date</th>
                      <th>Date valeur</th>
                      <th>Libellé</th>
                      <th className="conversion-money">Débit</th>
                      <th className="conversion-money">Crédit</th>
                    </tr>
                  </thead>
                  <tbody>
                    {preview.data.transactions
                      .slice((previewPage - 1) * 50, previewPage * 50)
                      .map((row) => (
                        <tr key={row.id}>
                          <td>{date(row.date)}</td>
                          <td>{date(row.valueDate)}</td>
                          <td>{row.description}</td>
                          <td className="conversion-money">{money(row.debit)}</td>
                          <td className="conversion-money">{money(row.credit)}</td>
                        </tr>
                      ))}
                  </tbody>
                </table>
              </div>
              {preview.data.transactions.length > 50 && (
                <nav className="conversion-pagination" aria-label="Pages de l’extrait">
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={previewPage === 1}
                    onClick={() => setPreviewPage(previewPage - 1)}
                  >
                    Précédent
                  </Button>
                  <span>
                    Page {previewPage} sur {Math.ceil(preview.data.transactions.length / 50)}
                  </span>
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={previewPage * 50 >= preview.data.transactions.length}
                    onClick={() => setPreviewPage(previewPage + 1)}
                  >
                    Suivant
                  </Button>
                </nav>
              )}
              {totals(preview.data)}
              <Button
                permission="documents.export"
                disabled={!!busy}
                onClick={() => void download(selected)}
              >
                <Download size={15} />
                Télécharger l’extrait CSV
              </Button>
            </div>
          )}
        </BankDialog>
      )}
    </div>
  );
}
