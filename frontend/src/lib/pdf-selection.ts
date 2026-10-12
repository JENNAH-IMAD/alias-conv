export type PdfSelection = {
  text: string;
  page: number;
  left: number;
  top: number;
  bottom: number;
};

export function pdfFieldValue(
  text: string,
  type: 'text' | 'date' | 'money',
  positive = false,
): string {
  const value = text.trim();
  if (!value) throw new Error('Sélectionnez ou saisissez une valeur avant de l’appliquer.');
  if (type === 'text') return value;
  if (type === 'money') {
    let amount = value.replace(/\s/g, '').replace(/(?:MAD|EUR|USD|DH|DIRHAMS?)$/i, '');
    if (amount.includes(',') && amount.includes('.')) {
      amount =
        amount.lastIndexOf(',') > amount.lastIndexOf('.')
          ? amount.replace(/\./g, '').replace(',', '.')
          : amount.replace(/,/g, '');
    } else if (amount.includes(',')) amount = amount.replace(',', '.');
    if (!/^-?\d+(?:\.\d+)?$/.test(amount) || (positive && amount.startsWith('-')))
      throw new Error(
        'Sélectionnez uniquement le montant, par exemple 35 023,45. Un débit ou un crédit doit être positif.',
      );
    return amount;
  }
  const iso = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  const french = /^(\d{1,2})[/.\-\s]+(\d{1,2})[/.\-\s]+(\d{4})$/.exec(value);
  if (!iso && !french) throw new Error('Renseignez la date complète, par exemple 31/03/2025.');
  const [year, month, day] = iso
    ? iso.slice(1).map(Number)
    : [Number(french![3]), Number(french![2]), Number(french![1])];
  const date = new Date(0);
  date.setUTCFullYear(year, month - 1, day);
  if (
    year < 1 ||
    date.getUTCFullYear() !== year ||
    date.getUTCMonth() !== month - 1 ||
    date.getUTCDate() !== day
  )
    throw new Error('La date sélectionnée est invalide. Vérifiez-la dans le PDF.');
  return `${String(year).padStart(4, '0')}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}
