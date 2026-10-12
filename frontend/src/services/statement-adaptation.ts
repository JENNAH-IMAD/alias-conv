import { request, type ExportField, type Page } from './api';
import type { PdfTextDocument, StatementDetail, StatementSummary } from './statement-exports';

type Mapping = NonNullable<PdfTextDocument['lastMapping']>;
const fold = (text: string) =>
  text
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/\s+/g, ' ')
    .trim()
    .toUpperCase();
const empty = (value: unknown) => value == null || value === '';

function needsRecovery(detail: StatementDetail, fields: ExportField[]) {
  const required = fields.filter((field) => field.required).map((field) => field.sourceField);
  return (
    detail.extraction?.needsMapping ||
    detail.statement.transactions.some((row) =>
      required.some((source) => {
        if (source === 'Date') return !row.date;
        if (source === 'ValueDate') return !row.valueDate;
        if (['Description', 'MovementDescription', 'DescriptionWithBalances'].includes(source))
          return !row.description?.trim();
        if (source === 'Reference') return !row.reference?.trim();
        if (['Amount', 'Debit', 'Credit', 'Direction'].includes(source))
          return empty(row.amount) && empty(row.debit) && empty(row.credit);
        return false;
      }),
    )
  );
}

function header(page: PdfTextDocument['pages'][number], mapping: Mapping): string | null {
  if (mapping.tableColumns) {
    const captions = page.tables?.[0]?.rows[mapping.tableColumns.startRow - 1];
    if (
      !captions ||
      captions.filter((cell) => /[a-zà-ÿ]/i.test(cell) && !/\d/.test(cell)).length < 3
    )
      return null;
    return 'TABLE:' + captions.map(fold).join('|');
  }
  // Compare captions and their positions, never customer names or transaction values.
  const target = (page.height * mapping.headerTop) / 100;
  const boxes = page.boxes.filter((box) => Math.abs(box.bottom - target) <= page.height * 0.05);
  const debit = boxes.find((box) => /DEBIT/.test(fold(box.text)));
  const credit = boxes.find((box) => /CREDIT/.test(fold(box.text)));
  if (!debit || !credit || Math.abs(debit.top - credit.top) > page.height * 0.012) return null;
  const line = boxes
    .filter((box) => Math.abs(box.top - debit.top) <= page.height * 0.008)
    .sort((a, b) => a.left - b.left);
  if (line.length < 3 || line.some((box) => /\d/.test(box.text))) return null;
  return (
    'TEXT:' +
    line.map((box) => fold(box.text) + '@' + Math.round((box.left / page.width) * 100)).join('|')
  );
}

export function sameStatementLayout(current: PdfTextDocument, previous: PdfTextDocument): boolean {
  const mapping = previous.lastMapping;
  const source = previous.pages[0];
  if (!mapping || !source || !current.pages.length || current.lastMapping) return false;
  const fingerprint = header(source, mapping);
  if (!fingerprint) return false;
  return current.pages.every(
    (page) =>
      page.width > 0 &&
      page.height > 0 &&
      Math.abs(page.width / source.width - 1) <= 0.02 &&
      Math.abs(page.height / source.height - 1) <= 0.02 &&
      header(page, mapping) === fingerprint,
  );
}

// Uses existing saved extraction settings and API actions. No bank-specific code or browser-only storage.
export async function reuseValidatedAdaptation(
  detail: StatementDetail,
  fields: ExportField[],
): Promise<{ detail: StatementDetail; reused: boolean }> {
  // Later versions may contain manual edits. Only a fresh, unvalidated import is eligible.
  if (!detail.extraction || detail.version !== 1 || detail.status === 'VALIDATED')
    return { detail, reused: false };
  let raw: PdfTextDocument;
  try {
    raw = await request<PdfTextDocument>(`/statement-imports/${detail.id}/extraction`);
  } catch {
    return { detail, reused: false };
  }
  if (raw.lastMapping) return { detail, reused: false };
  let profiles: {
    id: string;
    bankCode: string;
    currency: string;
    isActive: boolean;
    sampleJson: string;
  }[] = [];
  try {
    profiles = await request('/extraction-profiles');
  } catch {
    /* Older deployments use the existing fallback. */
  }
  const matching = profiles.filter(
    (profile) =>
      profile.isActive &&
      profile.bankCode === detail.statement.bankCode &&
      profile.currency === detail.statement.currency &&
      sameStatementLayout(raw, JSON.parse(profile.sampleJson)),
  );
  // Ambiguous layouts require an explicit choice in the extraction editor.
  if (matching.length === 1) {
    const adapted = await request<StatementDetail>(`/statement-imports/${detail.id}/adapt`, {
      method: 'POST',
      body: JSON.stringify({ statementVersion: detail.version, profileId: matching[0].id }),
    });
    return { detail: adapted, reused: true };
  }
  if (matching.length > 1) return { detail, reused: false };
  if (!needsRecovery(detail, fields)) return { detail, reused: false };
  let inspected = 0;
  for (let page = 1; page <= 5 && inspected < 12; page++) {
    let history: Page<StatementSummary>;
    try {
      history = await request<Page<StatementSummary>>(
        `/statement-imports?page=${page}&pageSize=100`,
      );
    } catch {
      return { detail, reused: false };
    }
    const candidates = history.items.filter(
      (item) =>
        item.id !== detail.id &&
        item.status === 'VALIDATED' &&
        fold(item.bankName) === fold(detail.statement.bankName) &&
        item.currency === detail.statement.currency,
    );
    for (const candidate of candidates) {
      if (++inspected > 12) break;
      let previous: PdfTextDocument;
      try {
        previous = await request<PdfTextDocument>(`/statement-imports/${candidate.id}/extraction`);
      } catch {
        continue;
      }
      if (!sameStatementLayout(raw, previous)) continue;
      // The server applies validation, version locking and traceability to the reused mapping.
      const adapted = await request<StatementDetail>(`/statement-imports/${detail.id}/adapt`, {
        method: 'POST',
        body: JSON.stringify({ statementVersion: detail.version, mapping: previous.lastMapping }),
      });
      return { detail: adapted, reused: true };
    }
    if (page * history.pageSize >= history.total) break;
  }
  return { detail, reused: false };
}
