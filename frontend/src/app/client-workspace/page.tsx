'use client';
import { Suspense, useEffect, useState } from 'react';
import { useSearchParams, useRouter } from 'next/navigation';
import { Archive } from 'lucide-react';
import { ClientConversions } from '@/components/client-conversions';
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
        description="Retrouvez les relevés, les conversions et les exports de chaque client."
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
                `/client-workspace${event.target.value ? `?clientId=${encodeURIComponent(event.target.value)}` : ''}`,
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
      <ClientConversions key={clientId} clientId={clientId} />
    </div>
  );
}
