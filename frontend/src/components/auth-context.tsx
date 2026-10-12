'use client';
import {
  createContext,
  useContext,
  useEffect,
  useState,
  useRef,
  useCallback,
  FormEvent,
} from 'react';
import { KeyRound, ShieldCheck, FileOutput, ScanLine, Users, LogIn, RefreshCw } from 'lucide-react';
import { ApiError, request } from '@/services/api';
import { confirmDiscard, hasUnsavedChanges } from '@/lib/use-unsaved-changes';
import { Logo } from './logo';
import { ThemeMenu } from './theme-menu';

export type WorkspaceUser = {
  id: string;
  name: string;
  email: string;
  role: 'Admin' | 'User';
  isActive: boolean;
  permissions: string[];
  createdAt: string;
  lastLoginAt: string | null;
  version: number;
};
type Session = { setupRequired: boolean; user: WorkspaceUser | null };
const AuthContext = createContext<{
  user: WorkspaceUser | null;
  can: (permission: string) => boolean;
  logout: () => Promise<void>;
  loggingOut: boolean;
  logoutError: string;
  refresh: () => Promise<void>;
}>({
  user: null,
  can: () => false,
  logout: async () => {},
  loggingOut: false,
  logoutError: '',
  refresh: async () => {},
});
export const useAuth = () => useContext(AuthContext);
export function Can({ permission, children }: { permission: string; children: React.ReactNode }) {
  return useAuth().can(permission) ? children : null;
}
export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);
  const [error, setError] = useState('');
  const sessionRequest = useRef(0);
  const logoutPending = useRef(false);
  const [loggingOut, setLoggingOut] = useState(false);
  const [logoutError, setLogoutError] = useState('');
  const refresh = useCallback(async () => {
    if (logoutPending.current) return;
    const version = ++sessionRequest.current;
    try {
      const result = await request<Session>('/auth/session');
      if (version === sessionRequest.current) {
        setSession(result);
        setError('');
      }
    } catch (e) {
      if (version === sessionRequest.current) setError((e as Error).message);
    }
  }, []);
  useEffect(() => {
    const controller = new AbortController();
    const version = ++sessionRequest.current;
    request<Session>('/auth/session', { signal: controller.signal })
      .then((result) => {
        if (version === sessionRequest.current) {
          setSession(result);
          setError('');
        }
      })
      .catch((e) => {
        if (!controller.signal.aborted && version === sessionRequest.current) setError(e.message);
      });
    const expired = () => {
      sessionRequest.current++;
      setSession({ setupRequired: false, user: null });
    };
    window.addEventListener('alias-session-expired', expired);
    const focus = () => {
      void refresh();
    };
    window.addEventListener('focus', focus);
    return () => {
      controller.abort();
      window.removeEventListener('alias-session-expired', expired);
      window.removeEventListener('focus', focus);
    };
  }, [refresh]);
  async function logout() {
    if (logoutPending.current || !confirmDiscard(hasUnsavedChanges())) return;
    logoutPending.current = true;
    setLoggingOut(true);
    setLogoutError('');
    sessionRequest.current++;
    try {
      try {
        await request('/auth/logout', { method: 'POST' });
      } catch (cause) {
        // An expired session is already signed out.
        if (!(cause instanceof ApiError && cause.status === 401)) throw cause;
      }
      sessionRequest.current++;
      setSession({ setupRequired: false, user: null });
    } catch (cause) {
      setLogoutError((cause as Error).message || 'Déconnexion impossible. Réessayez.');
    } finally {
      logoutPending.current = false;
      setLoggingOut(false);
    }
  }
  const user = session?.user ?? null;
  return (
    <AuthContext.Provider
      value={{
        user,
        can: (key) => user?.role === 'Admin' || !!user?.permissions.includes(key),
        logout,
        loggingOut,
        logoutError,
        refresh,
      }}
    >
      {user ? (
        children
      ) : session ? (
        <AuthScreen
          setup={session.setupRequired}
          onAuthenticated={(user) => {
            sessionRequest.current++;
            setSession({ setupRequired: false, user });
          }}
        />
      ) : (
        <div className="access-loading" role={error ? 'alert' : 'status'}>
          <Logo />
          <p>{error || 'Ouverture de votre espace…'}</p>
          {error && (
            <button className="ui-button" onClick={() => void refresh()}>
              <RefreshCw size={16} /> Réessayer
            </button>
          )}
        </div>
      )}
    </AuthContext.Provider>
  );
}
function AuthScreen({
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
      <section className="access-story">
        <Logo />
        <span className="ui-eyebrow">VOTRE ESPACE ALIAS</span>
        <h1>
          Du relevé bancaire
          <br />
          au fichier comptable.
        </h1>
        <p>Un espace partagé pour lire vos PDF, vérifier les mouvements et préparer vos exports.</p>
        <div className="access-features">
          <span>
            <ScanLine /> Lecture PDF & OCR
          </span>
          <span>
            <FileOutput /> Sage X3 & Sage 100
          </span>
          <span>
            <Users /> Clients & collaborateurs
          </span>
        </div>
        <small>
          <ShieldCheck size={16} /> Des accès adaptés au rôle de chacun.
        </small>
      </section>
      <section className="access-form-panel">
        <div className="access-theme">
          <ThemeMenu />
        </div>
        <form onSubmit={submit} className="access-form">
          <span className="access-icon">
            <KeyRound size={25} />
          </span>
          <p className="ui-eyebrow">{setup ? 'PREMIÈRE OUVERTURE' : 'BON RETOUR'}</p>
          <h2>{setup ? 'Configurez votre espace' : 'Connectez-vous'}</h2>
          <p className="muted">
            {setup
              ? 'Créez le premier administrateur. Vous pourrez ensuite ajouter vos collaborateurs et définir leurs permissions.'
              : 'Retrouvez vos clients, vos relevés et vos fichiers exportés.'}
          </p>
          <fieldset disabled={busy} className="space-y-5 mt-7">
            {setup && (
              <label>
                Votre nom
                <input
                  required
                  maxLength={150}
                  className="ui-input mt-2"
                  autoComplete="name"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                />
              </label>
            )}
            <label>
              Adresse e-mail
              <input
                required
                type="email"
                maxLength={254}
                className="ui-input mt-2"
                autoComplete="username"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </label>
            <label>
              Mot de passe
              <input
                required
                type={visible ? 'text' : 'password'}
                minLength={setup ? 12 : undefined}
                maxLength={128}
                className="ui-input mt-2"
                autoComplete={setup ? 'new-password' : 'current-password'}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
            </label>
            <label className="access-checkbox">
              <input
                type="checkbox"
                checked={visible}
                onChange={(e) => setVisible(e.target.checked)}
              />
              Afficher le mot de passe
            </label>
            {setup && (
              <p className="muted text-xs">
                12 caractères minimum. Aucun compte par défaut n’est créé.
              </p>
            )}
          </fieldset>
          {error && (
            <p role="alert" className="conversion-alert error mt-4">
              {error}
            </p>
          )}
          <button disabled={busy} className="ui-button access-submit">
            <LogIn size={17} />
            {busy ? 'Connexion…' : setup ? 'Créer mon espace' : 'Se connecter'}
          </button>
        </form>
      </section>
    </main>
  );
}
