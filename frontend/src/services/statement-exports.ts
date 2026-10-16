import { API_URL, apiError, ExportDiagnostic } from './api';
import { ExportDestination, saveExportBlob } from '@/lib/save-export';
export type Money = string | number | null;
export type StatementTransaction = {
  sourcePages?: number[] | null;
  id: string;
  sequence: number;
  date: string | null;
  valueDate: string | null;
  description: string | null;
  reference: string | null;
  debit: Money;
  credit: Money;
  amount: Money;
  direction: string | null;
  balance: Money;
  currency: string | null;
  journal?: string | null;
  account?: string | null;
  analytic?: string | null;
  recordType?: string | null;
};
export type StatementExtract = {
  excludeText?: string;
  id: string;
  filterText: string;
  createdAt: string;
  sourceVersion: number;
  currency: string | null;
  transactions: StatementTransaction[];
  totalDebit: Money;
  totalCredit: Money;
  balance: Money;
};
export type BankStatement = {
  conversionType?: 'STATEMENT' | 'EXTRACT';
  extracts?: StatementExtract[];
  accountHolder?: string | null;
  id: string;
  bankName: string;
  bankCode: string | null;
  accountNumber: string | null;
  currency: string | null;
  statementNumber: string | null;
  periodStart: string | null;
  periodEnd: string | null;
  openingBalance: Money;
  closingBalance: Money;
  openingBalanceDate?: string | null;
  openingBalanceValueDate?: string | null;
  openingBalanceDescription?: string | null;
  closingBalanceDate?: string | null;
  closingBalanceValueDate?: string | null;
  closingBalanceDescription?: string | null;
  totalDebit: Money;
  totalCredit: Money;
  balanceConvention: string | null;
  journal?: string | null;
  account?: string | null;
  analytic?: string | null;
  recordType?: string | null;
  transactions: StatementTransaction[];
};
export type StatementSummary = {
  id: string;
  bankName: string;
  statementNumber: string | null;
  currency: string | null;
  periodStart: string | null;
  periodEnd: string | null;
  status: string;
  version: number;
  transactionCount: number;
  createdAt: string;
};
export type StatementDetail = {
  clientId?: string | null;
  bankAccountId?: string | null;
  importedByName?: string | null;
  modifiedByName?: string | null;
  extraction?: {
    method: string;
    pageCount: number;
    lowConfidenceCount: number;
    needsMapping: boolean;
  } | null;
  sourceFileName?: string | null;
  id: string;
  status: string;
  version: number;
  validatedAt: string | null;
  statement: BankStatement;
  warnings: ExportDiagnostic[];
  errors: ExportDiagnostic[];
};
export type PdfTextDocument = {
  method: string;
  nativePages?: PdfTextDocument['pages'] | null;
  lastMapping?: {
    date: number;
    valueDate: number | null;
    description: number;
    debit: number;
    credit: number;
    headerTop: number;
    tableColumns?: {
      date: number;
      valueDate: number | null;
      description: number;
      debit: number;
      credit: number;
      startRow: number;
      reference?: number | null;
    } | null;
  } | null;
  pages: {
    number: number;
    width: number;
    height: number;
    markdown?: string | null;
    tables?: { rows: string[][] }[] | null;
    rawResult?: unknown;
    boxes: {
      text: string;
      left: number;
      right: number;
      top: number;
      bottom: number;
      confidence: number | null;
    }[];
  }[];
};
export type ExportPreview = {
  extractId?: string | null;
  text?: string | null;
  textTruncated?: boolean;
  rowColumnIndices?: number[][];
  template: { id: string; name: string };
  columns: string[];
  rows: string[][];
  rowSequences: (number | null)[];
  rowKinds?: string[];
  totalRows: number;
  isValidated: boolean;
  openingBalance: Money;
  closingBalance: Money;
  errors: ExportDiagnostic[];
  warnings: ExportDiagnostic[];
  statementVersion: number;
  templateFingerprint: string;
};
export type PreparedStatementExport = { detail: StatementDetail; preview: ExportPreview };
export type ExportJob = {
  hasFile?: boolean;
  generatedByName?: string | null;
  id: string;
  templateId: string;
  templateName: string;
  statementVersion: number;
  fileName: string;
  rowCount: number;
  extension: string;
  encoding: string;
  createdAt: string;
  status: string;
  contentSha256: string;
};

// Preserve decimal text through the browser. No floating-point arithmetic on amounts.
export function money(value: Money | undefined): string {
  if (value === null || value === undefined || value === '') return '—';
  const [whole, fraction = ''] = String(value).split('.');
  return `${whole.replace(/\B(?=(\d{3})+(?!\d))/g, '\u202f')},${fraction.padEnd(2, '0')}`;
}
export function accountSuffix(value: string | null) {
  const digits = (value || '').replace(/\D/g, '');
  return digits.length > 4 ? `•••• ${digits.slice(-4)}` : 'Compte';
}
export function date(value: string | null) {
  return value ? value.split('-').reverse().join('/') : '—';
}
export function separator(value: string) {
  return (
    (
      {
        ';': 'Point-virgule',
        ',': 'Virgule',
        '\t': 'Tabulation',
        '|': 'Barre verticale',
        ' ': 'Espace',
      } as Record<string, string>
    )[value] || value
  );
}
export async function downloadExport(
  statementId: string,
  templateId: string,
  preview: ExportPreview,
  options?: {
    allowErrors: boolean;
    editedText: string | null;
    onEncoding?: (encoding: string) => void;
    destination?: ExportDestination;
  },
) {
  const response = await fetch(
    `${API_URL}/statement-imports/${statementId}/exports/${templateId}`,
    {
      method: 'POST',
      cache: 'no-store',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json', 'X-Alias-Request': '1' },
      body: JSON.stringify({
        statementVersion: preview.statementVersion,
        ...(preview.extractId ? { extractId: preview.extractId } : {}),
        templateFingerprint: preview.templateFingerprint,
        ...(options?.allowErrors ? { allowErrors: true } : {}),
        ...(options?.editedText != null ? { editedText: options.editedText } : {}),
      }),
    },
  );
  if (!response.ok) throw await apiError(response);
  const encoding = response.headers.get('Content-Type')?.match(/charset=([^;\s]+)/i)?.[1];
  if (encoding) options?.onEncoding?.(encoding);
  const disposition = response.headers.get('Content-Disposition') || '';
  const extended = disposition.match(/filename\*=UTF-8''([^;]+)/i);
  const plain = disposition.match(/filename="?([^";]+)"?/i);
  const name = extended ? decodeURIComponent(extended[1]) : plain?.[1] || 'Alias-export.txt';
  const blob = await response.blob();
  const destination = options?.destination ?? { name };
  await saveExportBlob(blob, destination);
  return destination.name;
}
