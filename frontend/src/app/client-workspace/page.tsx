'use client';
import { Suspense, useEffect, useState } from 'react';
import { useSearchParams, useRouter } from 'next/navigation';
import { Archive } from 'lucide-react';
import { ClientConversions } from '@/components/client-conversions';
import { ClientExtracts } from '@/components/client-extracts';
import { all, Client } from '@/services/api';
import { PageHeader } from '@/components/page-header';

export default function ClientWorkspacePage() {
  return (
    <Suspense fallback={<p role="status">Chargement des relevés…</p>}>
      <ClientWorkspace />
    </Suspense>
  );
}
function ClientWorkspace() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const [clients, setClients] = useState<Client[]>([]);
  const [error, setError] = useState('');
  const clientId = searchParams.get('clientId') ?? '';
  const section = searchParams.get('section') === 'extracts' ? 'extracts' : 'statements';
  useEffect(() => {
    let active = true;
    all<Client>('/clients')
      .then((items) => {
        if (active) setClients(items);
      })
      .catch((error) => {
        if (active) setError(error.message);
      });
    return () => {
      active = false;
    };
  }, []);
  const client = clients.find((item) => item.id === clientId);
  return (
    <div className="ui-page client-workspace-page">
      <PageHeader
        icon={Archive}
        eyebrow="Espace clients"
        title="L’espace de vos clients."
        description="Retrouvez les relevés bancaires et les extraits avec leurs mouvements et leurs calculs."
      />
      <section className="ui-card workspace-client-selector">
        <label className="workspace-client-choice">
          Client
          <select
            aria-label="Client"
            className="ui-input mt-2"
            value={clientId}
            onChange={(event) =>
              router.replace(
                `/client-workspace?section=${section}${event.target.value ? `&clientId=${encodeURIComponent(event.target.value)}` : ''}`,
              )
            }
          >
            <option value="">Tous les clients et relevés à classer</option>
            {clients.map((client) => (
              <option key={client.id} value={client.id}>
                {client.name}
              </option>
            ))}
          </select>
        </label>
        {client && (
          <div className="workspace-profile">
            <span className="workspace-client-avatar">{client.name.slice(0, 2).toUpperCase()}</span>
            <div>
              <h2>{client.name}</h2>
              <p className="muted text-sm mt-1">
                {client.legalName} · {client.email || 'Espace partagé'}
              </p>
            </div>
          </div>
        )}
      </section>
      {error && (
        <p role="alert" className="text-destructive">
          {error}
        </p>
      )}
      <div
        className="conversion-tabs workspace-document-tabs"
        role="tablist"
        aria-label="Documents clients"
      >
        {(
          [
            ['statements', 'Relevés bancaires'],
            ['extracts', 'Extraits de relevés bancaires'],
          ] as const
        ).map(([value, label]) => (
          <button
            key={value}
            type="button"
            role="tab"
            id={`workspace-tab-${value}`}
            aria-selected={section === value}
            aria-controls={`workspace-panel-${value}`}
            tabIndex={section === value ? 0 : -1}
            onKeyDown={(event) => {
              if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
              event.preventDefault();
              const target =
                event.key === 'Home'
                  ? 'statements'
                  : event.key === 'End'
                    ? 'extracts'
                    : section === 'statements'
                      ? 'extracts'
                      : 'statements';
              document.getElementById(`workspace-tab-${target}`)?.click();
              document.getElementById(`workspace-tab-${target}`)?.focus();
            }}
            onClick={() =>
              router.replace(
                `/client-workspace?section=${value}${clientId ? `&clientId=${encodeURIComponent(clientId)}` : ''}`,
              )
            }
          >
            {label}
          </button>
        ))}
      </div>
      <section
        role="tabpanel"
        id={`workspace-panel-${section}`}
        aria-labelledby={`workspace-tab-${section}`}
      >
        {section === 'extracts' ? (
          <div className="space-y-8">
            <ClientConversions
              key={`sources-${clientId}`}
              clientId={clientId}
              conversionType="EXTRACT"
            />
            <details className="ui-card">
              <summary className="cursor-pointer font-semibold">
                Filtres sauvegardés sur les mouvements
              </summary>
              <div className="mt-5">
                <ClientExtracts key={`extracts-${clientId}`} clientId={clientId} />
              </div>
            </details>
          </div>
        ) : (
          <ClientConversions key={clientId} clientId={clientId} />
        )}
      </section>
    </div>
  );
}
