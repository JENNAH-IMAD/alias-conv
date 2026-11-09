'use client';
import { useState, type FormEvent } from 'react';
import { KeyRound, LoaderCircle, UserRound } from 'lucide-react';
import { PageHeader } from '@/components/page-header';
import { Button } from '@/components/ui/button';
import { useAuth } from '@/components/auth-context';
import { request } from '@/services/api';

export default function ProfilePage() {
  const { user, refresh } = useAuth();
  const [name, setName] = useState(user?.name ?? '');
  const [email, setEmail] = useState(user?.email ?? '');
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [saved, setSaved] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError('');
    setSaved(false);
    try {
      await request('/auth/profile', {
        method: 'PUT',
        body: JSON.stringify({ name, email, currentPassword, newPassword }),
      });
      await refresh();
      setCurrentPassword('');
      setNewPassword('');
      setSaved(true);
    } catch (cause) {
      setError((cause as Error).message || 'Impossible d’enregistrer les modifications.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="ui-page profile-page">
      <PageHeader
        eyebrow="Mon compte"
        title="Gérer mon profil"
        description="Mettez à jour vos coordonnées et sécurisez votre accès à l’espace."
        icon={UserRound}
      />
      <form className="profile-card" onSubmit={submit}>
        <section className="profile-form-section" aria-labelledby="profile-contact-title">
          <div className="profile-section-heading">
            <UserRound size={18} aria-hidden="true" />
            <div>
              <h2 id="profile-contact-title">Informations personnelles</h2>
              <p>Ces informations sont utilisées pour identifier votre compte.</p>
            </div>
          </div>
          <div className="profile-fields">
            <label>
              Nom complet
              <input
                className="ui-input"
                value={name}
                onChange={(event) => setName(event.target.value)}
                maxLength={150}
                autoComplete="name"
                required
              />
            </label>
            <label>
              Adresse e-mail
              <input
                className="ui-input"
                type="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                maxLength={254}
                autoComplete="email"
                required
              />
            </label>
          </div>
        </section>
        <section className="profile-form-section" aria-labelledby="profile-password-title">
          <div className="profile-section-heading">
            <KeyRound size={18} aria-hidden="true" />
            <div>
              <h2 id="profile-password-title">Changer le mot de passe</h2>
              <p>Laissez les champs vides si vous ne souhaitez pas le modifier.</p>
            </div>
          </div>
          <div className="profile-fields">
            <label>
              Mot de passe actuel
              <input
                className="ui-input"
                type="password"
                value={currentPassword}
                onChange={(event) => setCurrentPassword(event.target.value)}
                autoComplete="current-password"
                required={!!newPassword}
              />
            </label>
            <label>
              Nouveau mot de passe
              <input
                className="ui-input"
                type="password"
                value={newPassword}
                onChange={(event) => setNewPassword(event.target.value)}
                minLength={12}
                maxLength={128}
                autoComplete="new-password"
                aria-describedby="profile-password-hint"
              />
              <span id="profile-password-hint" className="profile-field-hint">
                12 caractères minimum.
              </span>
            </label>
          </div>
        </section>
        {(error || saved) && (
          <p className={`profile-feedback ${error ? 'is-error' : 'is-success'}`} role={error ? 'alert' : 'status'}>
            {error || 'Votre profil a été mis à jour.'}
          </p>
        )}
        <div className="profile-actions">
          <span>{user?.role === 'Admin' ? 'Administrateur' : 'Utilisateur'}</span>
          <Button type="submit" disabled={busy}>
            {busy && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
            {busy ? 'Enregistrement…' : 'Enregistrer le profil'}
          </Button>
        </div>
      </form>
    </div>
  );
}
