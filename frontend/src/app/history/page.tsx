'use client';
import { Suspense, useState } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { History, RefreshCw, ArrowUpRight } from 'lucide-react';
import { PageHeader } from '@/components/page-header';
import { Button } from '@/components/ui/button';
import { LoadState } from '@/components/ui/load-state';
import { Pagination } from '@/components/ui/pagination';
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
function formattedJson(value: string) {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}
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
  for (const key of ['statementId', 'clientId', 'bankAccountId']) {
    const value = params.get(key);
    if (value) query.set(key, value);
  }
  const history = useApi<Page<Activity>>(`/client-workspace/history?${query}`);
  const profiles = useApi<Page<ProfileActivity>>(
    `/extraction-profiles/history?page=${profilePage}&pageSize=20`,
  );
  return (
    <div className="ui-page">
      <PageHeader
        icon={History}
        eyebrow="Suivi de l’activité"
        title="Historique"
        description="Retrouvez les actions sur les relevés, les conversions et les modèles d’extraction."
      />
      <section className="ui-card space-y-5" aria-label="Historique des relevés">
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
        {history.data?.items.map((item) => (
          <article key={item.id} className="ui-card space-y-3">
            <div className="flex flex-wrap justify-between gap-3">
              <strong>{actionNames[item.action] || item.action}</strong>
              <time className="muted text-xs" dateTime={item.createdAt}>
                {new Date(item.createdAt).toLocaleString('fr-FR')}
              </time>
            </div>
            <div>
              <h3 className="font-semibold break-all">{item.fileName || item.bankName}</h3>
              <p className="muted text-sm">
                {item.clientName || 'Relevé à classer'} · {item.bankName}
                {item.accountName ? ` · ${item.accountName}` : ''}
              </p>
            </div>
            <p className="text-sm">
              Par <strong>{item.actorName || 'Application'}</strong> · version {item.version}
            </p>
            {item.action !== 'DELETED' && (
              <Link
                className="accent-text inline-flex items-center gap-2 text-sm"
                href={`/conversions?statementId=${encodeURIComponent(item.statementId)}`}
              >
                Ouvrir le relevé
                <ArrowUpRight size={14} />
              </Link>
            )}
            {item.changesJson && (
              <details>
                <summary className="cursor-pointer text-sm accent-text">
                  Voir les modifications
                </summary>
                <pre className="mt-3 max-h-80 overflow-auto rounded-lg bg-muted p-3 text-xs whitespace-pre-wrap break-all">
                  {formattedJson(item.changesJson)}
                </pre>
              </details>
            )}
          </article>
        ))}
        {history.data && <Pagination page={page} total={history.data.total} onChange={setPage} />}
      </section>
      <section className="ui-card space-y-5" aria-label="Historique des modèles d’extraction">
        <h2 className="text-lg font-semibold">Historique des modèles d’extraction</h2>
        <LoadState loading={profiles.loading} error={profiles.error} />
        {!profiles.loading && !profiles.error && !profiles.data?.items.length && (
          <p className="muted text-sm">Aucune modification de modèle enregistrée.</p>
        )}
        {profiles.data?.items.map((item) => (
          <article key={item.id} className="ui-card space-y-2">
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
              <pre className="mt-3 max-h-80 overflow-auto rounded-lg bg-muted p-3 text-xs whitespace-pre-wrap break-all">
                {formattedJson(item.snapshotJson)}
              </pre>
            </details>
          </article>
        ))}
        {profiles.data && (
          <Pagination page={profilePage} total={profiles.data.total} onChange={setProfilePage} />
        )}
      </section>
    </div>
  );
}
