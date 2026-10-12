import { ExportField, ExportTemplate } from './api';

export const descriptionSources = ['Description', 'MovementDescription', 'DescriptionWithBalances'];
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
    name: '',
    type,
    description: '',
    fileExtension: type === 'CUSTOMCSV' ? 'csv' : 'txt',
    delimiter: ';',
    encoding: type === 'SAGEX3' ? 'ascii' : 'utf-8',
    numberCulture: type === 'SAGEX3' ? 'en-US' : 'fr-FR',
    includeHeader: type === 'CUSTOMCSV',
    isActive: true,
    fields:
      type === 'SAGEX3'
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
