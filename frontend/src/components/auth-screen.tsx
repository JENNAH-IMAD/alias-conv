'use client';
import { useState, type FormEvent } from 'react';
import {
  AlertCircle,
  ArrowRight,
  Check,
  Eye,
  EyeOff,
  FileOutput,
  FileText,
  KeyRound,
  LoaderCircle,
  LockKeyhole,
  Mail,
  ShieldCheck,
  UserRound,
} from 'lucide-react';
import { request } from '@/services/api';
import type { WorkspaceUser } from './auth-context';
import { Logo } from './logo';
import { ThemeMenu } from './theme-menu';

export function AuthScreen({
  setup,
  onAuthenticated,
}: {
  setup: boolean;
  onAuthenticated: (user: WorkspaceUser) => void;
}) {
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [visible, setVisible] = useState(false);
  const [capsLock, setCapsLock] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    setError('');
    try {
      onAuthenticated(
        await request<WorkspaceUser>(`/auth/${setup ? 'setup' : 'login'}`, {
          method: 'POST',
          body: JSON.stringify({ name, email, password }),
        }),
      );
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <main className="access-page">
      <section className="access-story" data-theme="dark" aria-labelledby="access-story-title">
        <div className="access-brand">
          <Logo />
          <span>Votre espace bancaire</span>
        </div>
        <div className="access-story-content">
          <p className="access-eyebrow">
            <span /> RELEVÉS & EXTRAITS BANCAIRES
          </p>
          <h2 id="access-story-title">
            Vos relevés.
            <br />
            Vos extraits.
            <br />
            <span>Un seul espace.</span>
          </h2>
          <p className="access-story-description">
            Relevés et extraits bancaires : de la lecture du PDF à l’export comptable.
          </p>
          <div className="access-document-preview" aria-hidden="true">
            <div className="access-preview-paper">
              <div className="access-preview-heading">
                <span>
                  <FileText size={18} />
                </span>
                <div>
                  <strong>Relevé ou extrait bancaire</strong>
                  <small>Votre document source</small>
                </div>
                <span className="access-preview-pdf">PDF</span>
              </div>
              <div className="access-preview-table">
                <div>
                  <span>Date</span>
                  <span>Libellé</span>
                  <span>Montant</span>
                </div>
                {[0, 1, 2].map((row) => (
                  <div key={row}>
                    <i />
                    <i />
                    <i />
                  </div>
                ))}
              </div>
              <div className="access-preview-bottom">
                <ShieldCheck size={14} /> Vérification des mouvements
              </div>
            </div>
            <div className="access-preview-export">
              <span>
                <FileOutput size={21} />
              </span>
              <div>
                <strong>Prêt pour votre comptabilité</strong>
                <small>Sage X3 · Sage 100 · CSV</small>
              </div>
              <ArrowRight size={18} />
            </div>
          </div>
          <div className="access-workflow">
            <span>
              <Check size={13} /> Importer
            </span>
            <i />
            <span>
              <Check size={13} /> Vérifier
            </span>
            <i />
            <span>
              <Check size={13} /> Exporter
            </span>
          </div>
        </div>
        <div className="access-story-footer">
          <ShieldCheck size={16} />
          <span>Un espace partagé. Des accès adaptés à chaque rôle.</span>
        </div>
      </section>
      <section className="access-form-panel" aria-labelledby="access-form-title">
        <header className="access-panel-header">
          <span className="access-mobile-brand">
            <Logo />
          </span>
          <span className="access-panel-label">Espace de travail</span>
          <ThemeMenu />
        </header>
        <div className="access-form-container">
          <form
            onSubmit={submit}
            className="access-form"
            aria-labelledby="access-form-title"
            aria-busy={busy}
          >
            <span className="access-icon" aria-hidden="true">
              <KeyRound size={23} />
            </span>
            <p className="access-form-eyebrow">
              {setup ? 'PREMIÈRE OUVERTURE' : 'HEUREUX DE VOUS RETROUVER'}
            </p>
            <h1 id="access-form-title">{setup ? 'Créez votre espace' : 'Bienvenue sur Alias'}</h1>
            <p className="access-form-description">
              {setup
                ? 'Configurez votre compte administrateur pour commencer.'
                : 'Connectez-vous pour traiter vos relevés et extraits bancaires.'}
            </p>
            <fieldset disabled={busy} className="access-fields">
              <legend className="sr-only">
                {setup ? 'Informations du premier administrateur' : 'Identifiants de connexion'}
              </legend>
              {setup && (
                <div className="access-field">
                  <label htmlFor="access-name">Votre nom</label>
                  <div className="access-input-wrap">
                    <UserRound size={17} aria-hidden="true" />
                    <input
                      id="access-name"
                      required
                      maxLength={150}
                      className="ui-input"
                      autoComplete="name"
                      placeholder="Votre nom complet"
                      value={name}
                      onChange={(e) => {
                        setName(e.target.value);
                        setError('');
                      }}
                    />
                  </div>
                </div>
              )}
              <div className="access-field">
                <label htmlFor="access-email">Adresse e-mail</label>
                <div className="access-input-wrap">
                  <Mail size={17} aria-hidden="true" />
                  <input
                    id="access-email"
                    required
                    type="email"
                    inputMode="email"
                    autoCapitalize="none"
                    spellCheck={false}
                    maxLength={254}
                    className="ui-input"
                    autoComplete="username"
                    placeholder="vous@entreprise.com"
                    value={email}
                    onChange={(e) => {
                      setEmail(e.target.value);
                      setError('');
                    }}
                  />
                </div>
              </div>
              <div className="access-field">
                <label htmlFor="access-password">Mot de passe</label>
                <div className="access-input-wrap">
                  <LockKeyhole size={17} aria-hidden="true" />
                  <input
                    id="access-password"
                    required
                    type={visible ? 'text' : 'password'}
                    minLength={setup ? 12 : undefined}
                    maxLength={128}
                    className="ui-input access-password"
                    autoComplete={setup ? 'new-password' : 'current-password'}
                    placeholder={setup ? 'Créez un mot de passe' : 'Votre mot de passe'}
                    aria-describedby={setup || capsLock ? 'access-password-help' : undefined}
                    value={password}
                    onChange={(e) => {
                      setPassword(e.target.value);
                      setError('');
                    }}
                    onKeyUp={(e) => setCapsLock(e.getModifierState('CapsLock'))}
                    onKeyDown={(e) => setCapsLock(e.getModifierState('CapsLock'))}
                    onBlur={() => setCapsLock(false)}
                  />
                  <button
                    type="button"
                    className="access-password-toggle"
                    onClick={() => setVisible(!visible)}
                    aria-label={visible ? 'Masquer le mot de passe' : 'Afficher le mot de passe'}
                    aria-pressed={visible}
                    title={visible ? 'Masquer le mot de passe' : 'Afficher le mot de passe'}
                  >
                    {visible ? <EyeOff size={17} /> : <Eye size={17} />}
                  </button>
                </div>
                {(setup || capsLock) && (
                  <p id="access-password-help" className="access-password-help" aria-live="polite">
                    {capsLock
                      ? 'La touche Verr. Maj est activée.'
                      : '12 caractères minimum pour protéger votre compte.'}
                  </p>
                )}
              </div>
            </fieldset>
            {error && (
              <p role="alert" className="access-error">
                <AlertCircle size={17} />
                <span>{error}</span>
              </p>
            )}
            <button type="submit" disabled={busy} className="ui-button access-submit">
              {busy ? <LoaderCircle size={17} className="animate-spin" /> : null}
              <span>
                {busy
                  ? setup
                    ? 'Création en cours…'
                    : 'Connexion en cours…'
                  : setup
                    ? 'Créer mon espace'
                    : 'Se connecter'}
              </span>
              {!busy && <ArrowRight size={17} />}
            </button>
            <p className="access-account-help">
              {setup
                ? 'Vous pourrez ensuite inviter vos collaborateurs et gérer leurs permissions.'
                : 'Besoin d’un accès ? Contactez l’administrateur de votre espace.'}
            </p>
          </form>
          <p className="access-form-footer">
            <LockKeyhole size={13} /> Accès réservé aux utilisateurs autorisés
          </p>
        </div>
        <footer className="access-panel-footer">
          <span>Alias Convert PDF</span>
          <span>Relevés & extraits bancaires</span>
        </footer>
      </section>
    </main>
  );
}
