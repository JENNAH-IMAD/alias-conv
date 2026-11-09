'use client';
import { useState } from 'react';
import { AlertTriangle, ShieldCheck, Info, ChevronDown } from 'lucide-react';
import type { ExportDiagnostic } from '@/services/api';

const diagnosticFieldNames: Record<string, string> = {
  Date: 'Date opération',
  ValueDate: 'Date valeur',
  Description: 'Libellé',
  Amount: 'Montant',
  Debit: 'Débit',
  Credit: 'Crédit',
  Direction: 'Sens',
  OpeningBalance: 'Solde initial',
  ClosingBalance: 'Solde final',
  TotalDebit: 'Total débit',
  TotalCredit: 'Total crédit',
  Currency: 'Devise',
  StatementNumber: 'Numéro du relevé',
  AccountNumber: 'Compte bancaire',
  BankCode: 'Code banque',
  Journal: 'Journal',
  Account: 'Compte comptable',
  Balance: 'Solde',
  Sequence: 'Numéro de ligne',
  Reference: 'Référence',
};

export function StatementDiagnostics({
  errors,
  warnings,
  onTransaction,
  onStatement,
}: {
  errors: ExportDiagnostic[];
  warnings: ExportDiagnostic[];
  onTransaction?: (sequence: number) => void;
  onStatement?: () => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const countLabel = (count: number, single: string, plural: string) =>
    `${count} ${count === 1 ? single : plural}`;
  function issueItem(issue: ExportDiagnostic, blocking: boolean, index: number) {
    const field =
      issue.fieldName ||
      (issue.sourceField ? (diagnosticFieldNames[issue.sourceField] ?? issue.sourceField) : '');
    return (
      <li
        key={`${issue.code}-${issue.transactionSequence}-${index}`}
        className={blocking ? 'error' : 'warning'}
      >
        <span className="conversion-diagnostic-mark" aria-hidden="true">
          {blocking ? '!' : <Info size={14} />}
        </span>
        <div>
          <div className="diagnostic-issue-heading">
            <strong>
              {issue.transactionSequence ? `Mouvement ${issue.transactionSequence}` : 'Document'}
              {field ? ` · ${field}` : ''}
            </strong>
            {issue.transactionSequence && onTransaction ? (
              <button
                type="button"
                className="conversion-text-action"
                onClick={() => onTransaction(issue.transactionSequence!)}
              >
                Corriger
              </button>
            ) : blocking && onStatement ? (
              <button type="button" className="conversion-text-action" onClick={onStatement}>
                Corriger
              </button>
            ) : null}
          </div>
          <p>{issue.message}</p>
          <details className="diagnostic-technical">
            <summary>Référence du contrôle</summary>
            <code>{issue.code}</code>
          </details>
        </div>
      </li>
    );
  }
  if (!errors.length && !warnings.length)
    return (
      <div className="conversion-clean">
        <ShieldCheck size={18} />
        <span>Aucune anomalie détectée dans les données contrôlées.</span>
      </div>
    );
  return (
    <section className="conversion-diagnostics" aria-label="Diagnostics du relevé">
      <div className="conversion-section-heading">
        <h3>
          {errors.length ? <AlertTriangle size={17} /> : <ShieldCheck size={17} />}
          {errors.length ? 'Corrections nécessaires' : 'Contrôle du document'}
        </h3>
        <span>
          {errors.length
            ? countLabel(errors.length, 'correction', 'corrections')
            : 'Aucune erreur bloquante'}
        </span>
      </div>
      {errors.length > 0 && (
        <ul>
          {(expanded ? errors : errors.slice(0, 2)).map((issue, index) =>
            issueItem(issue, true, index),
          )}
        </ul>
      )}
      {errors.length > 2 && (
        <button
          type="button"
          className="conversion-text-action mt-3"
          aria-expanded={expanded}
          onClick={() => setExpanded(!expanded)}
        >
          {expanded ? 'Réduire' : `Voir les ${errors.length} corrections`}
        </button>
      )}
      {warnings.length > 0 && (
        <details className="diagnostic-warnings">
          <summary>
            <Info size={15} />
            {countLabel(
              warnings.length,
              'point de contrôle complémentaire',
              'points de contrôle complémentaires',
            )}
            <ChevronDown size={15} />
          </summary>
          <ul>{warnings.map((issue, index) => issueItem(issue, false, index))}</ul>
        </details>
      )}
    </section>
  );
}
