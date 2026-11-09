import type { EntryModelPreview, EntryModelRun } from './entry-models';
import type { ExportPreview } from './statement-exports';

type SavedSource = {
  statementId: string;
  sourceVersion: number;
  savedAt: string;
  current: boolean;
  bankAccountId?: string;
};
export type SavedTreatment = SavedSource &
  (
    | { kind: 'ENTRY'; selection: EntryModelRun; preview: EntryModelPreview | null }
    | {
        kind: 'STATEMENT';
        selection: { templateId: string; extractId?: string };
        preview: ExportPreview | null;
      }
  );
