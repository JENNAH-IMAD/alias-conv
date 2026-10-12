'use client';
import { useState } from 'react';
import { Columns3 } from 'lucide-react';
import { BankDialog } from './bank-dialogs';
import { Button } from './ui/button';
import type { StatementTransaction } from '@/services/statement-exports';
import { editTransactionValue, type EditableTransactionField } from '@/lib/statement-row-editing';

const columns: {
  key: EditableTransactionField;
  label: string;
  kind?: 'date' | 'money' | 'direction' | 'currency';
}[] = [
  { key: 'date', label: 'Date d’opération', kind: 'date' },
  { key: 'valueDate', label: 'Date de valeur', kind: 'date' },
  { key: 'description', label: 'Libellé' },
  { key: 'reference', label: 'Référence' },
  { key: 'debit', label: 'Débit', kind: 'money' },
  { key: 'credit', label: 'Crédit', kind: 'money' },
  { key: 'amount', label: 'Montant', kind: 'money' },
  { key: 'direction', label: 'Sens', kind: 'direction' },
  { key: 'balance', label: 'Solde après mouvement', kind: 'money' },
  { key: 'currency', label: 'Devise', kind: 'currency' },
  { key: 'journal', label: 'Journal' },
  { key: 'account', label: 'Compte comptable' },
  { key: 'analytic', label: 'Analytique' },
  { key: 'recordType', label: 'Type de ligne' },
];

export function StatementColumnEditor({
  rows,
  selected,
  onClose,
  onApply,
}: {
  rows: StatementTransaction[];
  selected: Set<number>;
  onClose: () => void;
  onApply: (
    field: EditableTransactionField,
    value: string | null,
    synchronize: boolean,
    scope: 'all' | 'selected',
  ) => void;
}) {
  const [field, setField] = useState<EditableTransactionField>('description');
  const [scope, setScope] = useState<'all' | 'selected'>(selected.size ? 'selected' : 'all');
  const [mode, setMode] = useState<'replace' | 'clear'>('replace');
  const [value, setValue] = useState('');
  const [synchronize, setSynchronize] = useState(true);
  const column = columns.find((c) => c.key === field)!;
  const affected = scope === 'all' ? rows : rows.filter((row) => selected.has(row.sequence));
  const applied = mode === 'clear' ? null : value;
  const coupled = ['debit', 'credit', 'amount', 'direction'].includes(field);
  return (
    <BankDialog title="Modifier une colonne" onClose={onClose} wide>
      <form
        className="space-y-5"
        onSubmit={(event) => {
          event.preventDefault();
          if (!affected.length) return;
          onApply(field, applied, synchronize, scope);
          onClose();
        }}
      >
        <div className="row-edit-intro">
          <Columns3 size={22} />
          <p>
            Remplacez une valeur sur les lignes choisies. La modification s’applique au brouillon du
            relevé et sera enregistrée avec vos corrections.
          </p>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <label>
            Colonne à modifier
            <select
              aria-label="Colonne à modifier"
              className="ui-input mt-2"
              value={field}
              onChange={(e) => {
                setField(e.target.value as EditableTransactionField);
                setValue('');
              }}
            >
              {columns.map((c) => (
                <option key={c.key} value={c.key}>
                  {c.label}
                </option>
              ))}
            </select>
          </label>
          <label>
            Lignes concernées
            <select
              aria-label="Lignes concernées"
              className="ui-input mt-2"
              value={scope}
              onChange={(e) => setScope(e.target.value as 'all' | 'selected')}
            >
              <option value="all">Toutes les lignes ({rows.length}), toutes les pages</option>
              <option value="selected" disabled={!selected.size}>
                Lignes sélectionnées ({selected.size})
              </option>
            </select>
          </label>
          <label>
            Action
            <select
              aria-label="Action sur la colonne"
              className="ui-input mt-2"
              value={mode}
              onChange={(e) => setMode(e.target.value as 'replace' | 'clear')}
            >
              <option value="replace">Remplacer les valeurs</option>
              <option value="clear">Vider les valeurs</option>
            </select>
          </label>
          {mode === 'replace' && (
            <label>
              Nouvelle valeur
              {column.kind === 'direction' ? (
                <select
                  required
                  aria-label="Nouvelle valeur"
                  className="ui-input mt-2"
                  value={value}
                  onChange={(e) => setValue(e.target.value)}
                >
                  <option value="">Choisir un sens…</option>
                  <option value="DEBIT">Débit</option>
                  <option value="CREDIT">Crédit</option>
                  <option value="UNKNOWN">Non déterminé</option>
                </select>
              ) : (
                <input
                  required
                  aria-label="Nouvelle valeur"
                  className="ui-input mt-2"
                  type={column.kind === 'date' ? 'date' : 'text'}
                  inputMode={column.kind === 'money' ? 'decimal' : undefined}
                  pattern={
                    column.kind === 'money'
                      ? field === 'debit' || field === 'credit'
                        ? '[0-9]+([.][0-9]+)?'
                        : '-?[0-9]+([.][0-9]+)?'
                      : column.kind === 'currency'
                        ? '[A-Z]{3}'
                        : undefined
                  }
                  maxLength={column.kind === 'currency' ? 3 : 10000}
                  value={value}
                  onChange={(e) =>
                    setValue(
                      column.kind === 'money'
                        ? e.target.value.replace(',', '.')
                        : column.kind === 'currency'
                          ? e.target.value.toUpperCase()
                          : e.target.value,
                    )
                  }
                />
              )}
            </label>
          )}
        </div>
        {coupled && (
          <label className="bulk-sync-option">
            <input
              type="checkbox"
              checked={synchronize}
              onChange={(e) => setSynchronize(e.target.checked)}
            />
            <span>
              Mettre le montant, le sens et les colonnes débit/crédit en cohérence avec cette
              valeur.
            </span>
          </label>
        )}
        <section className="bulk-preview" aria-label="Aperçu de la modification">
          <strong>
            {affected.length} ligne(s) concernée(s) · aperçu des {Math.min(5, affected.length)}{' '}
            premières
          </strong>
          <div className="bulk-preview-scroll">
            <table>
              <thead>
                <tr>
                  <th>Ligne</th>
                  <th>Avant</th>
                  <th>Après</th>
                </tr>
              </thead>
              <tbody>
                {affected.slice(0, 5).map((row) => (
                  <tr key={row.sequence}>
                    <td>{row.sequence}</td>
                    <td>{String(row[field] ?? 'Non renseigné')}</td>
                    <td>
                      {mode === 'replace' && !value
                        ? 'Saisissez une valeur'
                        : String(
                            editTransactionValue(row, field, applied, synchronize)[field] ??
                              'Non renseigné',
                          )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
        <p className="muted text-xs">
          Les soldes et totaux du relevé seront contrôlés lors de la validation. Les numéros uniques
          des mouvements restent conservés.
        </p>
        <div className="flex justify-end gap-3">
          <Button type="button" variant="outline" onClick={onClose}>
            Annuler
          </Button>
          <Button disabled={!affected.length}>Appliquer aux {affected.length} lignes</Button>
        </div>
      </form>
    </BankDialog>
  );
}
