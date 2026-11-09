'use client';
import { useState } from 'react';
import Link from 'next/link';
import {
  Users,
  Wallet,
  ArrowUpRight,
  ArrowRight,
  ArrowRightLeft,
  FileOutput,
  FolderOpen,
  FileText,
  FileSearch,
  CheckCircle2,
  Clock3,
  CalendarDays,
  Activity,
  Plus,
  LayoutDashboard,
} from 'lucide-react';
import { useAuth } from '@/components/auth-context';
import { Dashboard } from '@/services/api';
import { useApi } from '@/services/use-api';
import { Button } from '@/components/ui/button';
import { LoadState } from '@/components/ui/load-state';
import { PageHeader } from '@/components/page-header';
import { ActivityChart, StatusChart, statusInfo } from '@/components/dashboard-charts';
import {
  DashboardModelDownloads,
  DashboardPromptActions,
} from '@/components/conversion-prompt-resources';

const number = (value: number | undefined) => value?.toLocaleString('fr-FR') ?? '—';
export default function DashboardPage() {
  const { data, loading, error, reload } = useApi<Dashboard>('/dashboard');
  const { can, user } = useAuth();
  const [period, setPeriod] = useState(30);
  const activity = (data?.activity ?? []).slice(-period);
  const imports = activity.reduce((sum, day) => sum + day.imports, 0);
  const exports = activity.reduce((sum, day) => sum + day.exports, 0);
  const statuses = (data?.statuses ?? [])
    .slice()
    .sort((a, b) =>
      a.status === 'EXPORTED' ? -1 : b.status === 'EXPORTED' ? 1 : b.count - a.count,
    );
  const metrics = [
    {
      label: 'Relevés disponibles',
      value: data?.statementDocuments,
      icon: FileText,
      tone: 'blue',
      note: 'Documents de votre espace',
      href: '/client-workspace?section=statements',
      permission: 'documents.read',
    },
    {
      label: 'Extraits disponibles',
      value: data?.extracts,
      icon: FileSearch,
      tone: 'teal',
      note: 'Extraits bancaires importés',
      href: '/client-workspace?section=extracts',
      permission: 'documents.read',
    },
    {
      label: 'Clients',
      value: data?.clients,
      icon: Users,
      tone: 'violet',
      note: 'Dossiers clients enregistrés',
      href: '/clients',
      permission: 'clients.read',
    },
    {
      label: 'Comptes bancaires',
      value: data?.accounts,
      icon: Wallet,
      tone: 'amber',
      note: 'Comptes configurés',
      href: '/bank-accounts',
      permission: 'accounts.read',
    },
  ];
  const shortcuts = [
    {
      href: '/client-workspace',
      permission: 'documents.read',
      icon: FolderOpen,
      label: 'Espace clients',
      note: 'Retrouver vos documents',
    },
    {
      href: '/conversions',
      permission: 'documents.read',
      icon: ArrowRightLeft,
      label: 'Conversions',
      note: 'Reprendre un traitement',
    },
    {
      href: '/templates',
      permission: 'templates.read',
      icon: FileOutput,
      label: 'Modèles d’exportation',
      note: 'Configurer vos formats',
    },
    {
      href: '/history',
      permission: 'history.read',
      icon: Clock3,
      label: 'Historique',
      note: 'Consulter les dernières actions',
    },
  ].filter((item) => can(item.permission));
  return (
    <div className="ui-page overview-page dashboard-overview">
      <PageHeader
        eyebrow="Tableau de bord"
        title="Vue d’ensemble"
        description={`Bonjour${user?.name ? ` ${user.name.split(' ')[0]}` : ''}, voici l’essentiel de votre espace bancaire.`}
        icon={LayoutDashboard}
        actions={
          can('documents.read') && (
            <section className="dashboard-heading-resources" aria-label="Ressources de conversion">
              <DashboardPromptActions />
              <DashboardModelDownloads />
            </section>
          )
        }
      />
      <LoadState loading={loading} error={error} />
      {error && (
        <Button variant="outline" size="sm" onClick={reload}>
          Réessayer
        </Button>
      )}
      <section className="overview-indicators" aria-label="Indicateurs de votre activité">
        <div className="dashboard-summary-heading">
          <h2>Votre espace en chiffres</h2>
          {data?.updatedAt && (
            <span className="overview-freshness">
              <span className="overview-dot teal" />
              Actualisé à{' '}
              {new Date(data.updatedAt).toLocaleTimeString('fr-FR', {
                hour: '2-digit',
                minute: '2-digit',
                timeZone: 'UTC',
              })}{' '}
              UTC
            </span>
          )}
        </div>
        <div className="overview-metrics" aria-busy={loading}>
          {metrics.map(({ label, value, icon: Icon, tone, note, href, permission }) => {
            const content = (
              <>
                <div className="overview-metric-top">
                  <span className={`overview-icon ${tone}`}>
                    <Icon size={19} />
                  </span>
                  {can(permission) && <ArrowUpRight size={15} />}
                </div>
                <h2>{label}</h2>
                <p className="overview-metric-value">{number(value)}</p>
                <p className="overview-metric-note">{note}</p>
              </>
            );
            return can(permission) ? (
              <Link key={label} href={href} className="overview-card overview-metric">
                {content}
              </Link>
            ) : (
              <article key={label} className="overview-card overview-metric">
                {content}
              </article>
            );
          })}
        </div>
      </section>
      {can('documents.read') && data && !error && (
        <>
          <section className="dashboard-analysis" aria-label="Analyse de l’activité">
            <div className="dashboard-summary-heading">
              <div>
                <h2>Activité de votre espace</h2>
                <p>Vos conversions et l’état de vos documents bancaires.</p>
              </div>
            </div>
            <div className="overview-charts">
              <section
                className="overview-card overview-activity"
                aria-label="Activité des conversions"
              >
                <div className="overview-card-heading">
                  <div>
                    <h2>
                      <Activity size={17} />
                      Imports et exports
                    </h2>
                    <p>Volumes quotidiens sur la période choisie.</p>
                  </div>
                  <div className="overview-period" role="group" aria-label="Période du graphique">
                    {[7, 30, 90].map((days) => (
                      <button
                        key={days}
                        type="button"
                        aria-pressed={period === days}
                        onClick={() => setPeriod(days)}
                      >
                        {days} jours
                      </button>
                    ))}
                  </div>
                </div>
                <div className="overview-chart-totals">
                  <div>
                    <span>
                      <i className="overview-dot blue" />
                      Imports
                    </span>
                    <strong>{number(imports)}</strong>
                  </div>
                  <div>
                    <span>
                      <i className="overview-dot teal" />
                      Exports
                    </span>
                    <strong>{number(exports)}</strong>
                  </div>
                  <span className="overview-period-note">
                    <CalendarDays size={14} />
                    {period} derniers jours
                  </span>
                </div>
                <ActivityChart key={period} data={activity} />
              </section>
              <div className="dashboard-analysis-summary">
                <section
                  className="overview-card overview-status"
                  aria-label="Statuts des relevés et extraits"
                >
                  <div className="overview-card-heading">
                    <div>
                      <h2>État des documents</h2>
                      <p>Répartition de vos relevés et extraits.</p>
                    </div>
                  </div>
                  <StatusChart statuses={statuses} />
                </section>
                <section className="dashboard-priorities" aria-label="Documents à suivre">
                  <div className="dashboard-priority-label">
                    <span>À SUIVRE</span>
                    {data.pendingReview ? <Clock3 size={18} /> : <CheckCircle2 size={18} />}
                  </div>
                  <div className="dashboard-priority-count">
                    <strong>{number(data.pendingReview)}</strong>
                    <h2>
                      {data.pendingReview === 1 ? 'document à vérifier' : 'documents à vérifier'}
                    </h2>
                  </div>
                  <p>
                    {data.pendingReview
                      ? 'Contrôlez les informations avant de finaliser vos conversions.'
                      : 'Aucun document en attente de vérification.'}
                  </p>
                  <div className="dashboard-unassigned">
                    <span>
                      <FolderOpen size={15} />
                      Documents à classer
                    </span>
                    <strong>{number(data.unassigned)}</strong>
                  </div>
                  <Link href="/client-workspace">
                    Ouvrir l’espace clients
                    <ArrowRight size={15} />
                  </Link>
                </section>
              </div>
            </div>
          </section>
          <div className="dashboard-workspace-grid">
            <section className="overview-card overview-recent" aria-label="Documents récents">
              <div className="overview-card-heading">
                <div>
                  <p className="dashboard-section-eyebrow">VOTRE ESPACE DE TRAVAIL</p>
                  <h2>Derniers documents</h2>
                  <p>Reprenez un relevé ou un extrait là où vous l’avez laissé.</p>
                </div>
                <Link href="/client-workspace" className="overview-text-link">
                  Tout voir
                  <ArrowUpRight size={15} />
                </Link>
              </div>
              {!data.recent.length ? (
                <div className="overview-empty">
                  <span className="dashboard-empty-icon">
                    <FolderOpen size={28} />
                  </span>
                  <h3>Votre premier document vous attend</h3>
                  <p>Les relevés et extraits importés apparaîtront ici.</p>
                  {can('documents.write') && (
                    <Button size="sm" asChild>
                      <Link href="/conversions?import=1">
                        <Plus size={15} />
                        Importer un PDF
                      </Link>
                    </Button>
                  )}
                </div>
              ) : (
                <div className="overview-recent-list">
                  <div className="overview-table-head">
                    <span>Document / client</span>
                    <span>Statut</span>
                    <span>Mouvements</span>
                    <span>Ajouté le</span>
                    <span />
                  </div>
                  {data.recent.map((item) => (
                    <Link
                      key={item.id}
                      href={`/conversions?statementId=${encodeURIComponent(item.id)}`}
                      className="overview-recent-row"
                    >
                      <div className="overview-recent-file">
                        <span className="overview-file-icon">
                          <FileText size={18} />
                        </span>
                        <div>
                          <strong
                            title={item.sourceFileName || item.statementNumber || item.bankName}
                          >
                            {item.sourceFileName || item.statementNumber || item.bankName}
                          </strong>
                          <p>
                            {item.clientName || 'À classer'} · {item.bankName}
                          </p>
                        </div>
                      </div>
                      <span className="overview-status-badge" data-status={item.status}>
                        {statusInfo(item.status)
                          .label.replace('Exportés', 'Exporté')
                          .replace('Validés', 'Validé')}
                      </span>
                      <span
                        className="overview-row-count"
                        aria-label={`${item.transactionCount} mouvements`}
                      >
                        {number(item.transactionCount)}
                      </span>
                      <time
                        dateTime={item.createdAt}
                        title={
                          new Date(item.createdAt).toLocaleString('fr-FR', { timeZone: 'UTC' }) +
                          ' UTC'
                        }
                      >
                        {new Date(item.createdAt).toLocaleDateString('fr-FR', {
                          day: '2-digit',
                          month: 'short',
                          timeZone: 'UTC',
                        })}
                      </time>
                      <ArrowUpRight size={15} />
                    </Link>
                  ))}
                </div>
              )}
            </section>
            <aside className="dashboard-sidebar" aria-label="À suivre et accès rapides">
              {!!shortcuts.length && (
                <nav className="overview-card dashboard-quick-links" aria-label="Accès rapides">
                  <h2>Accès rapides</h2>
                  {shortcuts.map(({ href, icon: Icon, label, note }) => (
                    <Link key={href} href={href}>
                      <span className="dashboard-shortcut-icon">
                        <Icon size={16} />
                      </span>
                      <div>
                        <strong>{label}</strong>
                        <span>{note}</span>
                      </div>
                      <ArrowUpRight size={14} />
                    </Link>
                  ))}
                </nav>
              )}
            </aside>
          </div>
        </>
      )}
      {!can('documents.read') && !!shortcuts.length && (
        <nav className="dashboard-restricted-links" aria-label="Accès rapides">
          {shortcuts.map(({ href, label, icon: Icon }) => (
            <Link key={href} href={href}>
              <Icon size={17} />
              {label}
              <ArrowUpRight size={14} />
            </Link>
          ))}
        </nav>
      )}
    </div>
  );
}
