'use client';
import { FormEvent, useState } from 'react';
import { Users, UserPlus, ShieldCheck, UserCheck, Pencil, Shield, KeyRound } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { BankDialog } from '@/components/bank-dialogs';
import { LoadState } from '@/components/ui/load-state';
import { Pagination } from '@/components/ui/pagination';
import { WorkspaceUser, useAuth } from '@/components/auth-context';
import { Page, request } from '@/services/api';
import { useApi } from '@/services/use-api';
import { PageHeader } from '@/components/page-header';

type Permission = { key: string; group: string; label: string; requires: string[] };
type Catalog = { permissions: Permission[]; presets: { reader: string[]; accountant: string[] } };
export default function UsersPage() {
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<WorkspaceUser | null | undefined>();
  const users = useApi<Page<WorkspaceUser>>(`/users?page=${page}&pageSize=20`);
  const catalog = useApi<Catalog>('/users/permissions');
  return (
    <div className="ui-page users-page">
      <PageHeader
        icon={ShieldCheck}
        eyebrow="Administration"
        title="Votre équipe, les bons accès."
        description="Ajoutez des collaborateurs et choisissez ce qu’ils peuvent consulter, traiter ou télécharger."
        actions={
          <Button disabled={!catalog.data} onClick={() => setEditing(null)}>
            <UserPlus size={17} />
            Ajouter un utilisateur
          </Button>
        }
      />
      <div className="team-summary">
        <div className="ui-card team-stat">
          <Users size={22} />
          <div>
            <strong>{users.data?.total ?? '—'}</strong>
            <small>Utilisateurs enregistrés</small>
          </div>
        </div>
        <div className="ui-card team-stat">
          <UserCheck size={22} />
          <div>
            <strong>{users.data?.items.filter((u) => u.isActive).length ?? '—'}</strong>
            <small>Actifs sur cette page</small>
          </div>
        </div>
        <div className="ui-card team-stat">
          <ShieldCheck size={22} />
          <div>
            <strong>{users.data?.items.filter((u) => u.role === 'Admin').length ?? '—'}</strong>
            <small>Administrateurs sur cette page</small>
          </div>
        </div>
      </div>
      <LoadState
        loading={users.loading || catalog.loading}
        error={users.error || catalog.error}
        empty={!users.data?.items.length}
      />
      {users.data && (
        <section className="ui-card team-table">
          <table>
            <thead>
              <tr>
                <th>Collaborateur</th>
                <th>Rôle</th>
                <th>Accès</th>
                <th>Dernière connexion</th>
                <th>Gestion</th>
              </tr>
            </thead>
            <tbody>
              {users.data.items.map((user) => (
                <tr key={user.id}>
                  <td data-label="Collaborateur">
                    <div className="team-person">
                      <span>{user.name.slice(0, 2).toUpperCase()}</span>
                      <div>
                        <strong>{user.name}</strong>
                        <small>{user.email}</small>
                      </div>
                    </div>
                  </td>
                  <td data-label="Rôle">
                    <span className="ui-tag">
                      {user.role === 'Admin' ? 'Administrateur' : 'Collaborateur'}
                    </span>
                  </td>
                  <td data-label="Accès">
                    <span className={user.isActive ? 'status-positive' : 'ui-tag'}>
                      {user.isActive ? 'Actif' : 'Désactivé'}
                    </span>
                    <p className="muted text-xs mt-1">
                      {user.role === 'Admin'
                        ? 'Tous les droits'
                        : `${user.permissions.length} permissions`}
                    </p>
                  </td>
                  <td data-label="Dernière connexion">
                    {user.lastLoginAt
                      ? new Date(user.lastLoginAt).toLocaleString('fr-FR')
                      : 'Jamais'}
                  </td>
                  <td data-label="Gestion">
                    <Button
                      variant="outline"
                      size="sm"
                      aria-label={`Gérer ${user.name}`}
                      disabled={!catalog.data}
                      onClick={() => setEditing(user)}
                    >
                      <Pencil size={15} />
                      Gérer
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      )}
      {users.data && <Pagination page={page} total={users.data.total} onChange={setPage} />}
      <p className="muted text-xs flex items-center gap-2">
        <Shield size={15} />
        La désactivation, le changement des droits ou du mot de passe révoquent les sessions en
        cours.
      </p>
      {editing !== undefined && catalog.data && (
        <UserEditor
          user={editing}
          catalog={catalog.data}
          onClose={() => setEditing(undefined)}
          onSaved={() => {
            setEditing(undefined);
            users.reload();
          }}
        />
      )}
    </div>
  );
}
function UserEditor({
  user,
  catalog,
  onClose,
  onSaved,
}: {
  user: WorkspaceUser | null;
  catalog: Catalog;
  onClose: () => void;
  onSaved: () => void;
}) {
  const auth = useAuth();
  const [name, setName] = useState(user?.name ?? '');
  const [email, setEmail] = useState(user?.email ?? '');
  const [role, setRole] = useState(user?.role ?? 'User');
  const [active, setActive] = useState(user?.isActive ?? true);
  const [permissions, setPermissions] = useState(user?.permissions ?? catalog.presets.reader);
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  function toggle(key: string, checked: boolean) {
    const selected = new Set(permissions);
    if (checked) {
      const add = (value: string) => {
        selected.add(value);
        catalog.permissions.find((p) => p.key === value)?.requires.forEach(add);
      };
      add(key);
    } else {
      selected.delete(key);
      let changed = true;
      while (changed) {
        changed = false;
        catalog.permissions.forEach((p) => {
          if (selected.has(p.key) && p.requires.some((r) => !selected.has(r))) {
            selected.delete(p.key);
            changed = true;
          }
        });
      }
    }
    setPermissions([...selected]);
  }
  async function save(event: FormEvent) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    setError('');
    try {
      await request(user ? `/users/${user.id}` : '/users', {
        method: user ? 'PUT' : 'POST',
        body: JSON.stringify({
          name,
          email,
          role,
          isActive: active,
          permissions: role === 'Admin' ? catalog.permissions.map((p) => p.key) : permissions,
          password: password || null,
          version: user?.version,
        }),
      });
      onSaved();
      await auth.refresh();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  const self = user?.id === auth.user?.id;
  return (
    <BankDialog
      title={user ? `Gérer ${user.name}` : 'Ajouter un collaborateur'}
      wide
      busy={busy}
      onClose={onClose}
    >
      <form onSubmit={save} className="space-y-5">
        <fieldset disabled={busy} className="space-y-5">
          <div className="grid sm:grid-cols-2 gap-4">
            <label className="text-sm">
              Nom
              <input
                required
                maxLength={150}
                value={name}
                onChange={(e) => setName(e.target.value)}
                className="ui-input mt-2"
              />
            </label>
            <label className="text-sm">
              E-mail
              <input
                required
                type="email"
                maxLength={254}
                autoComplete="off"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="ui-input mt-2"
              />
            </label>
            <label className="text-sm">
              Rôle
              <select
                disabled={self}
                value={role}
                onChange={(e) => setRole(e.target.value as 'User' | 'Admin')}
                className="ui-input mt-2"
              >
                <option value="User">Collaborateur</option>
                <option value="Admin">Administrateur — tous les droits</option>
              </select>
            </label>
            <label className="text-sm">
              {user ? 'Nouveau mot de passe (facultatif)' : 'Mot de passe'}
              <input
                type="password"
                required={!user}
                minLength={12}
                maxLength={128}
                autoComplete="new-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="ui-input mt-2"
              />
            </label>
          </div>
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              disabled={self}
              checked={active}
              onChange={(e) => setActive(e.target.checked)}
            />
            Compte actif
          </label>
          <div className="workspace-identity">
            <KeyRound size={18} />
            <p>
              Mot de passe de 12 caractères minimum. Les données et archives des utilisateurs
              désactivés sont conservées.
            </p>
          </div>
          {role === 'Admin' ? (
            <div className="conversion-clean">
              <ShieldCheck size={18} />
              L’administrateur gère les utilisateurs et possède tous les droits de l’espace partagé.
            </div>
          ) : (
            <>
              <div className="flex gap-2 flex-wrap">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setPermissions(catalog.presets.reader)}
                >
                  Consultation
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setPermissions(catalog.presets.accountant)}
                >
                  Traitement comptable
                </Button>
              </div>
              <div className="permission-grid">
                {[...new Set(catalog.permissions.map((p) => p.group))].map((group) => (
                  <section className="permission-group" key={group}>
                    <h3>{group}</h3>
                    {catalog.permissions
                      .filter((p) => p.group === group)
                      .map((p) => (
                        <label key={p.key}>
                          <input
                            type="checkbox"
                            checked={permissions.includes(p.key)}
                            onChange={(e) => toggle(p.key, e.target.checked)}
                          />
                          <span>{p.label}</span>
                        </label>
                      ))}
                  </section>
                ))}
              </div>
              <p className="muted text-xs">
                Les droits de consultation nécessaires sont activés avec chaque action.
              </p>
            </>
          )}
        </fieldset>
        {error && (
          <p role="alert" className="conversion-alert error">
            {error}
          </p>
        )}
        <div className="flex justify-end gap-3">
          <Button type="button" disabled={busy} variant="outline" onClick={onClose}>
            Annuler
          </Button>
          <Button disabled={busy}>
            {busy ? 'Enregistrement…' : user ? 'Enregistrer les accès' : 'Créer l’utilisateur'}
          </Button>
        </div>
      </form>
    </BankDialog>
  );
}
