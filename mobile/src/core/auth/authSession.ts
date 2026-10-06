import type { AuthErrorKind } from './authError';

export type AuthStatus = 'bootstrapping' | 'unauthenticated' | 'authenticated';

export interface AuthSessionState {
  status: AuthStatus;
  pending: 'restore' | 'login' | 'logout' | null;
  error: AuthErrorKind | null;
  cleanupRequired: boolean;
}

export const initialAuthSessionState: AuthSessionState = {
  status: 'bootstrapping',
  pending: 'restore',
  error: null,
  cleanupRequired: false,
};
