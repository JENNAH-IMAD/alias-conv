'use client';
import { useState } from 'react';
import { Download, Eye, FileOutput } from 'lucide-react';
import { documentBlob, Page, request, type ExportDiagnostic } from '@/services/api';
import {
  canChooseExportLocation,
  chooseExportDestination,
  exportFileName,
  saveExportBlob,
} from '@/lib/save-export';
import { ExportJob } from '@/services/statement-exports';
import { useApi } from '@/services/use-api';
import { Button } from './ui/button';
import { BankDialog } from './bank-dialogs';
import { Pagination } from './ui/pagination';
import { LoadState } from './ui/load-state';
type SavedPreview = {
  status?: string;
  textEdited?: boolean;
  diagnostics?: { errors: ExportDiagnostic[]; warnings: ExportDiagnostic[] } | null;
  fileName: string;
  statementVersion: number;
  rowCount: number;
  encoding: string;
  delimiter: string;
  generatedByName: string | null;
  templateNameSnapshot: string;
  text: string;
  truncated: boolean;
};
export function ExportArchive({
  statementId,
  refreshKey = 0,
}: {
  statementId: string;
  refreshKey?: number;
}) {
  const [page, setPage] = useState(1);
  const [busy, setBusy] = useState('');
  const [error, setError] = useState('');
  const [preview, setPreview] = useState<SavedPreview | null>(null);
  const history = useApi<Page<ExportJob>>(
    `/statement-imports/${statementId}/exports?page=${page}&pageSize=20&refresh=${refreshKey}`,
  );
  async function download(job: ExportJob, standard: boolean) {
    setBusy(job.id);
    setError('');
    try {
      const extension = standard ? 'csv' : job.extension;
      let name = standard ? job.fileName.replace(/\.[^.]+$/, '_standard.csv') : job.fileName;
      if (!canChooseExportLocation()) {
        const entered = window.prompt('Nom du fichier à télécharger', name);
        if (entered === null) return;
        name = exportFileName(entered, extension);
      }
      const destination = await chooseExportDestination(name, extension);
      await saveExportBlob(
        await documentBlob(
          `/statement-imports/${statementId}/exports/archives/${job.id}/file${standard ? '?standard=true' : ''}`,
        ),
        destination,
      );
    } catch (e) {
      if ((e as Error).name !== 'AbortError') setError((e as Error).message);
    } finally {
      setBusy('');
    }
  }
  async function inspect(job: ExportJob) {
    setBusy(job.id);
    setError('');
    try {
      setPreview(
        await request<SavedPreview>(
          `/statement-imports/${statementId}/exports/archives/${job.id}/preview`,
        ),
      );
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy('');
    }
  }
  return (
    <div className="p-5">
      <div className="flex justify-between items-center gap-4 mb-5">
        <div>
          <h3 className="font-semibold">Fichiers exportés</h3>
          <p className="muted text-xs mt-1">
            Fichiers conservés avec le modèle et la version utilisés lors du téléchargement.
          </p>
        </div>
        <Button size="sm" variant="outline" onClick={history.reload}>
          Actualiser
        </Button>
      </div>
      <LoadState
        loading={history.loading}
        error={history.error}
        empty={!history.data?.items.length}
      />
      {error && (
        <p role="alert" className="conversion-alert error mb-4">
          {error}
        </p>
      )}
      <div className="archive-file-list">
        {history.data?.items.map((job) => (
          <article key={job.id} className="archive-file">
            <header>
              <FileOutput size={21} />
              <div>
                <strong>{job.fileName}</strong>
                <p>
                  {job.templateName} · version {job.statementVersion} · {job.rowCount} lignes ·{' '}
                  {job.encoding}
                </p>
                {job.status === 'FORCED' && (
                  <span className="ui-tag">Téléchargé avec erreurs · à vérifier</span>
                )}
                {job.status === 'EDITED' && (
                  <span className="ui-tag">Texte modifié manuellement</span>
                )}
                <p>
                  {new Date(job.createdAt).toLocaleString('fr-FR')}
                  {job.generatedByName ? ` · ${job.generatedByName}` : ''}
                </p>
              </div>
            </header>
            {job.hasFile ? (
              <div className="flex gap-2 flex-wrap">
                <Button
                  size="sm"
                  variant="outline"
                  disabled={!!busy}
                  onClick={() => void inspect(job)}
                >
                  <Eye size={15} />
                  Voir le fichier
                </Button>
                <Button
                  permission="documents.export"
                  size="sm"
                  variant="outline"
                  disabled={!!busy}
                  onClick={() => void download(job, false)}
                >
                  <Download size={15} />
                  Télécharger .{job.extension}
                </Button>
                <Button
                  permission="documents.export"
                  size="sm"
                  variant="outline"
                  disabled={!!busy}
                  onClick={() => void download(job, true)}
                >
                  <Download size={15} />
                  CSV standard
                </Button>
              </div>
            ) : (
              <p className="muted text-xs">
                Ancien export : empreinte conservée, fichier non archivé. Préparez un nouvel export
                dans la conversion.
              </p>
            )}
          </article>
        ))}
      </div>
      {history.data && <Pagination page={page} total={history.data.total} onChange={setPage} />}
      {preview && (
        <BankDialog title={preview.fileName} wide onClose={() => setPreview(null)}>
          <p className="muted text-xs mb-4">
            {preview.templateNameSnapshot} · version {preview.statementVersion} · {preview.rowCount}{' '}
            lignes · {preview.encoding}
          </p>
          {preview.status === 'FORCED' && (
            <p className="conversion-preview-warning">
              Fichier téléchargé avec erreurs : {preview.diagnostics?.errors.length ?? 0}{' '}
              diagnostic(s) enregistré(s). Encodage effectif : {preview.encoding}.
            </p>
          )}
          {preview.textEdited && (
            <p className="conversion-preview-warning">
              Ce fichier contient un texte modifié manuellement.
            </p>
          )}
          <pre className="archive-code" tabIndex={0}>
            {preview.text}
          </pre>
          {preview.truncated && (
            <p className="muted text-xs mt-3">
              Aperçu limité aux 200 000 premiers caractères. Le téléchargement contient le fichier
              complet.
            </p>
          )}
        </BankDialog>
      )}
    </div>
  );
}
