'use client';

import { FormEvent, useEffect, useState } from 'react';
import { KeyRound, Pencil, Plus, Search, ShieldCheck, Trash2, Users } from 'lucide-react';
import { useAuth } from '@/components/auth-context';
import { BankDialog } from '@/components/bank-dialogs';
import { Avatar } from '@/components/avatar';
import { LoadState, Pagination } from '@/components/resource-editor';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Page, request, User } from '@/services/api';
import { UserPermissions } from '@/components/user-permissions';
import { useApi } from '@/services/use-api';

type ManagedUser = User & { createdAt: string };

function UserEditor({ initial, self, onClose, onSaved }: { initial: ManagedUser | null; self: boolean; onClose: () => void; onSaved: () => void }) {
  const [name, setName] = useState(initial?.name || '');
  const [email, setEmail] = useState(initial?.email || '');
  const [role, setRole] = useState(initial?.role || 'User');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const { logout } = useAuth();
  async function submit(event: FormEvent) {
    event.preventDefault(); if (busy) return;
    setBusy(true); setError('');
    try {
      await request(`/users${initial ? `/${initial.id}` : ''}`, { method: initial ? 'PUT' : 'POST', body: JSON.stringify({ name: name.trim(), email: email.trim(), role, password: password || null }) });
      if (self && initial && (name.trim() !== initial.name || email.trim().toLowerCase() !== initial.email || password)) logout();
      else onSaved();
      onClose();
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  return <BankDialog title={initial ? 'Modifier l’utilisateur' : 'Créer un utilisateur'} onClose={onClose} busy={busy}><form onSubmit={submit} className="space-y-5"><fieldset disabled={busy} className="space-y-5"><label className="block"><span className="ui-label">Nom complet</span><Input required maxLength={100} autoComplete="off" value={name} onChange={e => setName(e.target.value)}/></label><label className="block"><span className="ui-label">Adresse e-mail</span><Input required type="email" maxLength={254} autoComplete="off" value={email} onChange={e => setEmail(e.target.value)}/></label><label htmlFor="user-role" className="block"><span className="ui-label">Rôle</span><select id="user-role" className="ui-input" value={role} disabled={self} onChange={e => setRole(e.target.value)}><option value="User">Utilisateur</option><option value="Admin">Administrateur</option></select></label><p className="text-xs leading-5 text-muted-foreground">Les administrateurs gèrent les utilisateurs, les banques et la configuration. Vous conservez votre propre rôle administrateur.</p><label className="block"><span className="ui-label">{initial ? 'Nouveau mot de passe (facultatif)' : 'Mot de passe'}</span><Input type="password" autoComplete="new-password" required={!initial} minLength={12} maxLength={128} value={password} onChange={e => setPassword(e.target.value)}/></label><p className="text-xs leading-5 text-muted-foreground">12 à 128 caractères, avec majuscule, minuscule et chiffre.{initial && ' Laissez vide pour conserver le mot de passe. Toute modification du compte impose une nouvelle connexion.'}</p></fieldset>{error && <p role="alert" className="text-sm text-destructive">{error}</p>}<div className="flex justify-end gap-3"><Button type="button" variant="outline" disabled={busy} onClick={onClose}>Annuler</Button><Button disabled={busy}>{busy ? 'Enregistrement…' : 'Enregistrer'}</Button></div></form></BankDialog>;
}

function UserManagement() {
  const { user } = useAuth();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [term, setTerm] = useState('');
  const [role, setRole] = useState('');
  const [editing, setEditing] = useState<ManagedUser | null | undefined>(undefined);
  const [permissionUser, setPermissionUser] = useState<ManagedUser | null>(null);
  const [deleting, setDeleting] = useState<ManagedUser | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  useEffect(() => { const timer = setTimeout(() => { setTerm(search); setPage(1); }, 300); return () => clearTimeout(timer); }, [search]);
  const result = useApi<Page<ManagedUser>>(`/users?page=${page}&pageSize=20&search=${encodeURIComponent(term)}&role=${role}`);
  async function remove() {
    if (!deleting || busy) return;
    setBusy(true); setError('');
    try {
      await request(`/users/${deleting.id}`, { method: 'DELETE' });
      setDeleting(null); setNotice('Utilisateur supprimé.');
      if (page > 1 && result.data?.items.length === 1) setPage(page - 1); else result.reload();
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  return <div className="ui-page"><section className="ui-hero flex flex-wrap items-center justify-between gap-5"><div><p className="ui-eyebrow">ADMINISTRATION</p><h1 className="ui-title">Utilisateurs</h1><p className="ui-description">Gérez les comptes, les rôles et les accès à votre espace.</p></div><Button onClick={() => setEditing(null)}><Plus/> Nouvel utilisateur</Button></section>
    {notice && <p role="status" className="text-sm text-[var(--success-text)]">{notice}</p>}
    <Card><CardContent><div className="mb-5 flex items-center gap-2"><Users className="size-5 text-primary"/><h2 className="font-semibold">Annuaire des utilisateurs</h2><Badge variant="secondary">{result.data?.total ?? '—'}</Badge></div><div className="mb-5 grid gap-3 sm:grid-cols-[1fr_210px]"><div className="relative"><Search className="absolute top-2.5 left-3 size-4 text-muted-foreground"/><Input className="pl-9" type="search" aria-label="Rechercher un utilisateur" placeholder="Nom ou adresse e-mail…" value={search} onChange={e => setSearch(e.target.value)}/></div><select className="ui-input" aria-label="Filtrer par rôle" value={role} onChange={e => { setRole(e.target.value); setPage(1); }}><option value="">Tous les rôles</option><option value="Admin">Administrateurs</option><option value="User">Utilisateurs</option></select></div><LoadState error={result.error} loading={result.loading} empty={!result.data?.items.length}/>
      {!!result.data?.items.length && <Table><TableHeader><TableRow><TableHead>Utilisateur</TableHead><TableHead>Rôle</TableHead><TableHead>Créé le</TableHead><TableHead className="text-right">Actions</TableHead></TableRow></TableHeader><TableBody>{result.data.items.map(item => <TableRow key={item.id}><TableCell><div className="flex items-center gap-3"><Avatar name={item.name} small/><div><p className="font-medium">{item.name} {item.id === user?.id && <span className="text-xs text-muted-foreground">(vous)</span>}</p><p className="mt-1 text-xs text-muted-foreground">{item.email}</p></div></div></TableCell><TableCell><Badge variant={item.role === 'Admin' ? 'default' : 'secondary'}>{item.role === 'Admin' && <ShieldCheck/>}{item.role === 'Admin' ? 'Administrateur' : 'Utilisateur'}</Badge></TableCell><TableCell className="text-xs text-muted-foreground">{new Date(item.createdAt).toLocaleDateString('fr-FR')}</TableCell><TableCell><div className="flex flex-wrap justify-end gap-2"><Button size="sm" variant="outline" onClick={() => setPermissionUser(item)} aria-label={`Permissions de ${item.name}`}><ShieldCheck/> Permissions</Button><Button size="sm" variant="edit" onClick={() => setEditing(item)} aria-label={`Modifier ${item.name}`}><Pencil/> Modifier</Button><Button size="icon-sm" variant="ghost" disabled={item.id === user?.id} className="text-destructive" aria-label={`Supprimer ${item.name}`} onClick={() => { setDeleting(item); setError(''); }}><Trash2/></Button></div></TableCell></TableRow>)}</TableBody></Table>}
      {!result.loading && <div className="mt-5"><Pagination page={page} total={result.data?.total || 0} onChange={setPage}/></div>}
    </CardContent></Card><p className="flex items-start gap-2 text-xs leading-5 text-muted-foreground"><KeyRound className="mt-0.5 size-4 shrink-0"/>La réinitialisation du mot de passe est disponible dans « Modifier ». Les comptes liés à des conversions sont conservés pour préserver l’historique.</p>
    {permissionUser && <UserPermissions user={permissionUser} onClose={() => setPermissionUser(null)} onSaved={() => { setNotice('Permissions enregistrées. L’utilisateur devra se reconnecter.'); result.reload(); }}/>}
    {editing !== undefined && <UserEditor initial={editing} self={editing?.id === user?.id} onClose={() => setEditing(undefined)} onSaved={() => { setNotice('Utilisateur enregistré.'); result.reload(); }}/>} 
    {deleting && <BankDialog title="Supprimer cet utilisateur ?" busy={busy} onClose={() => setDeleting(null)}><p className="text-sm leading-6">Le compte de <strong>{deleting.name}</strong> ({deleting.email}) sera supprimé. Cette action est définitive.</p>{error && <p className="mt-4 text-sm text-destructive" role="alert">{error}</p>}<div className="mt-6 flex justify-end gap-3"><Button variant="outline" disabled={busy} onClick={() => setDeleting(null)}>Annuler</Button><Button variant="destructive" disabled={busy} onClick={() => void remove()}>{busy ? 'Suppression…' : 'Confirmer la suppression'}</Button></div></BankDialog>}
  </div>;
}

export default function UsersPage() {
  const { user } = useAuth();
  if (user?.role !== 'Admin') return <div className="ui-page"><h1 className="ui-title">Accès réservé</h1><p className="ui-description">La gestion des utilisateurs est réservée aux administrateurs.</p></div>;
  return <UserManagement/>;
}

