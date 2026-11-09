'use client';
import { money } from '@/services/statement-exports';
import type { EntryModelOutput, EntryResultRow } from '@/services/entry-models';
const numeric = new Set(['NET', 'GROSS', 'TAX', 'RATE', 'DEBIT_HT', 'CREDIT_HT']);
export function EntryResultTable({
  output,
  rows,
}: {
  output: EntryModelOutput;
  rows: EntryResultRow[];
}) {
  const columns = output.columns.filter((c) => c.display);
  function formattedDate(raw: string | null | undefined) {
    if (!raw) return '—';
    const [year, month, day] = raw.slice(0, 10).split('-');
    if (!year || !month || !day) return raw;
    const values: Record<string, string> = { yyyy: year, yy: year.slice(-2), MM: month, dd: day };
    return (output.dateFormat || 'dd/MM/yyyy').replace(/yyyy|yy|MM|dd/g, (part) => values[part] ?? part);
  }
  function value(row: EntryResultRow, field: string) {
    const values: Record<string, string | null> = {
      ACCOUNT: row.accountCode,
      TITLE: row.title,
      DESCRIPTION: row.description,
      BANK: row.bank,
      NET: row.net,
      DEBIT_HT: row.debitHt,
      CREDIT_HT: row.creditHt,
      GROSS: row.gross,
      TAX: row.tax,
      RATE: row.rate,
      DATE: row.date,
      REFERENCE: row.reference,
      ABBREVIATION: row.abbreviation,
      CURRENCY: row.currency,
    };
    const raw = values[field];
    if (numeric.has(field))
      return output.numberCulture === 'fr-FR'
        ? money(raw)
        : String(raw ?? '')
            .replace(/(\.\d)$/, (fraction) => `${fraction}0`)
            .replace(/^(\d+)$/, '$1.00');
    return field === 'DATE' ? formattedDate(raw) : raw || '—';
  }
  return (
    <div className="entry-model-table-wrap">
      <table
        className="entry-model-results-table entry-model-output-table"
        aria-label="Résultat du modèle d’écritures"
      >
        <thead>
          <tr>
            {columns.map((c) => (
              <th scope="col" key={c.field} className={numeric.has(c.field) ? 'amount' : ''}>
                {c.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.transactionId}>
              {columns.map((c) => (
                <td
                  key={c.field}
                  data-label={c.label}
                  className={
                    numeric.has(c.field)
                      ? 'amount'
                      : c.field === 'ACCOUNT'
                        ? 'entry-model-output-account'
                        : ''
                  }
                >
                  {value(row, c.field)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
      {!rows.length && <p className="muted text-sm mt-4">Aucun mouvement retenu pour ce modèle.</p>}
    </div>
  );
}
