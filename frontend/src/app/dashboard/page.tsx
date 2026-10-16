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
} from 'lucide-react';
import { useAuth } from '@/components/auth-context';
import { Dashboard } from '@/services/api';
import { useApi } from '@/services/use-api';
import { Button } from '@/components/ui/button';
import { LoadState } from '@/components/ui/load-state';
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
      note: 'Dans votre espace clients',
      href: '/client-workspace?section=statements',
      permission: 'documents.read',
    },
    {
      label: 'Extraits disponibles',
      value: data?.extracts,
      icon: FileSearch,
      tone: 'blue',
      note: 'Extraits bancaires convertis',
      href: '/client-workspace?section=extracts',
      permission: 'documents.read',
    },
    {
      label: 'Exports des relevés',
      value: data?.statementExports,
      icon: FileOutput,
      tone: 'teal',
      note: 'Fichiers CSV et Sage',
      href: '/client-workspace?section=statements',
      permission: 'documents.read',
    },
    {
      label: 'Exports des extraits',
      value: data?.extractExports,
      icon: FileOutput,
      tone: 'teal',
      note: 'Fichiers CSV et Sage',
      href: '/client-workspace?section=extracts',
      permission: 'documents.read',
    },
    {
      label: 'Clients',
      value: data?.clients,
      icon: Users,
      tone: 'violet',
      note: 'Dossiers dans votre espace',
      href: '/clients',
      permission: 'clients.read',
    },
    {
      label: 'Comptes bancaires',
      value: data?.accounts,
      icon: Wallet,
      tone: 'amber',
      note: `${number(data?.banks)} banques référencées`,
      href: '/bank-accounts',
      permission: 'accounts.read',
    },
  ];
  return (
    <div className="ui-page overview-page">
      <header className="overview-header">
        <div className="overview-welcome">
          <p className="overview-eyebrow">
            <i />
            TABLEAU DE BORD
          </p>
          <h1>
            Vue d’ensemble<span>.</span>
          </h1>
          <p>
            Bonjour{user?.name ? ` ${user.name.split(' ')[0]}` : ''}, retrouvez votre activité et
            vos derniers relevés.
          </p>
          <DashboardModelDownloads />
        </div>
        <DashboardPromptActions />
      </header>
      <LoadState loading={loading} error={error} />
      {error && (
        <Button variant="outline" onClick={reload}>
          Réessayer
        </Button>
      )}
      <section className="overview-indicators" aria-label="Indicateurs de votre activité">
        <div className="overview-section-intro">
          <div>
            <h2>Votre activité en chiffres</h2>
            <p>Documents, exports et dossiers de votre espace.</p>
          </div>
          {data?.updatedAt && (
            <span className="overview-freshness">
              <span className="overview-dot teal" />
              Actualisé à{' '}
              {new Date(data.updatedAt).toLocaleTimeString('fr-FR', {
                hour: '2-digit',
                minute: '2-digit',
              })}
            </span>
          )}
        </div>
        <div className="overview-metrics">
          {metrics.map(({ label, value, icon: Icon, tone, note, href, permission }) => {
            const content = (
              <>
                <div className="overview-metric-top">
                  <span className={`overview-icon ${tone}`}>
                    <Icon size={21} />
                  </span>
                  <h2>{label}</h2>
                  {can(permission) && <ArrowUpRight size={16} />}
                </div>
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
      {can('documents.read') && (
        <>
          {data && !error && (
            <section className="overview-review-strip" aria-label="À suivre">
              <span className={`overview-icon ${data.pendingReview ? 'amber' : 'teal'}`}>
                {data.pendingReview ? <Clock3 size={21} /> : <CheckCircle2 size={21} />}
              </span>
              <div>
                <h2>
                  {data.pendingReview
                    ? `${number(data.pendingReview)} relevé(s) à vérifier`
                    : 'Aucun relevé en attente de vérification'}
                </h2>
                <p>
                  {data.unassigned
                    ? `${number(data.unassigned)} relevé(s) à rattacher à un compte bancaire.`
                    : 'Retrouvez vos relevés et les exports associés dans l’espace clients.'}
                </p>
              </div>
              <Link href="/client-workspace">
                Ouvrir les relevés
                <ArrowRight size={16} />
              </Link>
            </section>
          )}
          <div className="overview-charts">
            <section
              className="overview-card overview-activity"
              aria-label="Activité des conversions"
            >
              <div className="overview-card-heading">
                <div>
                  <h2>
                    <Activity size={18} />
                    Activité des conversions
                  </h2>
                  <p>Les imports et exports de votre espace.</p>
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
                  <strong>{data ? number(imports) : '—'}</strong>
                </div>
                <div>
                  <span>
                    <i className="overview-dot teal" />
                    Exports
                  </span>
                  <strong>{data ? number(exports) : '—'}</strong>
                </div>
                <span className="overview-period-note">
                  <CalendarDays size={14} />
                  {period} derniers jours
                </span>
              </div>
              {data && !error ? (
                <ActivityChart key={period} data={activity} />
              ) : (
                <div className="overview-chart-placeholder" aria-hidden="true" />
              )}
            </section>
            <section
              className="overview-card overview-status"
              aria-label="Statuts des relevés et extraits"
            >
              <div className="overview-card-heading">
                <div>
                  <h2>État des relevés et extraits</h2>
                  <p>La répartition de vos documents bancaires.</p>
                </div>
                <span className="overview-icon blue">
                  <FileText size={18} />
                </span>
              </div>
              {data && !error ? (
                <StatusChart statuses={statuses} />
              ) : (
                <div className="overview-chart-placeholder" aria-hidden="true" />
              )}
            </section>
          </div>
        </>
      )}
      <div className="overview-bottom">
        {can('documents.read') && (
          <section className="overview-card overview-recent" aria-label="Relevés récents">
            <div className="overview-card-heading">
              <div>
                <h2>Derniers relevés</h2>
                <p>Reprenez une conversion en un clic.</p>
              </div>
              <Link href="/client-workspace" className="overview-text-link">
                Tout voir
                <ArrowUpRight size={15} />
              </Link>
            </div>
            {!loading && !error && !data?.recent.length && (
              <div className="overview-empty">
                <FolderOpen size={30} />
                <h3>Votre premier relevé vous attend.</h3>
                <p>Importez un PDF pour commencer une conversion.</p>
                <Link href="/conversions" className="overview-text-link">
                  Ouvrir les conversions <ArrowRight size={16} />
                </Link>
              </div>
            )}
            {!!data?.recent.length && (
              <div className="overview-recent-list">
                <div className="overview-table-head">
                  <span>Relevé / client</span>
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
                        <FileText size={20} />
                      </span>
                      <div>
                        <strong>
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
                    <span className="overview-row-count">{number(item.transactionCount)}</span>
                    <time dateTime={item.createdAt}>
                      {new Date(item.createdAt).toLocaleDateString('fr-FR', {
                        day: '2-digit',
                        month: 'short',
                      })}
                    </time>
                    <ArrowUpRight size={16} />
                  </Link>
                ))}
              </div>
            )}
          </section>
        )}
        <aside className="overview-card overview-shortcuts" aria-label="Accès rapides">
          <div className="overview-card-heading">
            <div>
              <h2>Accès rapides</h2>
              <p>Votre espace, à portée de main.</p>
            </div>
          </div>
          {[
            {
              href: '/conversions',
              permission: 'documents.read',
              icon: ArrowRightLeft,
              label: 'Conversion & export',
              note: 'Lire, corriger et exporter.',
            },
            {
              href: '/client-workspace',
              permission: 'documents.read',
              icon: FolderOpen,
              label: 'Espace clients',
              note: 'Relevés et fichiers archivés.',
            },
            {
              href: '/templates',
              permission: 'templates.read',
              icon: FileOutput,
              label: 'Modèles d’exportation',
              note: 'Vos formats CSV et Sage.',
            },
          ]
            .filter((item) => can(item.permission))
            .map(({ href, icon: Icon, label, note }) => (
              <Link key={href} href={href} className="overview-shortcut">
                <span className="overview-icon blue">
                  <Icon size={19} />
                </span>
                <div>
                  <strong>{label}</strong>
                  <p>{note}</p>
                </div>
                <ArrowUpRight size={16} />
              </Link>
            ))}
          <div className="overview-update">
            <span className="overview-dot teal" />
            {data?.updatedAt
              ? `Actualisé à ${new Date(data.updatedAt).toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit' })}`
              : 'Votre espace de travail'}
          </div>
        </aside>
      </div>
    </div>
  );
}
