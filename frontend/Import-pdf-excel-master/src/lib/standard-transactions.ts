import type { Transaction } from '@/services/api';
export function standardTransactions(rows: string[][]): Transaction[] {
  const header = rows[0] || [];
  const index = (key: string) => header.indexOf(key);
  const valueAt = (row: string[], key: string) => {
    const position = index(key);
    return position >= 0 ? row[position] || '' : '';
  };
  return rows.slice(1).map((row, index) => ({
    id: String(index),
    date: valueAt(row, 'Date'),
    reference: valueAt(row, 'Reference'),
    description: valueAt(row, 'Description'),
    debit: numberValue(valueAt(row, 'Debit')),
    credit: numberValue(valueAt(row, 'Credit')),
    amount: numberValue(valueAt(row, 'Amount')),
    balance: valueAt(row, 'Balance') === '' ? null : numberValue(valueAt(row, 'Balance')),
  }));
}

function numberValue(value: string) { const n = Number(value.replace(/\s/g, "").replace(",", ".")); return Number.isFinite(n) ? n : 0; }
