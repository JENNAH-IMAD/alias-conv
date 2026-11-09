'use client';
import { Suspense, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { BookOpen, Search, Settings2 } from 'lucide-react';
import { PageHeader } from '@/components/page-header';
import { Button } from '@/components/ui/button';
import { EntryModelResultsWorkspace } from '@/components/entry-model-results-workspace';
import { useAll } from '@/services/use-api';
import type { Client } from '@/services/api';
import '@/styles/entry-models.css';

export default function Page() {
  return (
    <Suspense fallback={<p role="status">Chargement des résultats…</p>}>
      <ResultsPage />
    </Suspense>
  );
}
function ResultsPage() {
  const params = useSearchParams();
  const clients = useAll<Client>('/clients');
  const [clientId, setClientId] = useState(params.get('clientId') || '');
  return (
    <div className="ui-page entry-models-page">
      <PageHeader
        icon={BookOpen}
        eyebrow="Écritures comptables · Traitement des libellés"
        title="Résultats des modèles d’écritures"
        description="Après le choix du modèle, retrouvez le compte, l’intitulé, le libellé, la banque et le montant hors taxes de chaque mouvement retenu."
        actions={
          <Button variant="outline" asChild>
            <Link href={`/entry-models${clientId ? '?clientId=' + clientId : ''}`}>
              <Settings2 size={16} /> Paramétrer les modèles
            </Link>
          </Button>
        }
      />
      <section className="ui-card entry-model-context">
        <label>
          Client
          <select
            className="ui-input mt-2"
            aria-label="Client du traitement"
            value={clientId}
            disabled={clients.loading}
            onChange={(e) => setClientId(e.target.value)}
          >
            <option value="">Choisir un client…</option>
            {clients.data?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </label>
      </section>
      {clients.error && (
        <p role="alert" className="conversion-alert error">
          {clients.error}
          <Button variant="outline" onClick={clients.reload}>
            Réessayer
          </Button>
        </p>
      )}
      {clientId ? (
        <EntryModelResultsWorkspace
          key={clientId}
          clientId={clientId}
          initialSource={params.get('statementId') || ''}
          initialExtract={params.get('extractId') || ''}
          initialFormat={params.get('templateId') || ''}
          initialAbbreviation={params.get('abbreviation') || ''}
          initialModelId={params.get('modelId') || ''}
          initialOutputTemplateId={params.get('outputTemplateId') || ''}
        />
      ) : (
        <section className="ui-card entry-model-empty">
          <Search size={30} />
          <h2>Choisissez le client à traiter</h2>
          <p>
            Les modèles et les mouvements restent séparés par client. L’aperçu ne modifie pas le
            relevé original.
          </p>
        </section>
      )}
    </div>
  );
}
