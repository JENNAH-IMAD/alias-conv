import assert from 'node:assert/strict';
import fs from 'node:fs';
import ts from 'typescript';

const source = fs.readFileSync(
  new URL('../src/services/export-template-defaults.ts', import.meta.url),
  'utf8',
);
const { outputText } = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 },
});
const { newExportTemplate, accountingFields } = await import(
  'data:text/javascript;base64,' + Buffer.from(outputText).toString('base64')
);
const model = newExportTemplate('SAGE100ENTRIES');
assert.deepEqual(
  model.fields.map((f) => f.fieldName),
  [
    'Jour',
    'N° pièce',
    'N° facture',
    'Référence',
    'N° compte général',
    'N° compte tiers',
    'Libellé écriture',
    'Date échéance',
    'Pointage',
    'Débit',
    'Crédit',
  ],
);
assert.deepEqual(
  model.fields.map((f) => f.sourceField),
  [
    'Day',
    'PieceNumber',
    'InvoiceNumber',
    'Reference',
    'Account',
    'ThirdParty',
    'Description',
    'DueDate',
    'Pointage',
    'Debit',
    'Credit',
  ],
);
assert.deepEqual(
  model.fields.map((f) => f.position),
  Array.from({ length: 11 }, (_, i) => i),
);
assert.deepEqual(
  model.fields.filter((f) => !f.required).map((f) => f.sourceField),
  ['InvoiceNumber', 'Reference', 'ThirdParty', 'DueDate', 'Pointage'],
);
assert.equal(model.fields[7].format, 'dd/MM/yyyy');
assert.equal(model.fields[9].format, '0.00');
assert.equal(model.fields[10].format, '0.00');
assert.equal(model.encoding, 'windows-1252');
assert.equal(model.includeHeader, false);
assert.deepEqual(accountingFields('SAGE100ENTRIES'), model.fields);
assert.equal(
  accountingFields('SAGEX3ENTRIES').find((f) => f.sourceField === 'Date').format,
  'yyyyMMdd',
);
console.log(
  'Sage 100 template: eleven ordered columns, distinct accounts, optional document fields, and compatible formats passed.',
);
