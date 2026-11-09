import { request, type ExportTemplate } from './api';
import { reuseValidatedAdaptation } from './statement-adaptation';
import type { StatementDetail, PreparedStatementExport } from './statement-exports';

export async function prepareStatementModel(
  detail: StatementDetail,
  model: ExportTemplate,
  extractMode: boolean,
  extractId?: string,
  onAdapted?: (detail: StatementDetail) => void,
) {
  const adaptation = await reuseValidatedAdaptation(detail, model.fields);
  onAdapted?.(adaptation.detail);
  const result = await request<PreparedStatementExport>(`/statement-imports/${detail.id}/prepare`, {
    method: 'POST',
    body: JSON.stringify({
      statementVersion: adaptation.detail.version,
      templateId: model.id,
      ...(extractMode && adaptation.detail.statement.extracts?.length
        ? { extractId: extractId ?? adaptation.detail.statement.extracts.at(-1)?.id }
        : {}),
    }),
  });
  if (adaptation.reused) {
    result.preview.warnings = [
      ...result.preview.warnings,
      {
        code: 'SAVED_ADAPTATION_REUSED',
        message:
          'Un modèle d’extraction enregistré ou un réglage vérifié de même format a été réutilisé.',
        transactionSequence: null,
        fieldName: null,
        sourceField: null,
      },
    ];
  }
  return result;
}
