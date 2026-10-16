'use client';
import { Suspense, useState } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import {
  History,
  RefreshCw,
  ArrowUpRight,
  FileText,
  UserRound,
  Activity as ActivityIcon,
} from 'lucide-react';
import { PageHeader } from '@/components/page-header';
import { Button } from '@/components/ui/button';
import { LoadState } from '@/components/ui/load-state';
import { Pagination } from '@/components/ui/pagination';
import { HistorySelection } from '@/components/history-selection';
import { HistoryChangeDetails } from '@/components/history-change-details';
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
  DELETED: 'Relevé supprimé définitivement',
  DOWNLOAD_COMPLETED: 'Téléchargement terminé',
  SOURCE_SAVED: 'PDF original conservé',
  MODEL_APPLIED: 'Modèle d’extraction appliqué',
};
export default function HistoryPage() {
  return (
    <Suspense fallback={<p role="status">Chargement de l’historique…</p>}>
      <HistoryContent />
    </Suspense>
  );
}
function HistoryContent() {
  const params = useSearchParams();
  const [page, setPage] = useState(1);
  const [profilePage, setProfilePage] = useState(1);
  const query = new URLSearchParams({ page: String(page), pageSize: '20' });
  const filters: Record<string, string> = {};
  for (const key of ['statementId', 'clientId', 'bankAccountId']) {
    const value = params.get(key);
    if (value) {
      query.set(key, value);
      filters[key] = value;
    }
  }
  const history = useApi<Page<Activity>>(`/client-workspace/history?${query}`);
  const profiles = useApi<Page<ProfileActivity>>(
    `/extraction-profiles/history?page=${profilePage}&pageSize=20`,
  );
  return (
    <div className="ui-page history-page">
      <PageHeader
        icon={History}
        eyebrow="Suivi de l’activité"
        title="Historique"
        description="Retrouvez les actions sur les relevés, les conversions et les modèles d’extraction."
      />
      <div className="history-overview">
        <div className="ui-card history-stat">
          <History size={21} />
          <div>
            <strong>{history.data?.total ?? '—'}</strong>
            <span>Actions enregistrées</span>
          </div>
        </div>
        <div className="ui-card history-stat">
          <FileText size={21} />
          <div>
            <strong>
              {history.data
                ? new Set(history.data.items.map((item) => item.statementId)).size
                : '—'}
            </strong>
            <span>Relevés sur cette page</span>
          </div>
        </div>
        <div className="ui-card history-stat">
          <ActivityIcon size={21} />
          <div>
            <strong>
              {history.data?.items[0]
                ? new Date(history.data.items[0].createdAt).toLocaleDateString('fr-FR')
                : '—'}
            </strong>
            <span>Dernière activité affichée</span>
          </div>
        </div>
      </div>
      <section className="ui-card history-panel" aria-label="Historique des relevés">
        <div className="flex flex-wrap justify-between items-center gap-3">
          <div>
            <h2 className="text-lg font-semibold">Activité des relevés</h2>
            <p className="muted text-sm">Dates, auteurs et modifications enregistrées.</p>
          </div>
          <div className="flex flex-wrap gap-3">
            {params.size > 0 && (
              <Button variant="outline" asChild>
                <Link href="/history">Tout l’historique</Link>
              </Button>
            )}
            <Button variant="outline" disabled={history.loading} onClick={history.reload}>
              <RefreshCw size={16} />
              Actualiser
            </Button>
          </div>
        </div>
        <LoadState loading={history.loading} error={history.error} />
        {!history.loading && !history.error && !history.data?.items.length && (
          <div className="empty-state">
            <History size={30} />
            <h3>Aucune activité enregistrée</h3>
            <p>Les nouvelles actions sur les relevés apparaîtront ici.</p>
          </div>
        )}
        <div className="history-timeline">
          <HistorySelection<Activity>
            key={JSON.stringify(filters)}
            kind="statements"
            filters={filters}
            items={history.data?.items ?? []}
            total={history.data?.total ?? 0}
            loading={history.loading}
            onDeleted={(count) => {
              setPage(
                Math.min(page, Math.max(1, Math.ceil(((history.data?.total ?? 0) - count) / 20))),
              );
              history.reload();
            }}
          >
            {(item, controls) => (
              <article key={item.id} className="history-event" data-action={item.action}>
                <div className="history-event-heading">
                  {controls}
                  <strong className="history-action">
                    {actionNames[item.action] || item.action}
                  </strong>
                  <time className="muted text-xs" dateTime={item.createdAt}>
                    {new Date(item.createdAt).toLocaleString('fr-FR')}
                  </time>
                </div>
                <div className="history-event-document">
                  <FileText size={18} aria-hidden="true" />
                  <div>
                    <h3 className="font-semibold">{item.fileName || item.bankName}</h3>
                    <p className="muted text-sm">
                      {item.clientName || 'Relevé à classer'} · {item.bankName}
                      {item.accountName ? ` · ${item.accountName}` : ''}
                    </p>
                  </div>
                </div>
                <div className="history-event-footer">
                  <p className="history-author">
                    <UserRound size={14} aria-hidden="true" />{' '}
                    <strong>{item.actorName || 'Application'}</strong>
                    <span className="history-version">Version {item.version}</span>
                  </p>
                  {item.action !== 'DELETED' && (
                    <Link
                      className="history-open-link accent-text inline-flex items-center gap-2 text-sm"
                      href={`/conversions?statementId=${encodeURIComponent(item.statementId)}`}
                    >
                      Ouvrir le relevé
                      <ArrowUpRight size={14} />
                    </Link>
                  )}
                </div>
                {item.changesJson && (
                  <details className="history-changes">
                    <summary className="cursor-pointer text-sm accent-text">
                      Voir les modifications
                    </summary>
                    <HistoryChangeDetails value={item.changesJson} />
                  </details>
                )}
              </article>
            )}
          </HistorySelection>
        </div>
        {history.data && <Pagination page={page} total={history.data.total} onChange={setPage} />}
      </section>
      <details
        className="ui-card history-profiles"
        aria-label="Historique des modèles d’extraction"
      >
        <summary>
          Historique des modèles d’extraction <span>{profiles.data?.total ?? '—'} action(s)</span>
        </summary>
        <div className="history-profile-content">
          <LoadState loading={profiles.loading} error={profiles.error} />
          {!profiles.loading && !profiles.error && !profiles.data?.items.length && (
            <p className="muted text-sm">Aucune modification de modèle enregistrée.</p>
          )}
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
            {(item, controls) => (
              <article key={item.id} className="ui-card space-y-2">
                {controls}
                <strong>{item.profileName}</strong>
                <p className="muted text-sm">
                  {item.action === 'CREATED'
                    ? 'Créé'
                    : item.action === 'UPDATED'
                      ? 'Modifié'
                      : item.action === 'DELETED'
                        ? 'Supprimé'
                        : item.action}{' '}
                  par {item.actorName} · version {item.version}
                </p>
                <time className="muted text-xs" dateTime={item.createdAt}>
                  {new Date(item.createdAt).toLocaleString('fr-FR')}
                </time>
                <details>
                  <summary className="cursor-pointer accent-text text-sm">
                    Voir les réglages enregistrés
                  </summary>
                  <HistoryChangeDetails value={item.snapshotJson} snapshot />
                </details>
              </article>
            )}
          </HistorySelection>
          {profiles.data && (
            <Pagination page={profilePage} total={profiles.data.total} onChange={setProfilePage} />
          )}
        </div>
      </details>
    </div>
  );
}
