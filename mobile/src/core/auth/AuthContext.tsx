import { useQueryClient } from '@tanstack/react-query';
import {
  createContext,
  PropsWithChildren,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from 'react';

import {
  AuthSessionState,
  AuthStatus,
  initialAuthSessionState,
} from '@/core/auth/authSession';
import {
  secureSessionStore,
  SecureSessionStore,
} from '@/core/storage/secureSessionStore';

interface AuthContextValue {
  status: AuthStatus;
  getAccessToken(): string | null;
  completeAuthentication(accessToken: string): void;
  logout(): Promise<void>;
}

interface AuthProviderProps extends PropsWithChildren {
  credentialStore?: SecureSessionStore;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({
  children,
  credentialStore = secureSessionStore,
}: AuthProviderProps) {
  const queryClient = useQueryClient();
  const [session, setSession] = useState<AuthSessionState>(
    initialAuthSessionState,
  );

  useEffect(() => {
    let isActive = true;

    async function bootstrap() {
      try {
        await credentialStore.read();
      } catch {
        // Storage failure must not create an authenticated session.
      } finally {
        if (isActive) {
          setSession({ status: 'unauthenticated', accessToken: null });
        }
      }
    }

    void bootstrap();

    return () => {
      isActive = false;
    };
  }, [credentialStore]);

  const getAccessToken = useCallback(() => session.accessToken, [session]);

  const completeAuthentication = useCallback((accessToken: string) => {
    if (!accessToken) {
      throw new TypeError('Access token must not be empty.');
    }

    setSession({ status: 'authenticated', accessToken });
  }, []);

  const logout = useCallback(async () => {
    setSession({ status: 'unauthenticated', accessToken: null });
    queryClient.clear();

    await credentialStore.delete();
  }, [credentialStore, queryClient]);

  const value = useMemo<AuthContextValue>(
    () => ({
      status: session.status,
      getAccessToken,
      completeAuthentication,
      logout,
    }),
    [completeAuthentication, getAccessToken, logout, session.status],
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
