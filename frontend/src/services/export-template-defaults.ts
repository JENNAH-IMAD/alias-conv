import { ExportField, ExportTemplate } from './api';

export const descriptionSources = ['Description', 'MovementDescription', 'DescriptionWithBalances'];
export const isAccountingTemplate = (type: string) =>
  type === 'SAGE100ENTRIES' || type === 'SAGEX3ENTRIES';
export function exportTypeLabel(type: string): string {
  return (
    (
      {
        CUSTOMCSV: 'CSV personnalisé',
        SAGE100: 'Sage 100 — relevé',
        SAGEX3: 'Sage X3 — relevé',
        SAGE100ENTRIES: 'Sage 100 — écritures',
        SAGEX3ENTRIES: 'Sage X3 — écritures',
      } as Record<string, string>
    )[type] || type
  );
}
export function accountingFields(type: ExportTemplate['type']): ExportField[] {
  if (type === 'SAGE100ENTRIES') {
    return [
      ['Jour', 'Day', null, true],
      ['N° pièce', 'PieceNumber', null, true],
      ['N° facture', 'InvoiceNumber', null, false],
      ['Référence', 'Reference', null, false],
      ['N° compte général', 'Account', null, true],
      ['N° compte tiers', 'ThirdParty', null, false],
      ['Libellé écriture', 'Description', null, true],
      ['Date échéance', 'DueDate', 'dd/MM/yyyy', false],
      ['Pointage', 'Pointage', null, false],
      ['Débit', 'Debit', '0.00', true],
      ['Crédit', 'Credit', '0.00', true],
    ].map(([fieldName, sourceField, format, required], position) => ({
      fieldName,
      sourceField,
      format,
      required,
      position,
    })) as ExportField[];
  }
  const x3 = type === 'SAGEX3ENTRIES';
  const columns: [string, string, string | null, boolean][] = [
    ...(x3
      ? ([
          ['Site', 'Constant', '', false],
          ['Type de pièce', 'Constant', '', false],
        ] as [string, string, string, boolean][])
      : []),
    ['Journal comptable', 'BankJournal', null, true],
    ['Date comptable', 'Date', x3 ? 'yyyyMMdd' : 'dd/MM/yyyy', true],
    ['N° de pièce', x3 ? 'Reference' : 'PieceNumber', null, true],
    ['Compte comptable', 'BankAccount', null, true],
    ...(!x3
      ? [['Compte tiers', 'ThirdParty', null, false] as [string, string, string | null, boolean]]
      : []),
    ['Libellé', 'Description', null, true],
    ['Débit comptable', 'AccountingDebit', '0.00', true],
    ['Crédit comptable', 'AccountingCredit', '0.00', true],
    ['Devise', 'Currency', null, true],
    ...(!x3
      ? ([
          ['Référence bancaire', 'Reference', null, false],
          ['N° de facture', 'InvoiceNumber', null, false],
          ['Échéance', 'DueDate', 'dd/MM/yyyy', false],
          ['Mode de règlement', 'PaymentMethod', null, false],
        ] as [string, string, string | null, boolean][])
      : []),
  ];
  return columns.map(([fieldName, sourceField, format, required], position) => ({
    fieldName,
    sourceField,
    format,
    required,
    position,
  }));
}
export function sageX3Fields(): ExportField[] {
  return [
    ['Date opération', 'Date', 'yyyyMMdd', true],
    ['Date valeur', 'ValueDate', 'yyyyMMdd', true],
    ['Libellé', 'Description', null, false],
    ['Montant en devise', 'Amount', '0.00', true],
    ['Sens', 'Direction', null, true],
    ['Code', 'BankCode', null, true],
    ['Devise', 'Currency', null, true],
    ['Numéro relevé', 'StatementNumber', null, true],
    ['Numéro', 'RecordType', null, true],
    ['Numéro interne', 'Sequence', null, true],
  ].map(([fieldName, sourceField, format, required], position) => ({
    fieldName,
    sourceField,
    format,
    required,
    position,
  })) as ExportField[];
}
export function newExportTemplate(type: ExportTemplate['type']): Omit<ExportTemplate, 'id'> {
  return {
    name: isAccountingTemplate(type)
      ? `Écritures ${type === 'SAGE100ENTRIES' ? 'Sage 100' : 'Sage X3'} paramétrables`
      : '',
    type,
    description:
      type === 'SAGE100ENTRIES'
        ? "Journal bancaire : 11 colonnes de saisie Sage 100, du jour au crédit. Export d'un seul journal et d'une seule période mensuelle."
        : '',
    fileExtension: type === 'CUSTOMCSV' ? 'csv' : 'txt',
    delimiter: ';',
    encoding: isAccountingTemplate(type) ? 'windows-1252' : type === 'SAGEX3' ? 'ascii' : 'utf-8',
    numberCulture: type === 'SAGEX3' || type === 'SAGEX3ENTRIES' ? 'en-US' : 'fr-FR',
    includeHeader: type === 'CUSTOMCSV',
    isActive: true,
    fields: isAccountingTemplate(type)
      ? accountingFields(type)
      : type === 'SAGEX3'
        ? sageX3Fields()
        : [
            {
              fieldName: '',
              sourceField: 'Description',
              format: null,
              required: false,
              position: 0,
            },
          ],
  };
}
