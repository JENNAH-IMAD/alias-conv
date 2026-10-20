'use client';
import { useState } from 'react';
import { Download } from 'lucide-react';
import { useApi } from '@/services/use-api';
import { documentBlob, saveBlob } from '@/services/api';
import { Button } from './ui/button';
import { LoadState } from './ui/load-state';

export function StatementSourceText({ id }: { id: string }) {
  const source = useApi<{ text: string; method: string }>(
    `/statement-imports/${id}/source-text?preview=true`,
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  return (
    <section className="space-y-3 p-3" aria-label="Texte source du relevé">
      <p className="muted text-sm">
        Lecture initiale conservée, avant les corrections et l’export. Vérifiez les lignes et les
        colonnes avec le PDF original ; une transcription automatique peut contenir des erreurs.
      </p>
      <LoadState loading={source.loading} error={source.error} />
      {source.error && (
        <Button variant="outline" onClick={source.reload}>
          Réessayer
        </Button>
      )}
      {source.data && (
        <>
          <Button
            type="button"
            variant="outline"
            permission="documents.export"
            disabled={busy}
            onClick={async () => {
              setBusy(true);
              setError('');
              try {
                saveBlob(
                  await documentBlob(`/statement-imports/${id}/source-text`),
                  `releve_${id}_source.txt`,
                );
              } catch (cause) {
                setError((cause as Error).message);
              } finally {
                setBusy(false);
              }
            }}
          >
            <Download size={16} /> {busy ? 'Préparation…' : 'Télécharger le texte source'}
          </Button>
          <pre
            className="max-h-[70vh] overflow-auto whitespace-pre border rounded-lg p-3 text-xs"
            tabIndex={0}
          >
            {source.data.text}
          </pre>
        </>
      )}
      {error && (
        <p role="alert" className="text-destructive">
          {error}
        </p>
      )}
    </section>
  );
}
