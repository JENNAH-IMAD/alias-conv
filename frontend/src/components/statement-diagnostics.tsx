'use client';
import { useState } from 'react';
import { AlertTriangle, ShieldCheck } from 'lucide-react';
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
  const items = [
    ...errors.map((e) => ({ ...e, blocking: true })),
    ...warnings.map((e) => ({ ...e, blocking: false })),
  ];
  if (!items.length)
    return (
      <div className="conversion-clean">
        <ShieldCheck size={18} />
        <span>Aucune anomalie détectée dans les données contrôlées.</span>
      </div>
    );
  return (
    <div className="conversion-diagnostics" aria-label="Diagnostics du relevé">
      <div className="conversion-section-heading">
        <h3>
          <AlertTriangle size={17} />
          Points à vérifier
        </h3>
        <span>
          {errors.length} erreur(s) · {warnings.length} avertissement(s)
        </span>
      </div>
      <ul>
        {(expanded ? items : items.slice(0, 3)).map((issue, index) => (
          <li
            key={`${issue.code}-${issue.transactionSequence}-${issue.fieldName}-${index}`}
            className={issue.blocking ? 'error' : 'warning'}
          >
            <span className="conversion-diagnostic-mark">{issue.blocking ? '!' : 'i'}</span>
            <div>
              <strong>
                {issue.transactionSequence ? `Transaction ${issue.transactionSequence}` : 'Relevé'}
                {issue.fieldName
                  ? ` · ${issue.fieldName}`
                  : issue.sourceField
                    ? ` · ${diagnosticFieldNames[issue.sourceField] ?? issue.sourceField}`
                    : ''}
              </strong>
              <p>{issue.message}</p>
              <small
                title={
                  issue.blocking
                    ? 'À corriger avant validation ou export'
                    : 'À vérifier avec le PDF original'
                }
              >
                {issue.code}
              </small>
            </div>
            {issue.transactionSequence && onTransaction && (
              <button
                type="button"
                className="conversion-text-action"
                onClick={() => onTransaction(issue.transactionSequence!)}
              >
                Corriger
              </button>
            )}
            {!issue.transactionSequence && onStatement && issue.blocking && (
              <button type="button" className="conversion-text-action" onClick={onStatement}>
                Corriger les informations
              </button>
            )}
          </li>
        ))}
      </ul>
      {items.length > 3 && (
        <button
          type="button"
          className="conversion-text-action mt-3"
          aria-expanded={expanded}
          onClick={() => setExpanded(!expanded)}
        >
          {expanded ? 'Réduire' : `Voir les ${items.length} diagnostics`}
        </button>
      )}
    </div>
  );
}
