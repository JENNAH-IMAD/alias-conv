import type { Money, StatementTransaction } from '@/services/statement-exports';

export function emptyMovementAmount(value: Money): Money {
  return value === null || value === '' || /^[+-]?0+(?:[.,]0+)?$/.test(String(value))
    ? null
    : value;
}

export function normalizeMovementAmounts(row: StatementTransaction): StatementTransaction {
  const debit = emptyMovementAmount(row.debit);
  const credit = emptyMovementAmount(row.credit);
  let amount = emptyMovementAmount(row.amount);
  let direction = row.direction;
  const hasZero = [row.debit, row.credit, row.amount].some(
    (value) => value !== null && value !== '' && emptyMovementAmount(value) === null,
  );
  if (!hasZero && !(debit === null && credit === null && amount === null)) return row;
  if ((debit !== null) !== (credit !== null)) {
    if (!direction || ['UNKNOWN', 'DEBIT', 'CREDIT'].includes(direction))
      direction = debit !== null ? 'DEBIT' : 'CREDIT';
    amount ??= debit ?? credit;
  } else if (
    debit === null &&
    credit === null &&
    amount === null &&
    (!direction || ['UNKNOWN', 'DEBIT', 'CREDIT'].includes(direction))
  )
    direction = null;
  return { ...row, debit, credit, amount, direction };
}
