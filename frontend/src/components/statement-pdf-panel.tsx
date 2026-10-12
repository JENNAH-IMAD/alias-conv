'use client';
import { useState } from 'react';
import { StatementPdfViewer } from './statement-pdf-viewer';
import { Button } from './ui/button';
import { pdfFieldValue, type PdfSelection } from '@/lib/pdf-selection';
import type { StatementTransaction } from '@/services/statement-exports';
import { useAuth } from './auth-context';
import { ChevronDown, FileText } from 'lucide-react';

const fields = [
  ['statement.openingBalance', 'Solde initial', 'money'],
  ['statement.closingBalance', 'Solde final', 'money'],
  ['statement.totalDebit', 'Total débit', 'money'],
  ['statement.totalCredit', 'Total crédit', 'money'],
  ['statement.statementNumber', 'Numéro du relevé', 'text'],
  ['statement.accountNumber', 'Numéro du compte', 'text'],
  ['statement.periodStart', 'Début de période', 'date'],
  ['statement.periodEnd', 'Fin de période', 'date'],
  ['transaction.date', 'Date opération', 'date'],
  ['transaction.valueDate', 'Date valeur', 'date'],
  ['transaction.description', 'Libellé du mouvement', 'text'],
  ['transaction.reference', 'Référence du mouvement', 'text'],
  ['transaction.debit', 'Débit du mouvement', 'money'],
  ['transaction.credit', 'Crédit du mouvement', 'money'],
  ['transaction.amount', 'Montant du mouvement', 'money'],
] as const;
export function StatementPdfPanel({
  id,
  hasExtraction,
  rows,
  disabled,
  onApply,
  onSelection,
  collapsed = false,
  onToggle,
}: {
  id: string;
  hasExtraction: boolean;
  rows: StatementTransaction[];
  disabled: boolean;
  onApply: (sequence: number | null, field: string, value: string) => boolean;
  onSelection: (selection: PdfSelection | null) => void;
  collapsed?: boolean;
  onToggle?: () => void;
}) {
  const { can } = useAuth();
  const [text, setText] = useState('');
  const [target, setTarget] = useState<string>(fields[0][0]);
  const [sequence, setSequence] = useState(rows[0]?.sequence ?? 0);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const movement = target.startsWith('transaction.');
  return (
    <aside
      className={`statement-pdf-panel ${collapsed ? 'pdf-collapsed' : ''}`}
      aria-label="Consultation et sélection du PDF"
    >
      {onToggle && (
        <button
          type="button"
          className="pdf-display-toggle"
          aria-expanded={!collapsed}
          aria-controls={`pdf-content-${id}`}
          onClick={onToggle}
        >
          <FileText size={18} /> Affichage du PDF original <ChevronDown size={18} />
        </button>
      )}
      <div id={`pdf-content-${id}`} hidden={collapsed}>
        <StatementPdfViewer
          id={id}
          hasExtraction={hasExtraction}
          onSelection={(selection) => {
            setText(selection?.text ?? '');
            setError('');
            setNotice('');
            onSelection(selection);
          }}
        />
        {can('documents.write') && (
          <form
            className="pdf-correction-form"
            onSubmit={(event) => {
              event.preventDefault();
              if (disabled) return;
              setError('');
              setNotice('');
              try {
                const field = fields.find(([key]) => key === target)!;
                const value = pdfFieldValue(
                  text,
                  field[2],
                  target === 'transaction.debit' || target === 'transaction.credit',
                );
                if (movement && !rows.some((row) => row.sequence === sequence))
                  throw new Error('Choisissez un mouvement existant à corriger.');
                if (onApply(movement ? sequence : null, target.split('.')[1], value))
                  setNotice(
                    'Valeur appliquée au brouillon. Enregistrez les corrections pour la conserver.',
                  );
              } catch (cause) {
                setError((cause as Error).message);
              }
            }}
          >
            <h3>Utiliser la valeur sélectionnée</h3>
            <p>
              Choisissez le champ à corriger. Vous pouvez ajuster le texte avant de l’appliquer.
            </p>
            <fieldset disabled={disabled}>
              <label>
                Valeur sélectionnée
                <textarea
                  className="ui-input mt-2"
                  aria-label="Valeur sélectionnée dans le PDF"
                  maxLength={10000}
                  value={text}
                  onChange={(event) => setText(event.target.value)}
                  placeholder="Cliquez sur une valeur du relevé…"
                />
              </label>
              <label>
                Champ à corriger
                <select
                  className="ui-input mt-2"
                  aria-label="Champ à corriger depuis le PDF"
                  value={target}
                  onChange={(event) => {
                    setTarget(event.target.value);
                    setError('');
                    setNotice('');
                  }}
                >
                  <optgroup label="Informations du relevé">
                    {fields
                      .filter(([key]) => key.startsWith('statement.'))
                      .map(([key, label]) => (
                        <option key={key} value={key}>
                          {label}
                        </option>
                      ))}
                  </optgroup>
                  <optgroup label="Mouvement bancaire">
                    {fields
                      .filter(([key]) => key.startsWith('transaction.'))
                      .map(([key, label]) => (
                        <option key={key} value={key}>
                          {label}
                        </option>
                      ))}
                  </optgroup>
                </select>
              </label>
              {movement && (
                <label>
                  Mouvement à corriger
                  <select
                    className="ui-input mt-2"
                    aria-label="Mouvement à corriger depuis le PDF"
                    value={sequence}
                    onChange={(event) => setSequence(Number(event.target.value))}
                  >
                    <option value={0}>Choisir un mouvement…</option>
                    {rows.map((row) => (
                      <option key={row.id} value={row.sequence}>
                        Ligne {row.sequence} · {row.description?.slice(0, 65) || 'Sans libellé'}
                      </option>
                    ))}
                  </select>
                </label>
              )}
              <Button type="submit" disabled={!text.trim() || (movement && !rows.length)}>
                Appliquer la valeur au champ
              </Button>
            </fieldset>
            {error && (
              <p role="alert" className="pdf-correction-error">
                {error}
              </p>
            )}
            {notice && (
              <p role="status" className="pdf-correction-notice">
                {notice}
              </p>
            )}
          </form>
        )}
      </div>
    </aside>
  );
}
