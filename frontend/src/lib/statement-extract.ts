import type { Money, StatementTransaction } from '@/services/statement-exports';

export function normalizeFilterText(text: string) {
  return text.normalize('NFD').replace(/\p{M}/gu, '').normalize('NFC').toLowerCase();
}

export function filterStatementRows(rows: StatementTransaction[], text: string, excludeText = '') {
  const filter = normalizeFilterText(text.trim());
  const exclusions = excludeText
    .split(/[,;\n\r]/)
    .map((value) => normalizeFilterText(value.trim()))
    .filter(Boolean);
  return rows.filter((row) => {
    const description = normalizeFilterText(row.description ?? '');
    return description.includes(filter) && !exclusions.some((value) => description.includes(value));
  });
}

// Integer arithmetic at the source precision, including amounts smaller than a cent.
export function extractTotals(rows: StatementTransaction[]) {
  const values = rows.flatMap((row) => [row.debit, row.credit]);
  const scale = values.reduce<number>(
    (precision, value) => Math.max(precision, String(value ?? 0).split('.')[1]?.length ?? 0),
    2,
  );
  function units(value: Money) {
    const text = String(value ?? 0);
    const [whole, fraction = ''] = text.replace(/^-/, '').split('.');
    return (
      BigInt(whole + fraction.padEnd(scale, '0')) * (text.startsWith('-') ? BigInt(-1) : BigInt(1))
    );
  }
  function decimal(value: bigint) {
    const text = (value < BigInt(0) ? -value : value).toString().padStart(scale + 1, '0');
    return `${value < BigInt(0) ? '-' : ''}${text.slice(0, -scale)}.${text.slice(-scale)}`;
  }
  const debit = rows.reduce((total, row) => total + units(row.debit), BigInt(0));
  const credit = rows.reduce((total, row) => total + units(row.credit), BigInt(0));
  return {
    totalDebit: decimal(debit),
    totalCredit: decimal(credit),
    balance: decimal(credit - debit),
  };
}
