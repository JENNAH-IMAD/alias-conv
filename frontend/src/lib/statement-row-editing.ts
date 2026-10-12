import type { StatementTransaction } from '@/services/statement-exports';
import { createUuid } from './create-uuid';

export type EditableTransactionField = Exclude<keyof StatementTransaction, 'id' | 'sequence'>;

// Monetary values stay decimal strings. Related fields are synchronized only when requested.
export function editTransactionValue(
  row: StatementTransaction,
  field: EditableTransactionField,
  value: string | null,
  synchronize = true,
): StatementTransaction {
  const next = { ...row, [field]: value };
  if (!synchronize) return next;
  if (value !== null && (field === 'debit' || field === 'credit')) {
    return {
      ...next,
      amount: value,
      direction: field === 'debit' ? 'DEBIT' : 'CREDIT',
      [field === 'debit' ? 'credit' : 'debit']: null,
    };
  }
  if (field === 'amount') {
    if (
      row.direction === 'DEBIT' ||
      (row.direction !== 'CREDIT' && row.debit !== null && row.credit === null)
    )
      return { ...next, debit: value, credit: null, direction: 'DEBIT' };
    if (
      row.direction === 'CREDIT' ||
      (row.direction !== 'DEBIT' && row.credit !== null && row.debit === null)
    )
      return { ...next, credit: value, debit: null, direction: 'CREDIT' };
  }
  if (field === 'direction' && (value === 'DEBIT' || value === 'CREDIT')) {
    const magnitude =
      row.debit !== null && row.credit === null
        ? row.debit
        : row.credit !== null && row.debit === null
          ? row.credit
          : row.debit === null && row.credit === null
            ? row.amount
            : null;
    if (magnitude !== null)
      return {
        ...next,
        amount: magnitude,
        debit: value === 'DEBIT' ? magnitude : null,
        credit: value === 'CREDIT' ? magnitude : null,
      };
  }
  if (value === null && (field === 'debit' || field === 'credit') && row.amount === row[field])
    return { ...next, amount: null };
  return next;
}

export function newStatementTransaction(rows: StatementTransaction[]): StatementTransaction {
  const sequences = new Set(rows.map((row) => row.sequence));
  let sequence = 0;
  for (const row of rows) sequence = Math.max(sequence, row.sequence);
  sequence++;
  if (sequence > 2147483647) {
    sequence = 1;
    while (sequences.has(sequence)) sequence++;
  }
  return {
    id: createUuid(),
    sequence,
    date: null,
    valueDate: null,
    description: null,
    reference: null,
    debit: null,
    credit: null,
    amount: null,
    direction: null,
    balance: null,
    currency: null,
  };
}
