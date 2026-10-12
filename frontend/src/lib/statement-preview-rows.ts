import type { ExportPreview, StatementTransaction } from '@/services/statement-exports';

// Keep the server's formatted cells attached to the movement ID when positions change.
export function statementPreviewRows(
  preview: ExportPreview,
  original: StatementTransaction[],
  rows: StatementTransaction[],
) {
  const originalBySequence = new Map(original.map((row) => [row.sequence, row.id]));
  const entries = preview.rows.map((cells, index) => ({
    cells,
    kind: preview.rowKinds?.[index] ?? 'TRANSACTION',
    sequence: preview.rowSequences[index],
    columns: preview.rowColumnIndices?.[index],
    movementId: originalBySequence.get(preview.rowSequences[index] ?? -1),
    formatted: true,
  }));
  const reordered =
    rows.length !== original.length || rows.some((row, index) => row.id !== original[index]?.id);
  if (!reordered) return entries;
  const opening = entries.filter((row) => row.kind === 'OPENING_BALANCE');
  const closing = entries.filter((row) => row.kind === 'CLOSING_BALANCE');
  const formattedById = new Map(
    entries
      .filter((entry) => entry.kind === 'TRANSACTION')
      .map((entry) => [entry.movementId, entry]),
  );
  const movements = rows.map((row) => {
    const entry = formattedById.get(row.id);
    return {
      cells: entry?.cells ?? preview.columns.map(() => ''),
      columns: entry?.columns,
      kind: 'TRANSACTION',
      sequence: row.sequence,
      movementId: row.id,
      formatted: !!entry,
    };
  });
  return [...opening, ...movements, ...closing];
}
