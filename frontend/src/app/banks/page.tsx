'use client';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { ClientAvatar, ClientPhotoEditor } from '@/components/client-photo';
import { Camera, Landmark, Plus, RefreshCw } from 'lucide-react';
import { PageHeader } from '@/components/page-header';
import { LoadState } from '@/components/ui/load-state';
import { pageTheme } from '@/components/page-theme';
import { useEffect, useState } from 'react';
import { BankDelete, BankDetails, BankEditor } from '@/components/bank-dialogs';
import { Bank, Page } from '@/services/api';
import { useApi } from '@/services/use-api';
export default function BanksPage() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [term, setTerm] = useState('');
  const [sort, setSort] = useState('name');
  const [notice, setNotice] = useState('');
  useEffect(() => {
    const timer = setTimeout(() => {
      setTerm(search);
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [search]);
  const { data, error, loading, reload } = useApi<Page<Bank>>(
    `/banks?page=${page}&pageSize=20&search=${encodeURIComponent(term)}&sort=${sort}`,
  );
  const bankRegistry = data?.items || [];
  const [editing, setEditing] = useState<Bank | null | undefined>(undefined);
  const [deleting, setDeleting] = useState<Bank | null>(null);
  const [logoBank, setLogoBank] = useState<Bank | null>(null);
  const [details, setDetails] = useState<Bank | null>(null);
  const theme = pageTheme;
  return (
    <div className={`${theme.page} banks-page`}>
      {logoBank && (
        <ClientPhotoEditor
          resource="banks"
          client={{ ...logoBank, photoVersion: logoBank.logoVersion }}
          onClose={() => setLogoBank(null)}
          onSaved={() => {
            reload();
            setNotice('Logo enregistré.');
          }}
        />
      )}
      {editing !== undefined && (
        <BankEditor
          initial={editing}
          onClose={() => setEditing(undefined)}
          onSaved={(saved) => {
            if (!editing) setLogoBank(saved);
            setNotice(editing ? 'Banque modifiée avec succès.' : 'Banque ajoutée avec succès.');
            reload();
          }}
        />
      )}
      {deleting && (
        <BankDelete
          bank={deleting}
          onClose={() => setDeleting(null)}
          onDeleted={() => {
            setNotice('Banque supprimée.');
            if (bankRegistry.length === 1 && page > 1) setPage(page - 1);
            else reload();
          }}
        />
      )}
      {details && <BankDetails bank={details} onClose={() => setDetails(null)} />}
      <PageHeader
        icon={Landmark}
        eyebrow="Référentiel"
        title="Banques"
        description="Gérez vos banques, consultez leurs comptes."
        actions={
          <Button
            permission="banks.write"
            className={theme.button}
            onClick={() => setEditing(null)}
          >
            <Plus size={17} /> Ajouter une banque
          </Button>
        }
      />

      {notice && (
        <div
          role="status"
          className="flex justify-between gap-4 rounded-xl bg-success/10 p-4 text-[var(--success-text)]"
        >
          <span>{notice}</span>
          <button aria-label="Fermer le message" onClick={() => setNotice('')}>
            ×
          </button>
        </div>
      )}
      <section
        className={`${theme.card} catalog-toolbar flex flex-wrap items-end gap-4`}
        aria-label="Recherche et filtres"
      >
        <label className="min-w-48 flex-1 text-sm">
          Rechercher
          <Input
            type="search"
            maxLength={200}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Nom, code ou description…"
            className="ui-input mt-2"
          />
        </label>
        <label className="text-sm">
          Trier par
          <select
            value={sort}
            onChange={(e) => {
              setSort(e.target.value);
              setPage(1);
            }}
            className="ui-input mt-2"
          >
            <option value="name">Nom A–Z</option>
            <option value="name-desc">Nom Z–A</option>
            <option value="code">Code</option>
          </select>
        </label>
        <Button variant="outline" onClick={reload} disabled={loading}>
          <RefreshCw size={16} className={loading ? 'animate-spin' : ''} /> Actualiser
        </Button>
      </section>
      <LoadState loading={loading} error="" />
      {error && (
        <div role="alert" className="rounded-xl bg-destructive/10 p-4 text-destructive">
          {error}{' '}
          <button className="ml-3 underline" onClick={reload}>
            Réessayer
          </button>
        </div>
      )}
      {!loading && !error && !bankRegistry.length && (
        <section className={`${theme.card} text-center`}>
          <h2 className="text-lg font-semibold">
            {term ? 'Aucune banque ne correspond aux filtres.' : 'Aucune banque enregistrée.'}
          </h2>
          <p className={`mt-2 ${theme.meta}`}>
            {term
              ? 'Modifiez votre recherche ou réinitialisez les filtres.'
              : 'Ajoutez votre première banque pour y associer des comptes.'}
          </p>
          {term && (
            <button
              className="mt-4 text-[var(--link)]"
              onClick={() => {
                setSearch('');
                setTerm('');
                setPage(1);
              }}
            >
              Réinitialiser les filtres
            </button>
          )}
        </section>
      )}
      <section aria-label="Liste des banques" className="catalog-grid bank-grid">
        {bankRegistry.map((bank) => (
          <article key={bank.id} className={`${theme.card} bank-card`}>
            <div className="mb-4 flex items-center justify-between gap-3">
              <div className="flex min-w-0 items-center gap-3">
                <ClientAvatar
                  resource="banks"
                  client={{ ...bank, photoVersion: bank.logoVersion }}
                />
                <h2 className="text-base font-semibold text-foreground break-words">{bank.name}</h2>
              </div>
              <span
                className={`rounded-full px-2.5 py-1 text-[10px] font-semibold ${'bg-success/10 text-[var(--success-text)] ring-1 ring-inset ring-success/20'}`}
              >
                {bank.status}
              </span>
            </div>

            <div className={`space-y-3 text-sm ${theme.meta}`}>
              <div className={theme.row}>
                <span className={theme.rowLabel}>Clients</span>
                <span className={theme.rowValue}>{bank.clients}</span>
              </div>
              <div className={theme.row}>
                <span className={theme.rowLabel}>Code bancaire</span>
                <span className={theme.rowValue}>{bank.code}</span>
              </div>
              <div className={theme.row}>
                <span className={theme.rowLabel}>Comptes</span>
                <span className={theme.rowValue}>{bank.accounts}</span>
              </div>
              <p className="line-clamp-2 break-words pt-1">
                {bank.description || 'Aucune description.'}
              </p>
            </div>
            <div className="mt-5 flex flex-wrap gap-2 border-t border-current/10 pt-4 text-sm">
              <Button variant="outline" onClick={() => setDetails(bank)}>
                Détails
              </Button>
              {
                <>
                  <Button
                    permission="banks.write"
                    variant="outline"
                    onClick={() => setLogoBank(bank)}
                  >
                    <Camera size={15} /> {bank.logoVersion ? 'Modifier le logo' : 'Ajouter un logo'}
                  </Button>
                  <Button
                    permission="banks.write"
                    variant="outline"
                    className="accent-text"
                    onClick={() => setEditing(bank)}
                  >
                    Modifier
                  </Button>
                  <Button
                    permission="banks.write"
                    variant="outline"
                    className="danger-button"
                    onClick={() => setDeleting(bank)}
                  >
                    Supprimer
                  </Button>
                </>
              }
            </div>
          </article>
        ))}
      </section>
      {data && (
        <nav
          aria-label="Pagination des banques"
          className="flex flex-wrap items-center justify-between gap-4 text-sm"
        >
          <p>
            {data.total} banque(s) · Page {page} sur {Math.max(1, Math.ceil(data.total / 20))}
          </p>
          <div className="flex gap-3">
            <button
              disabled={page === 1 || loading}
              className="rounded-xl border border-current/20 px-4 py-2 disabled:cursor-not-allowed disabled:opacity-30"
              onClick={() => setPage(page - 1)}
            >
              Précédent
            </button>
            <button
              disabled={page * 20 >= data.total || loading}
              className="rounded-xl border border-current/20 px-4 py-2 disabled:cursor-not-allowed disabled:opacity-30"
              onClick={() => setPage(page + 1)}
            >
              Suivant
            </button>
          </div>
        </nav>
      )}
    </div>
  );
}
