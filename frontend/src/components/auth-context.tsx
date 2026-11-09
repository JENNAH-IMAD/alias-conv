'use client';
import { createContext, useContext, useEffect, useState, useRef, useCallback } from 'react';
import { RefreshCw } from 'lucide-react';
import { ApiError, request } from '@/services/api';
import { useRouter } from 'next/navigation';
import { confirmDiscard, hasUnsavedChanges } from '@/lib/use-unsaved-changes';
import { Logo } from './logo';
import { AuthScreen } from './auth-screen';

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
  const router = useRouter();
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
            router.replace('/dashboard');
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
