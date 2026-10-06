import { useQueryClient } from '@tanstack/react-query';
import {
  createContext,
  PropsWithChildren,
  useContext,
  useEffect,
  useMemo,
  useState,
  useSyncExternalStore,
} from 'react';

import { createAuth0OidcClient } from './auth0OidcClient';
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
  manager: suppliedManager,
}: PropsWithChildren<{ manager?: SessionManager }>) {
  const queryClient = useQueryClient();
  const [manager] = useState(
    () =>
      suppliedManager ??
      new SessionManager(createAuth0OidcClient, async () => {
        // No public-only cache namespace exists yet; clear the shared client at account boundaries.
        try {
          await queryClient.cancelQueries();
        } finally {
          queryClient.clear();
        }
      }),
  );
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
  if (!context) throw new Error('useAuth must be used within AuthProvider.');
  return context;
}
