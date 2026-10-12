'use client';
import { useState } from 'react';
import { BankDialog } from './bank-dialogs';
import { StatementWorkspace } from './statement-workspace';
import { LoadState } from './ui/load-state';
import { useApi, useAll } from '@/services/use-api';
import type { ExportTemplate } from '@/services/api';
import type { StatementDetail } from '@/services/statement-exports';
import { confirmDiscard, hasUnsavedChanges } from '@/lib/use-unsaved-changes';

export function ClientStatementViewer({
  id,
  onClose,
  onSaved,
}: {
  id: string;
  onClose: () => void;
  onSaved: () => void;
}) {
  const detail = useApi<StatementDetail>(`/statement-imports/${id}`);
  const templates = useAll<ExportTemplate>('/export-templates');
  const [templateId, setTemplateId] = useState('');
  return (
    <BankDialog
      title="Consulter le relevé"
      wide
      className="statement-archive-dialog"
      onClose={() => {
        if (confirmDiscard(hasUnsavedChanges())) onClose();
      }}
    >
      <LoadState
        loading={detail.loading || templates.loading}
        error={detail.error || templates.error}
      />
      {detail.data && !detail.loading && (
        <StatementWorkspace
          key={`${detail.data.id}-${detail.data.version}`}
          detail={detail.data}
          templates={templates.data || []}
          templateId={templateId}
          onTemplate={setTemplateId}
          onTemplatesChanged={(modelId) => {
            setTemplateId(modelId);
            templates.reload();
          }}
          onSaved={() => {
            detail.reload();
            onSaved();
          }}
        />
      )}
    </BankDialog>
  );
}
