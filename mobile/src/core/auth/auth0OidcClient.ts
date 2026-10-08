import { Platform } from 'react-native';
import Auth0, {
  CredentialsManagerError,
  WebAuthError,
  WebAuthErrorCodes,
} from 'react-native-auth0';

import { AuthError } from '@/core/auth/authError';
import {
  Auth0Environment,
  getAuth0Environment,
  parseAuth0Environment,
} from '@/core/config/environment';

export interface AccessCredentials {
  accessToken: string;
  expiresAt: number;
}

export interface GetCredentialsOptions {
  forceRefresh?: boolean;
}

export interface OidcClient {
  authorize(): Promise<void>;
  getCredentials(options?: GetCredentialsOptions): Promise<AccessCredentials>;
  revokeRefreshToken(): Promise<void>;
  clearCredentials(): Promise<void>;
  clearSession(): Promise<void>;
}

function webAuthError(error: unknown): AuthError {
  return new AuthError(
    error instanceof WebAuthError &&
      error.type === WebAuthErrorCodes.USER_CANCELLED
      ? 'cancelled'
      : 'provider',
  );
}

function credentialError(error: unknown): AuthError {
  if (!(error instanceof CredentialsManagerError))
    return new AuthError('internal');
  if (error.type === 'NO_CREDENTIALS') return new AuthError('no-session');
  if (
    ['INVALID_CREDENTIALS', 'NO_REFRESH_TOKEN', 'SESSION_EXPIRED'].includes(
      error.type,
    ) ||
    ['invalid_grant', 'invalid_refresh_token'].includes(error.code)
  ) {
    return new AuthError('invalid-session');
  }
  if (
    error.type === 'NO_NETWORK' ||
    ['a0.network_error', 'request_error'].includes(error.code)
  ) {
    return new AuthError('network');
  }
  // RENEW_FAILED alone does not prove revocation (notably on Android).
  if (['RENEW_FAILED', 'API_ERROR'].includes(error.type))
    return new AuthError('provider');
  if (error.type === 'STORE_FAILED') return new AuthError('storage');
  return new AuthError('internal');
}

// No singleton is initialized on import; Phase 2 will own the client lifecycle.
export function createAuth0OidcClient(config?: Auth0Environment): OidcClient {
  let environment: Auth0Environment;
  let sdk: Auth0;
  try {
    if (Platform.OS !== 'android' && Platform.OS !== 'ios') {
      throw new AuthError('configuration');
    }
    environment = config
      ? parseAuth0Environment(config)
      : getAuth0Environment();
    sdk = new Auth0({
      domain: environment.domain,
      clientId: environment.clientId,
      useDPoP: false,
    });
  } catch {
    throw new AuthError('configuration');
  }

  return {
    async authorize() {
      const credentials = await sdk.webAuth
        .authorize({
          audience: environment.audience,
          scope: 'openid profile email offline_access',
        })
        .catch((error: unknown) => {
          throw webAuthError(error);
        });
      try {
        // Class-based WebAuth does not save credentials automatically.
        await sdk.credentialsManager.saveCredentials(credentials);
      } catch {
        throw new AuthError('storage');
      }
    },
    async getCredentials(options: GetCredentialsOptions = {}) {
      try {
        const credentials = options.forceRefresh
          ? await sdk.credentialsManager.getCredentials(
              undefined,
              undefined,
              undefined,
              true,
            )
          : await sdk.credentialsManager.getCredentials();
        // Keep refresh/ID credentials inside the SDK boundary.
        return {
          accessToken: credentials.accessToken,
          expiresAt: credentials.expiresAt,
        };
      } catch (error) {
        throw credentialError(error);
      }
    },
    async revokeRefreshToken() {
      let credentials;

      try {
        credentials = await sdk.credentialsManager.getCredentials();
      } catch (error) {
        const failure = credentialError(error);

        // Nothing remains to revoke when the local/provider session is already
        // absent or definitively invalid.
        if (
          failure.kind === 'no-session' ||
          failure.kind === 'invalid-session'
        ) {
          return;
        }

        throw failure;
      }

      const refreshToken = credentials.refreshToken;

      // A session without a refresh token has no refresh grant to revoke.
      if (!refreshToken) {
        return;
      }

      try {
        // Keep the refresh token entirely inside the Auth0 SDK adapter.
        await sdk.auth.revoke({ refreshToken });
      } catch {
        throw new AuthError('provider');
      }
    },
    async clearCredentials() {
      try {
        // The tracked iOS patch accepts only deletion / errSecItemNotFound and
        // rejects real storage errors. Unexpected false remains fail-safe.
        const result: unknown = await sdk.credentialsManager.clearCredentials();
        if (result === false) throw new AuthError('storage');
      } catch {
        throw new AuthError('storage');
      }
    },
    async clearSession() {
      try {
        // Browser session only. Local credentials and revocation are separate.
        await sdk.webAuth.clearSession();
      } catch (error) {
        throw webAuthError(error);
      }
    },
  };
}
