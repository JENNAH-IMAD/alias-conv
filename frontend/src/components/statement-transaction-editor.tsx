'use client';
import { useState } from 'react';
import { BankDialog } from './bank-dialogs';
import { Button } from './ui/button';
import { StatementTransaction } from '@/services/statement-exports';
import { editTransactionValue, type EditableTransactionField } from '@/lib/statement-row-editing';
export function StatementTransactionEditor({
  initial,
  onClose,
  onApply,
  adding = false,
  initialPosition,
  maxPosition,
}: {
  initial: StatementTransaction;
  onClose: () => void;
  onApply: (transaction: StatementTransaction, position: number) => boolean | void;
  adding?: boolean;
  initialPosition: number;
  maxPosition: number;
}) {
  const [draft, setDraft] = useState({ ...initial });
  const [position, setPosition] = useState(String(initialPosition));
  function text(key: EditableTransactionField, label: string, type = 'text') {
    return (
      <label key={key}>
        {label}
        <input
          type={type}
          className="ui-input mt-2"
          value={String(draft[key] ?? '')}
          maxLength={key === 'currency' ? 3 : 10000}
          onChange={(e) =>
            setDraft(
              editTransactionValue(
                draft,
                key,
                key === 'currency' ? e.target.value.toUpperCase() || null : e.target.value || null,
              ),
            )
          }
        />
      </label>
    );
  }
  function amount(key: 'debit' | 'credit' | 'amount' | 'balance', label: string) {
    return (
      <label key={key}>
        {label}
        <input
          className="ui-input mt-2"
          aria-label={label}
          inputMode="decimal"
          pattern={
            key === 'debit' || key === 'credit' ? '[0-9]+([.][0-9]+)?' : '-?[0-9]+([.][0-9]+)?'
          }
          value={String(draft[key] ?? '')}
          onChange={(e) =>
            setDraft(editTransactionValue(draft, key, e.target.value.replace(',', '.') || null))
          }
        />
        <span className="muted text-xs">Vide = non renseigné</span>
      </label>
    );
  }
  return (
    <BankDialog
      title={adding ? 'Ajouter une ligne' : `Corriger la transaction ${initial.sequence}`}
      onClose={onClose}
      wide
    >
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (onApply(draft, Number(position)) === false) return;
          onClose();
        }}
        className="space-y-5"
      >
        <p className="muted text-sm">
          Renseignez les informations vérifiées dans le relevé. La saisie d’un débit ou d’un crédit
          met le montant et le sens en cohérence. Les montants conservent leur précision décimale.
        </p>
        <div className="grid gap-4 sm:grid-cols-2">
          <label>
            Emplacement de la ligne
            <input
              type="number"
              aria-label="Emplacement de la ligne"
              required
              min={1}
              max={maxPosition}
              step={1}
              className="ui-input mt-2"
              value={position}
              onChange={(e) => setPosition(e.target.value)}
            />
            <span className="muted text-xs">
              Position dans le relevé : 1 = début, {maxPosition} = fin. Les lignes suivantes se
              décalent.
            </span>
          </label>
          {text('date', 'Date d’opération', 'date')}
          {text('valueDate', 'Date de valeur', 'date')}
          {text('description', 'Libellé')}
          {text('reference', 'Référence')}
          {amount('debit', 'Débit')}
          {amount('credit', 'Crédit')}
          {amount('amount', 'Montant')}
          {amount('balance', 'Solde après mouvement')}
          <label>
            Sens
            <select
              aria-label="Sens de la transaction"
              className="ui-input mt-2"
              value={draft.direction || ''}
              onChange={(e) =>
                setDraft(editTransactionValue(draft, 'direction', e.target.value || null))
              }
            >
              <option value="">Non renseigné</option>
              <option value="DEBIT">DEBIT</option>
              <option value="CREDIT">CREDIT</option>
              <option value="UNKNOWN">UNKNOWN</option>
            </select>
          </label>
          {text('currency', 'Devise')}
        </div>
        <details className="conversion-details">
          <summary>Sources complémentaires explicites</summary>
          <div className="mt-4 grid gap-4 sm:grid-cols-2">
            {text('journal', 'Journal')}
            {text('account', 'Compte comptable')}
            {text('analytic', 'Analytique')}
            {text('recordType', 'Type de ligne')}
          </div>
        </details>
        <div className="flex justify-end gap-3">
          <Button type="button" variant="outline" onClick={onClose}>
            Annuler
          </Button>
          <Button>{adding ? 'Ajouter la ligne' : 'Appliquer la correction'}</Button>
        </div>
      </form>
    </BankDialog>
  );
}
