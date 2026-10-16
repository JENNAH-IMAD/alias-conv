import type { Money } from '@/services/statement-exports';

// Presentation only: accounting data keeps its original sign.
export function absoluteBalance(value: Money): Money {
  return typeof value === 'number'
    ? Math.abs(value)
    : typeof value === 'string'
      ? value.trim().replace(/^-/, '')
      : null;
}
export function balanceDirection(value: Money): string {
  return value === null || value === '' ? '' : Number(value) < 0 ? 'Crédit' : 'Débit';
}
