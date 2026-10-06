import {
  createContext,
  PropsWithChildren,
  useContext,
  useEffect,
  useMemo,
  useSyncExternalStore,
} from 'react';

import type { AuthSessionState } from './authSession';
import { SessionManager } from './sessionManager';

interface AuthContextValue extends AuthSessionState {
  login(): Promise<void>;
  logout(): Promise<void>;
  retryRestore(): Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({
  children,
  manager,
}: PropsWithChildren<{ manager: SessionManager }>) {
  const session = useSyncExternalStore(
    manager.subscribe,
    manager.getSnapshot,
    manager.getSnapshot,
  );

  useEffect(() => {
    void manager.start();
  }, [manager]);

  const value = useMemo(
    () => ({
      ...session,
      login: manager.login,
      logout: manager.logout,
      retryRestore: manager.retryRestore,
    }),
    [manager, session],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error('useAuth must be used within AuthProvider.');
  }

  return context;
}
