'use client';
import { Fragment, Suspense, useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import {
  History,
  RefreshCw,
  ArrowUpRight,
  FileText,
  UserRound,
  Search,
  SlidersHorizontal,
  X,
  ChevronDown,
  FileInput,
  FilePenLine,
  FolderCheck,
  BadgeCheck,
  FileOutput,
  Trash2,
  Download,
  HardDrive,
  WandSparkles,
  Layers3,
  Clock3,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/page-header';
import { LoadState } from '@/components/ui/load-state';
import { Pagination } from '@/components/ui/pagination';
import { HistorySelection } from '@/components/history-selection';
import { HistoryChangeDetails } from '@/components/history-change-details';
import { Skeleton } from '@/components/ui/skeleton';
import { useApi } from '@/services/use-api';
import type { Page } from '@/services/api';

type Activity = {
  id: string;
  statementId: string;
  clientId: string | null;
  clientName: string;
  accountName: string;
  bankName: string;
  fileName: string;
  action: string;
  actorName: string;
  version: number;
  createdAt: string;
  changesJson?: string | null;
};
type ProfileActivity = {
  id: string;
  profileName: string;
  action: string;
  actorName: string;
  version: number;
  createdAt: string;
  snapshotJson: string;
};
const actionNames: Record<string, string> = {
  IMPORTED: 'Relevé importé',
  MODIFIED: 'Relevé modifié',
  FILED: 'Relevé classé',
  VALIDATED: 'Relevé validé',
  EXPORTED: 'Fichier exporté',
  DELETED: 'Relevé supprimé',
  DOWNLOAD_COMPLETED: 'Téléchargement terminé',
  SOURCE_SAVED: 'PDF original conservé',
  MODEL_APPLIED: 'Modèle appliqué',
};
const actionIcons: Record<string, typeof History> = {
  IMPORTED: FileInput,
  MODIFIED: FilePenLine,
  FILED: FolderCheck,
  VALIDATED: BadgeCheck,
  EXPORTED: FileOutput,
  DELETED: Trash2,
  DOWNLOAD_COMPLETED: Download,
  SOURCE_SAVED: HardDrive,
  MODEL_APPLIED: WandSparkles,
  CREATED: Layers3,
  UPDATED: FilePenLine,
};
const utcDay = (value: string) => new Date(value).toISOString().slice(0, 10);
const dayLabel = (value: string) =>
  new Date(value).toLocaleDateString('fr-FR', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC',
  });
const stamp = (value: string) =>
  new Date(value).toLocaleString('fr-FR', {
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
    timeZone: 'UTC',
  });
function HistoryEvent({
  title,
  subtitle,
  action,
  label,
  actor,
  version,
  createdAt,
  controls,
  href,
  children,
  snapshot = false,
}: {
  title: string;
  subtitle?: string;
  action: string;
  label: string;
  actor: string;
  version: number;
  createdAt: string;
  controls: ReactNode;
  href?: string;
  children?: ReactNode;
  snapshot?: boolean;
}) {
  const Icon = actionIcons[action] ?? History;
  return (
    <article
      className={`history-event ${controls ? 'can-manage' : ''}`}
      data-action={action}
      aria-label={`${label} · ${title}`}
    >
      <div className="history-event-heading">
        {controls}
        <span className="history-event-icon" aria-hidden="true">
          <Icon size={18} />
        </span>
        <div className="history-event-document">
          <h4>
            {href ? (
              <Link href={href}>
                {title}
                <ArrowUpRight size={13} aria-hidden="true" />
              </Link>
            ) : (
              title
            )}
          </h4>
          {subtitle && <p>{subtitle}</p>}
        </div>
        <div className="history-event-side">
          <span className="history-action">{label}</span>
          <time
            dateTime={createdAt}
            title={`${new Date(createdAt).toLocaleString('fr-FR', { timeZone: 'UTC' })} UTC`}
          >
            {stamp(createdAt)} <small>UTC</small>
          </time>
        </div>
      </div>
      <div className="history-event-footer">
        <p className="history-author">
          <UserRound size={13} aria-hidden="true" />
          <span>{actor || 'Application'}</span>
          <span className="history-version">v{version}</span>
        </p>
        {href && (
          <Link className="history-open-link" href={href}>
            Ouvrir le relevé
            <ArrowUpRight size={13} aria-hidden="true" />
          </Link>
        )}
      </div>
      {children && (
        <details className="history-changes">
          <summary>
            {snapshot ? 'Réglages enregistrés' : 'Voir les modifications'}
            <ChevronDown size={13} aria-hidden="true" />
          </summary>
          {children}
        </details>
      )}
    </article>
  );
}
export default function HistoryPage() {
  const [loading, setLoading] = useState(true);
  const reloadRef = useRef<() => void>(() => {});
  const updateControls = useCallback((isLoading: boolean, reload: () => void) => {
    reloadRef.current = reload;
    setLoading(isLoading);
  }, []);

  return (
    <div className="ui-page history-page">
      <PageHeader
        icon={History}
        eyebrow="Journal d’activité"
        title="Historique"
        description="Suivez vos relevés, vos conversions et les changements de modèles."
        actions={
          <Button
            variant="outline"
            size="sm"
            disabled={loading}
            onClick={() => {
              setLoading(true);
              reloadRef.current();
            }}
          >
            <RefreshCw size={15} className={loading ? 'animate-spin' : ''} />
            Actualiser
          </Button>
        }
      />
      <Suspense fallback={<HistoryJournalSkeleton />}>
        <HistoryContent onControls={updateControls} />
      </Suspense>
    </div>
  );
}
function HistoryJournalSkeleton() {
  return (
    <>
      <div className="history-overview history-overview-skeleton" aria-hidden="true">
        {[0, 1, 2].map((item) => (
          <div className="history-stat-placeholder" key={item}>
            <Skeleton className="size-10 shrink-0" />
            <div className="grid min-w-0 flex-1 gap-2">
              <Skeleton className="h-6 w-16" />
              <Skeleton className="h-3 w-4/5" />
            </div>
          </div>
        ))}
      </div>
      <section className="history-panel" aria-label="Journal d’activité">
        <div className="history-tabs history-tabs-skeleton" aria-hidden="true">
          <Skeleton className="h-11 w-52" />
          <Skeleton className="h-11 w-48" />
        </div>
        <div className="history-initial-loading" role="status" aria-live="polite">
          <span className="sr-only">Chargement de l’historique…</span>
          <div className="history-toolbar-skeleton" aria-hidden="true">
            <Skeleton className="h-10 min-w-48 flex-1" />
            <Skeleton className="h-10 w-44" />
            <Skeleton className="h-10 w-24" />
          </div>
          <div className="history-event-skeletons" aria-hidden="true">
            {[0, 1, 2].map((item) => (
              <div className="history-event-skeleton" key={item}>
                <Skeleton className="size-9 shrink-0" />
                <div className="grid min-w-0 flex-1 gap-2">
                  <Skeleton className="h-4 w-2/5" />
                  <Skeleton className="h-3 w-1/4" />
                  <Skeleton className="h-3 w-20" />
                </div>
                <Skeleton className="h-4 w-24" />
              </div>
            ))}
          </div>
        </div>
      </section>
    </>
  );
}
function HistoryContent({ onControls }: { onControls: (loading: boolean, reload: () => void) => void }) {
  const params = useSearchParams();
  const scope: Record<string, string> = {};
  for (const key of ['statementId', 'clientId', 'bankAccountId']) {
    const value = params.get(key);
    if (value) scope[key] = value;
  }
  return <HistoryJournal key={JSON.stringify(scope)} scope={scope} onControls={onControls} />;
}
function HistoryJournal({
  scope,
  onControls,
}: {
  scope: Record<string, string>;
  onControls: (loading: boolean, reload: () => void) => void;
}) {
  const [page, setPage] = useState(1);
  const [profilePage, setProfilePage] = useState(1);
  const [tab, setTab] = useState<'statements' | 'profiles'>('statements');
  const [search, setSearch] = useState('');
  const [term, setTerm] = useState('');
  const [action, setAction] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [showDates, setShowDates] = useState(false);
  useEffect(() => {
    const timer = setTimeout(() => {
      setTerm(search.trim());
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [search]);
  const filters = {
    ...scope,
    ...(term ? { search: term } : {}),
    ...(action ? { action } : {}),
    ...(from ? { from } : {}),
    ...(to ? { to } : {}),
  };
  const query = new URLSearchParams({ ...filters, page: String(page), pageSize: '20' });
  const history = useApi<Page<Activity> & { filterVersion?: number }>(
    `/client-workspace/history?${query}`,
    true,
  );
  const profiles = useApi<Page<ProfileActivity>>(
    `/extraction-profiles/history?page=${profilePage}&pageSize=20`,
    true,
  );
  const filtered = !!(term || action || from || to);
  const historyError =
    history.error ||
    (filtered && history.data && history.data.filterVersion !== 1
      ? 'Les filtres nécessitent la nouvelle version du service. Redémarrez le backend puis actualisez la page.'
      : '');
  const historyData = historyError ? null : history.data;
  const active = tab === 'statements' ? { ...history, data: historyData } : profiles;
  const reloadHistory = history.reload;
  const reloadProfiles = profiles.reload;
  const reload = useCallback(() => {
    reloadHistory();
    reloadProfiles();
  }, [reloadHistory, reloadProfiles]);
  useEffect(() => {
    onControls(history.loading || profiles.loading, reload);
  }, [history.loading, onControls, profiles.loading, reload]);
  function reset() {
    setSearch('');
    setTerm('');
    setAction('');
    setFrom('');
    setTo('');
    setPage(1);
  }
  function switchTab(value: typeof tab) {
    setTab(value);
  }
  if (!history.data && !profiles.data && history.loading && profiles.loading)
    return <HistoryJournalSkeleton />;
  return (
    <>
      <div className="history-overview" aria-label="Résumé de l’activité">
        <div className="history-stat">
          <span className="history-stat-icon">
            <History size={18} />
          </span>
          <div>
            <strong>{historyData?.total ?? '—'}</strong>
            <span>
              {filtered ? 'Actions correspondant aux filtres' : 'Actions sur les relevés'}
            </span>
          </div>
        </div>
        <div className="history-stat">
          <span className="history-stat-icon">
            <Layers3 size={18} />
          </span>
          <div>
            <strong>{profiles.data?.total ?? '—'}</strong>
            <span>Actions sur les modèles</span>
          </div>
        </div>
        <div className="history-stat">
          <span className="history-stat-icon">
            <Clock3 size={18} />
          </span>
          <div>
            <strong className="history-last-date">
              {active.data?.items[0] ? stamp(active.data.items[0].createdAt) : '—'}
            </strong>
            <span>Dernière action de cette page · UTC</span>
          </div>
        </div>
      </div>
      <section className="history-panel" aria-label="Journal d’activité">
        <div className="history-tabs" role="tablist" aria-label="Type d’historique">
          {(
            [
              ['statements', 'Relevés & conversions', FileText, historyData?.total],
              ['profiles', 'Modèles d’extraction', Layers3, profiles.data?.total],
            ] as const
          ).map(([value, label, Icon, count]) => (
            <button
              key={value}
              id={`history-tab-${value}`}
              role="tab"
              aria-selected={tab === value}
              aria-controls={`history-panel-${value}`}
              tabIndex={tab === value ? 0 : -1}
              onClick={() => switchTab(value)}
              onKeyDown={(event) => {
                if (['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) {
                  event.preventDefault();
                  const target =
                    event.key === 'Home'
                      ? 'statements'
                      : event.key === 'End'
                        ? 'profiles'
                        : tab === 'statements'
                          ? 'profiles'
                          : 'statements';
                  switchTab(target);
                  document.getElementById(`history-tab-${target}`)?.focus();
                }
              }}
            >
              <Icon size={16} />
              {label}
              <span>{count ?? '—'}</span>
            </button>
          ))}
        </div>
        {tab === 'statements' ? (
          <div
            key="statements"
            id="history-panel-statements"
            role="tabpanel"
            aria-labelledby="history-tab-statements"
          >
            <h2 className="sr-only">Activité des relevés et conversions</h2>
            <div className="history-toolbar">
              <label className="history-search">
                <Search size={17} aria-hidden="true" />
                <span className="sr-only">Rechercher dans l’historique</span>
                <input
                  type="search"
                  className="ui-input"
                  placeholder="Rechercher un fichier, un client, une banque ou un auteur…"
                  maxLength={150}
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                />
              </label>
              <label className="history-action-filter">
                <span className="sr-only">Type d’action</span>
                <select
                  className="ui-input"
                  value={action}
                  onChange={(event) => {
                    setAction(event.target.value);
                    setPage(1);
                  }}
                >
                  <option value="">Toutes les actions</option>
                  {Object.entries(actionNames).map(([code, label]) => (
                    <option key={code} value={code}>
                      {label}
                    </option>
                  ))}
                </select>
              </label>
              <Button
                size="sm"
                variant="outline"
                aria-expanded={showDates}
                aria-controls="history-period"
                onClick={() => setShowDates(!showDates)}
              >
                <SlidersHorizontal size={15} />
                Période{(from || to) && <span className="history-filter-dot" />}
              </Button>
              {filtered && (
                <Button size="sm" variant="ghost" onClick={reset}>
                  <X size={14} />
                  Effacer
                </Button>
              )}
            </div>
            {showDates && (
              <div className="history-period" id="history-period">
                <label>
                  Du (UTC)
                  <input
                    className="ui-input"
                    type="date"
                    value={from}
                    max={to || undefined}
                    onChange={(event) => {
                      setFrom(event.target.value);
                      setPage(1);
                    }}
                  />
                </label>
                <label>
                  Au (UTC)
                  <input
                    className="ui-input"
                    type="date"
                    value={to}
                    min={from || undefined}
                    onChange={(event) => {
                      setTo(event.target.value);
                      setPage(1);
                    }}
                  />
                </label>
                <p>Recherche sur l’ensemble de l’historique, avant pagination.</p>
              </div>
            )}
            {Object.keys(scope).length > 0 && (
              <div className="history-scope">
                <span>
                  Historique ciblé sur{' '}
                  {scope.statementId
                    ? 'un relevé'
                    : scope.bankAccountId
                      ? 'un compte'
                      : 'un client'}
                </span>
                <Link href="/history">
                  Tout l’historique
                  <ArrowUpRight size={13} />
                </Link>
              </div>
            )}
            <div className="history-results-note" role="status">
              {historyError
                ? 'Lecture indisponible'
                : history.loading
                  ? 'Chargement des actions…'
                  : `${historyData?.total ?? 0} action${historyData?.total === 1 ? '' : 's'}${filtered ? ' trouvée' + (historyData?.total === 1 ? '' : 's') : ' enregistrée' + (historyData?.total === 1 ? '' : 's')}`}
              {filtered && <span>Filtres actifs</span>}
            </div>
            <LoadState loading={history.loading && !historyData} error={historyError} />
            {historyError && (
              <Button size="sm" variant="outline" onClick={history.reload}>
                Réessayer
              </Button>
            )}
            {!history.loading && !historyError && !historyData?.items.length && (
              <div className="history-empty">
                <Search size={26} />
                <h3>
                  {filtered ? 'Aucune action ne correspond' : 'Votre historique commence ici'}
                </h3>
                <p>
                  {filtered
                    ? 'Essayez un autre terme ou ajustez la période.'
                    : 'Les prochaines actions sur vos relevés et extraits apparaîtront ici.'}
                </p>
                {filtered && (
                  <Button size="sm" variant="outline" onClick={reset}>
                    Réinitialiser les filtres
                  </Button>
                )}
              </div>
            )}
            <div className="history-timeline">
              <HistorySelection<Activity>
                key={JSON.stringify(filters)}
                kind="statements"
                filters={filters}
                items={historyData?.items ?? []}
                total={historyData?.total ?? 0}
                loading={history.loading}
                onDeleted={(count) => {
                  setPage(
                    Math.min(
                      page,
                      Math.max(1, Math.ceil(((historyData?.total ?? 0) - count) / 20)),
                    ),
                  );
                  history.reload();
                }}
              >
                {(item, controls) => {
                  const index = historyData!.items.indexOf(item);
                  const previous = historyData!.items[index - 1];
                  return (
                    <Fragment key={item.id}>
                      {(!previous || utcDay(previous.createdAt) !== utcDay(item.createdAt)) && (
                        <h3 className="history-day-label">{dayLabel(item.createdAt)}</h3>
                      )}
                      <HistoryEvent
                        title={item.fileName || item.bankName || 'Relevé bancaire'}
                        subtitle={[
                          item.clientName || 'Relevé à classer',
                          item.bankName,
                          item.accountName,
                        ]
                          .filter(Boolean)
                          .join(' · ')}
                        action={item.action}
                        label={actionNames[item.action] || item.action}
                        actor={item.actorName}
                        version={item.version}
                        createdAt={item.createdAt}
                        controls={controls}
                        href={
                          item.action !== 'DELETED'
                            ? `/conversions?statementId=${encodeURIComponent(item.statementId)}`
                            : undefined
                        }
                      >
                        {item.changesJson ? (
                          <HistoryChangeDetails value={item.changesJson} />
                        ) : null}
                      </HistoryEvent>
                    </Fragment>
                  );
                }}
              </HistorySelection>
            </div>
            {historyData && historyData.total > 0 && (
              <Pagination
                page={page}
                total={historyData.total}
                onChange={setPage}
                loading={history.loading}
                itemLabel="actions"
                label="Pagination des actions"
              />
            )}
          </div>
        ) : (
          <div
            key="profiles"
            id="history-panel-profiles"
            role="tabpanel"
            aria-labelledby="history-tab-profiles"
          >
            <div className="history-profile-heading">
              <div>
                <h2>Modèles d’extraction</h2>
                <p>Créations, modifications et réglages enregistrés.</p>
              </div>
              <span>{profiles.data?.total ?? '—'} actions</span>
            </div>
            <LoadState loading={profiles.loading && !profiles.data} error={profiles.error} />
            {profiles.error && (
              <Button size="sm" variant="outline" onClick={profiles.reload}>
                Réessayer
              </Button>
            )}
            {!profiles.loading && !profiles.error && !profiles.data?.items.length && (
              <div className="history-empty">
                <Layers3 size={26} />
                <h3>Aucune modification de modèle</h3>
                <p>Les actions sur vos modèles d’extraction apparaîtront ici.</p>
              </div>
            )}
            <div className="history-timeline">
              <HistorySelection<ProfileActivity>
                kind="profiles"
                items={profiles.data?.items ?? []}
                total={profiles.data?.total ?? 0}
                loading={profiles.loading}
                onDeleted={(count) => {
                  setProfilePage(
                    Math.min(
                      profilePage,
                      Math.max(1, Math.ceil(((profiles.data?.total ?? 0) - count) / 20)),
                    ),
                  );
                  profiles.reload();
                }}
              >
                {(item, controls) => {
                  const index = profiles.data!.items.indexOf(item);
                  const previous = profiles.data!.items[index - 1];
                  return (
                    <Fragment key={item.id}>
                      {(!previous || utcDay(previous.createdAt) !== utcDay(item.createdAt)) && (
                        <h3 className="history-day-label">{dayLabel(item.createdAt)}</h3>
                      )}
                      <HistoryEvent
                        title={item.profileName}
                        action={item.action}
                        label={
                          item.action === 'CREATED'
                            ? 'Modèle créé'
                            : item.action === 'UPDATED'
                              ? 'Modèle modifié'
                              : item.action === 'DELETED'
                                ? 'Modèle supprimé'
                                : item.action
                        }
                        actor={item.actorName}
                        version={item.version}
                        createdAt={item.createdAt}
                        controls={controls}
                        snapshot
                      >
                        <HistoryChangeDetails value={item.snapshotJson} snapshot />
                      </HistoryEvent>
                    </Fragment>
                  );
                }}
              </HistorySelection>
            </div>
            {profiles.data && profiles.data.total > 0 && (
              <Pagination
                page={profilePage}
                total={profiles.data.total}
                onChange={setProfilePage}
                loading={profiles.loading}
                itemLabel="actions"
                label="Pagination des modèles"
              />
            )}
          </div>
        )}
      </section>
    </>
  );
}
