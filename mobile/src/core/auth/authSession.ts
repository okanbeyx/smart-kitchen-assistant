export type AuthStatus = 'bootstrapping' | 'unauthenticated' | 'authenticated';

export interface AuthSessionState {
  status: AuthStatus;
  accessToken: string | null;
}

export const initialAuthSessionState: AuthSessionState = {
  status: 'bootstrapping',
  accessToken: null,
};
