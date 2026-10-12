'use client';
import { Button } from '@/components/ui/button';
import Link from 'next/link';
import { CatalogDelete } from '@/components/catalog-delete';
import { useState } from 'react';
import { useAuth } from '@/components/auth-context';
import { Camera, Plus, Pencil, Trash2, Mail, Phone, MapPin, Landmark, Users } from 'lucide-react';
import { ClientAvatar, ClientPhotoEditor } from '@/components/client-photo';
import { ResourceEditor } from '@/components/resource-editor';
import { LoadState } from '@/components/ui/load-state';
import { Pagination } from '@/components/ui/pagination';
import { Client, Page } from '@/services/api';
import { useApi } from '@/services/use-api';
import { PageHeader } from '@/components/page-header';
export default function ClientsPage() {
  const { can } = useAuth();
  const [page, setPage] = useState(1);
  const { data, error, loading, reload } = useApi<Page<Client>>(
    '/clients?page=' + page + '&pageSize=20',
  );
  const clients = data?.items || [];
  const [photoClient, setPhotoClient] = useState<Client | null>(null);
  const [editing, setEditing] = useState<Client | null | undefined>(undefined);
  const [deleting, setDeleting] = useState<Client | null>(null);
  return (
    <div className="ui-page clients-page">
      {deleting && (
        <CatalogDelete
          resource="clients"
          id={deleting.id}
          name={deleting.name}
          onClose={() => setDeleting(null)}
          onDeleted={() => {
            if (clients.length === 1 && page > 1) setPage(page - 1);
            else reload();
          }}
        />
      )}
      {photoClient && (
        <ClientPhotoEditor
          client={photoClient}
          onClose={() => setPhotoClient(null)}
          onSaved={reload}
        />
      )}
      {editing !== undefined && (
        <ResourceEditor
          resource="clients"
          initial={editing || undefined}
          onClose={() => setEditing(undefined)}
          onSaved={reload}
        />
      )}
      <PageHeader
        icon={Users}
        eyebrow="Référentiel"
        title="Clients"
        description="Retrouvez leurs coordonnées, leurs informations fiscales et leurs banques."
        actions={
          <Button permission="clients.write" onClick={() => setEditing(null)}>
            <Plus size={17} /> Nouveau client
          </Button>
        }
      />
      <div className="flex items-center justify-between gap-3">
        <div className="flex items-center gap-2 font-semibold">
          <Users size={18} className="accent-text" /> Tous les clients{' '}
          <span className="ui-tag">{data?.total ?? '—'}</span>
        </div>
        <span className="muted text-xs">Répertoire clients</span>
      </div>
      <LoadState error={error} loading={loading} empty={!clients.length} />
      <section aria-label="Liste des clients" className="catalog-grid client-grid">
        {clients.map((client) => (
          <article key={client.id} className="ui-card client-card">
            <div className="flex items-start justify-between gap-3">
              <div className="flex min-w-0 items-center gap-3">
                <button
                  type="button"
                  className="client-photo-button"
                  disabled={!can('clients.write')}
                  title="Modifier la photo ou le logo"
                  aria-label={`Modifier la photo de ${client.name}`}
                  onClick={() => setPhotoClient(client)}
                >
                  <ClientAvatar client={client} />
                  <span className="photo-camera">
                    <Camera size={11} />
                  </span>
                </button>
                <div className="min-w-0">
                  <h2 className="truncate text-base font-bold" title={client.name}>
                    {client.name}
                  </h2>
                  <p className="muted mt-1 truncate text-xs" title={client.legalName}>
                    {client.legalName}
                  </p>
                </div>
              </div>
              <span className={client.status === 'Actif' ? 'status-positive' : 'ui-tag'}>
                {client.status}
              </span>
            </div>
            <div className="my-5">
              <div className="client-contact">
                <Mail size={15} />
                {client.email ? (
                  <a href={`mailto:${client.email}`} title={client.email}>
                    {client.email}
                  </a>
                ) : (
                  <span>Email non renseigné</span>
                )}
              </div>
              <div className="client-contact">
                <Phone size={15} />
                {client.phone ? (
                  <a href={`tel:${client.phone}`}>{client.phone}</a>
                ) : (
                  <span>Téléphone non renseigné</span>
                )}
              </div>
              <div className="client-contact">
                <MapPin size={15} />
                {client.country || 'Pays non renseigné'}
              </div>
            </div>
            <div className="rounded-lg bg-[var(--surface-alt)] px-3">
              <div className="ui-row">
                <span className="muted">ICE</span>
                <span className="ui-value">{client.ice || '—'}</span>
              </div>
              <div className="ui-row">
                <span className="muted">Identifiant fiscal</span>
                <span className="ui-value">{client.if || '—'}</span>
              </div>
              <div className="ui-row">
                <span className="muted">Registre de commerce</span>
                <span className="ui-value">{client.rc || '—'}</span>
              </div>
            </div>
            <div className="mt-5 flex flex-wrap items-center gap-2">
              <Landmark size={14} className="muted" />
              {client.banks.length ? (
                client.banks.map((bank) => (
                  <span key={bank} className="ui-tag">
                    {bank}
                  </span>
                ))
              ) : (
                <span className="muted text-xs">Aucune banque associée</span>
              )}
            </div>
            {
              <div className="client-actions flex-wrap gap-2">
                <Link className="button-secondary" href={`/client-workspace?clientId=${client.id}`}>
                  Archives
                </Link>
                <Button
                  permission="clients.write"
                  variant="edit"
                  onClick={() => setEditing(client)}
                >
                  <Pencil size={14} /> Modifier le client
                </Button>
                <button
                  className="icon-button danger-button"
                  disabled={deleting !== null || !can('clients.write')}
                  onClick={() => setDeleting(client)}
                  aria-label={`Supprimer ${client.name}`}
                  title="Supprimer le client"
                >
                  <Trash2 size={16} />
                </button>
              </div>
            }
          </article>
        ))}
      </section>
      <Pagination page={page} total={data?.total || 0} onChange={setPage} />
    </div>
  );
}
