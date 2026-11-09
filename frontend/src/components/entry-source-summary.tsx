'use client';

import { BookOpen, CheckCircle2, Clock3 } from 'lucide-react';
import { useAll } from '@/services/use-api';
import type { Account, Client } from '@/services/api';
import { date, money, type StatementDetail } from '@/services/statement-exports';

export function EntrySourceSummary({
  detail,
  clientId,
  modelName,
  accountCode,
}: {
  detail: StatementDetail;
  clientId: string;
  modelName?: string;
  accountCode?: string;
}) {
  const clients = useAll<Client>('/clients');
  const accounts = useAll<Account>('/bank-accounts');
  const client = clients.data?.find((item) => item.id === clientId);
  const account = accounts.data?.find(
    (item) => item.id === detail.bankAccountId && item.clientId === clientId,
  );
  const source = detail.statement;
  const validated = detail.status === 'VALIDATED';
  const number = account?.accountNumber || source.accountNumber;
  return (
    <section
      className="ui-card entry-source-summary"
      aria-label="Informations des écritures comptables"
    >
      <div className="entry-source-heading">
        <span className="entry-source-icon">
          <BookOpen size={24} />
        </span>
        <div className="entry-source-title">
          <p className="conversion-eyebrow">Écritures comptables</p>
          <h2>{account?.bank || source.bankName || 'Banque non renseignée'}</h2>
          <p className="muted">
            {source.statementNumber || detail.sourceFileName || 'Document bancaire'}
            {number ? ` · •••• ${number.replace(/\s/g, '').slice(-4)}` : ''}
          </p>
        </div>
        <span className={`conversion-status ${validated ? 'validated' : 'review'}`}>
          {validated ? <CheckCircle2 size={14} /> : <Clock3 size={14} />}
          {validated ? 'Validé' : 'À vérifier'}
        </span>
      </div>
      <dl className="entry-source-assignment">
        <div>
          <dt>Client</dt>
          <dd>{client?.name || (clients.loading ? 'Chargement…' : 'Client indisponible')}</dd>
        </div>
        <div>
          <dt>Compte bancaire</dt>
          <dd>
            {account?.accountName || (accounts.loading ? 'Chargement…' : '—')}
            <span className="entry-source-number">{number || '—'}</span>
          </dd>
        </div>
        <div>
          <dt>Compte comptable</dt>
          <dd className="entry-source-number">{accountCode || account?.accountCode || '—'}</dd>
        </div>
        <div>
          <dt>Modèle choisi</dt>
          <dd>{modelName || '—'}</dd>
        </div>
      </dl>
      <dl className="entry-source-metrics">
        <div>
          <dt>Période</dt>
          <dd>
            {date(source.periodStart)} — {date(source.periodEnd)}
          </dd>
        </div>
        <div>
          <dt>Mouvements</dt>
          <dd>{source.transactions.length}</dd>
        </div>
        <div>
          <dt>Devise</dt>
          <dd>{source.currency || '—'}</dd>
        </div>
        <div>
          <dt>Version</dt>
          <dd>{detail.version}</dd>
        </div>
      </dl>
      <dl className="entry-source-balances">
        <div>
          <dt>Solde initial</dt>
          <dd>
            {money(source.openingBalance)} <small>{source.currency}</small>
          </dd>
        </div>
        <div>
          <dt>Solde final</dt>
          <dd>
            {money(source.closingBalance)} <small>{source.currency}</small>
          </dd>
        </div>
      </dl>
      {(clients.error || accounts.error) && (
        <p className="muted text-sm" role="status">
          Certaines informations du dossier sont indisponibles. Les données du document restent
          affichées.
        </p>
      )}
    </section>
  );
}
