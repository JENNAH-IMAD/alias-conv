import type { Money } from './statement-exports';
export type EntryModel = {
  id: string;
  name: string;
  abbreviation: string;
  keywords: string[];
  bankAccountId: string;
  accountCode: string;
  exportTemplateId: string;
  taxRate: string | number;
  taxAccountCode: string;
  active: boolean;
  bankAccountBased?: boolean;
  output?: EntryModelOutput;
  outputTemplateId?: string | null;
};
export type EntryModelColumn = { field: string; label: string; display: boolean; export: boolean };
export type EntryModelOutput = {
  fileExtension?: 'txt' | 'csv' | 'tsv';
  delimiter: string;
  encoding: string;
  numberCulture: string;
  dateFormat: string;
  includeHeader: boolean;
  columns: EntryModelColumn[];
};
export const entryFields: Record<string, string> = {
  ACCOUNT: 'N° compte',
  TITLE: 'Intitulé',
  DESCRIPTION: 'Libellé',
  BANK: 'Banque',
  NET: 'Montant HT',
  DEBIT_HT: 'Débit HT',
  CREDIT_HT: 'Crédit HT',
  GROSS: 'Montant TTC',
  TAX: 'TVA',
  RATE: 'Taux de TVA',
  DATE: 'Date',
  REFERENCE: 'Référence',
  ABBREVIATION: 'Abréviation',
  CURRENCY: 'Devise',
};
export const defaultEntryOutput = (): EntryModelOutput => ({
  fileExtension: 'txt',
  delimiter: ';',
  encoding: 'utf-8',
  numberCulture: 'fr-FR',
  dateFormat: 'dd/MM/yyyy',
  includeHeader: true,
  columns: ['ACCOUNT', 'TITLE', 'DESCRIPTION', 'BANK', 'DATE', 'DEBIT_HT', 'CREDIT_HT'].map(
    (field) => ({ field, label: entryFields[field], display: true, export: true }),
  ),
});
export type EntryOutputTemplate = {
  id: string;
  name: string;
  description: string;
  active: boolean;
  version: number;
  output: EntryModelOutput;
};
export type EntryResultRow = {
  transactionId: string;
  sequence: number;
  date: string | null;
  accountCode: string;
  title: string;
  description: string;
  bank: string;
  abbreviation: string;
  currency: string;
  reference: string;
  gross: string;
  net: string;
  debitHt: string;
  creditHt: string;
  tax: string;
  rate: string;
};
export type EntryArchiveDetail = {
  id: string;
  clientId: string;
  statementId: string;
  clientName: string;
  bankName: string;
  sourceFileName: string;
  modelName: string;
  fileName: string;
  sourceVersion: number;
  dossierVersion: number;
  createdAt: string;
  generatedByName: string;
  contentSha256: string;
  model: EntryModel;
  output: EntryModelOutput;
  rows: EntryResultRow[];
  text: string;
  truncated: boolean;
};
export type EntryModelDossier = { clientId: string; version: number; models: EntryModel[] };
export type EntryModelRun = {
  outputTemplateId?: string;
  clientId: string;
  statementId: string;
  exportTemplateId: string;
  abbreviation: string;
  extractId?: string;
  modelId?: string;
  keywordOverrides?: string[];
  decisions?: Record<string, string | null>;
  previewFingerprint?: string;
  confirmed?: boolean;
};
export type EntryModelCandidate = {
  id: string;
  name: string;
  accountCode: string;
  taxRate: number;
  keywords: string[];
};
export type EntryModelRow = {
  transactionId: string;
  sequence: number;
  date: string | null;
  description: string;
  status: 'MATCHED' | 'AMBIGUOUS' | 'INVALID' | 'UNMATCHED' | 'EXCLUDED';
  candidates: EntryModelCandidate[];
  modelId: string | null;
  total: Money;
  net: Money;
  tax: Money;
  message: string;
};
export type EntryModelPreview = {
  output?: EntryModelOutput;
  resultRows?: EntryResultRow[];
  clientId: string;
  statementId: string;
  extractId: string | null;
  abbreviation: string;
  sourceVersion: number;
  settingsVersion: number;
  fingerprint: string;
  rows: EntryModelRow[];
  lines: AccountingLine[];
  errors: { code: string; message: string }[];
  canExport: boolean;
};

export type AccountingLine = {
  transactionId: string;
  sequence: number;
  date: string | null;
  journal: string;
  pieceNumber: string;
  accountCode: string;
  thirdPartyCode: string | null;
  description: string;
  reference: string;
  invoiceNumber: string;
  dueDate: string | null;
  paymentMethod: string;
  analytic: string;
  analyticPlanCode: string;
  debit: Money;
  credit: Money;
  currency: string;
  isBank: boolean;
};
