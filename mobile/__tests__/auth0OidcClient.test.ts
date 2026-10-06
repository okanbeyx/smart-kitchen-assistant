import { Platform } from 'react-native';
import Auth0, {
  CredentialsManagerError,
  WebAuthError,
} from 'react-native-auth0';

import { createAuth0OidcClient } from '@/core/auth/auth0OidcClient';
import { AuthError } from '@/core/auth/authError';
import { SessionManager } from '@/core/auth/sessionManager';

jest.mock('react-native-auth0', () => ({
  __esModule: true,
  default: jest.fn(),
  CredentialsManagerError: class extends Error {
    type = 'UNKNOWN_ERROR';
    code = 'unknown_error';
  },
  WebAuthError: class extends Error {
    type = 'USER_CANCELLED';
  },
  WebAuthErrorCodes: { USER_CANCELLED: 'USER_CANCELLED' },
}));
jest.mock('expo-secure-store', () => {
  throw new Error('The Auth0 adapter must not load SecureStore.');
});

const config = {
  domain: 'tenant.eu.auth0.com',
  clientId: 'PUBLIC_NATIVE_ID',
  audience: 'urn:example:api',
};
// Deliberately nonfunctional fixtures, never real credentials.
const credentials = {
  accessToken: 'fixture-access',
  idToken: 'fixture-id',
  refreshToken: 'fixture-refresh',
  tokenType: 'Bearer',
  expiresAt: 2000000000,
};
const sdk = {
  webAuth: { authorize: jest.fn(), clearSession: jest.fn() },
  credentialsManager: {
    saveCredentials: jest.fn(),
    getCredentials: jest.fn(),
    clearCredentials: jest.fn(),
  },
};

beforeEach(() => {
  jest.resetAllMocks();
  jest.mocked(Auth0).mockImplementation(() => sdk as unknown as Auth0);
  sdk.webAuth.authorize.mockResolvedValue(credentials);
  sdk.credentialsManager.saveCredentials.mockResolvedValue(undefined);
  sdk.credentialsManager.getCredentials.mockResolvedValue(credentials);
});

it('creates a native Bearer client without a client secret', () => {
  createAuth0OidcClient(config);
  expect(Auth0).toHaveBeenCalledWith({
    domain: config.domain,
    clientId: config.clientId,
    useDPoP: false,
  });
});

it('saves successful class-based authorization before resolving without returning credentials', async () => {
  let finishSave!: () => void;
  sdk.credentialsManager.saveCredentials.mockReturnValue(
    new Promise<void>((resolve) => {
      finishSave = resolve;
    }),
  );
  const client = createAuth0OidcClient(config);
  let completed = false;
  const result = client.authorize().then((value) => {
    completed = true;
    return value;
  });
  await Promise.resolve();
  await Promise.resolve();
  expect(sdk.webAuth.authorize).toHaveBeenCalledWith({
    audience: config.audience,
    scope: 'openid profile email offline_access',
  });
  expect(sdk.credentialsManager.saveCredentials).toHaveBeenCalledWith(
    credentials,
  );
  expect(sdk.credentialsManager.saveCredentials).toHaveBeenCalledTimes(1);
  expect(completed).toBe(false);
  finishSave();
  await expect(result).resolves.toBeUndefined();
});

it('maps SDK cancellation without saving credentials', async () => {
  sdk.webAuth.authorize.mockRejectedValue(new WebAuthError({} as never));
  await expect(createAuth0OidcClient(config).authorize()).rejects.toMatchObject(
    { kind: 'cancelled' },
  );
  expect(sdk.credentialsManager.saveCredentials).not.toHaveBeenCalled();
});

it('drops raw provider errors and payloads', async () => {
  sdk.webAuth.authorize.mockRejectedValue({
    message: 'private fixture payload',
    credentials,
  });
  const error = await createAuth0OidcClient(config)
    .authorize()
    .catch((value: unknown) => value);
  expect(error).toEqual(new AuthError('provider'));
  expect(JSON.stringify(error)).not.toMatch(/fixture|credentials|cause/);
  expect(sdk.credentialsManager.saveCredentials).not.toHaveBeenCalled();
});

it('does not report success if secure persistence fails', async () => {
  sdk.credentialsManager.saveCredentials.mockRejectedValue(credentials);
  await expect(createAuth0OidcClient(config).authorize()).rejects.toEqual(
    new AuthError('storage'),
  );
});

it('delegates retrieval and renewal to CredentialsManager, returning only API credentials', async () => {
  await expect(createAuth0OidcClient(config).getCredentials()).resolves.toEqual(
    { accessToken: credentials.accessToken, expiresAt: credentials.expiresAt },
  );
  expect(sdk.credentialsManager.getCredentials).toHaveBeenCalledWith();
  expect(sdk.credentialsManager.saveCredentials).not.toHaveBeenCalled();
});

it('sanitizes credential retrieval failures without deciding session validity', async () => {
  sdk.credentialsManager.getCredentials.mockRejectedValue(credentials);
  await expect(createAuth0OidcClient(config).getCredentials()).rejects.toEqual(
    new AuthError('internal'),
  );
  expect(sdk.credentialsManager.clearCredentials).not.toHaveBeenCalled();
});

it('keeps browser logout and local credential deletion independent', async () => {
  const client = createAuth0OidcClient(config);
  await client.clearSession();
  expect(sdk.webAuth.clearSession).toHaveBeenCalledWith();
  expect(sdk.credentialsManager.clearCredentials).not.toHaveBeenCalled();
  await client.clearCredentials();
  expect(sdk.credentialsManager.clearCredentials).toHaveBeenCalledTimes(1);
  expect(sdk.webAuth.clearSession).toHaveBeenCalledTimes(1);
});

it('sanitizes credential deletion failures', async () => {
  sdk.credentialsManager.clearCredentials.mockRejectedValue(credentials);
  await expect(
    createAuth0OidcClient(config).clearCredentials(),
  ).rejects.toEqual(new AuthError('storage'));
});

it('maps browser logout cancellation', async () => {
  sdk.webAuth.clearSession.mockRejectedValue(new WebAuthError({} as never));
  await expect(
    createAuth0OidcClient(config).clearSession(),
  ).rejects.toMatchObject({ kind: 'cancelled' });
});

it('sanitizes browser logout failure', async () => {
  sdk.webAuth.clearSession.mockRejectedValue(credentials);
  await expect(createAuth0OidcClient(config).clearSession()).rejects.toEqual(
    new AuthError('provider'),
  );
});

it('validates configuration before constructing the SDK', () => {
  expect(() => createAuth0OidcClient({ ...config, clientId: '' })).toThrow(
    new AuthError('configuration'),
  );
  expect(Auth0).not.toHaveBeenCalled();
});

it('sanitizes SDK initialization failures', () => {
  jest.mocked(Auth0).mockImplementation(() => {
    throw credentials;
  });
  expect(() => createAuth0OidcClient(config)).toThrow(
    new AuthError('configuration'),
  );
});

it('rejects web initialization to keep the credential vault native-only', () => {
  const previous = Platform.OS;
  try {
    Object.defineProperty(Platform, 'OS', { value: 'web', configurable: true });
    expect(() => createAuth0OidcClient(config)).toThrow(
      new AuthError('configuration'),
    );
    expect(Auth0).not.toHaveBeenCalled();
  } finally {
    Object.defineProperty(Platform, 'OS', {
      value: previous,
      configurable: true,
    });
  }
});

it.each([
  ['NO_CREDENTIALS', 'NO_CREDENTIALS', 'no-session'],
  ['INVALID_CREDENTIALS', 'INVALID_CREDENTIALS', 'invalid-session'],
  ['NO_REFRESH_TOKEN', 'NO_REFRESH_TOKEN', 'invalid-session'],
  ['SESSION_EXPIRED', 'SESSION_EXPIRED', 'invalid-session'],
  ['RENEW_FAILED', 'invalid_grant', 'invalid-session'],
  ['NO_NETWORK', 'NO_NETWORK', 'network'],
  ['UNKNOWN_ERROR', 'request_error', 'network'],
  ['RENEW_FAILED', 'RENEW_FAILED', 'provider'],
  ['API_ERROR', 'server_error', 'provider'],
  ['STORE_FAILED', 'STORE_FAILED', 'storage'],
])(
  'maps credential type %s / code %s to %s without copying payloads',
  async (type, code, kind) => {
    const error = Object.assign(new CredentialsManagerError({} as never), {
      type,
      code,
      message: 'private fixture payload',
    });
    sdk.credentialsManager.getCredentials.mockRejectedValue(error);
    await expect(
      createAuth0OidcClient(config).getCredentials(),
    ).rejects.toMatchObject({ kind });
  },
);
it('fails safe on an unexpected false result without guessing from credential retrieval', async () => {
  sdk.credentialsManager.clearCredentials.mockResolvedValue(false);
  await expect(
    createAuth0OidcClient(config).clearCredentials(),
  ).rejects.toEqual(new AuthError('storage'));
  expect(sdk.credentialsManager.getCredentials).not.toHaveBeenCalled();
});

// Models the patched native SDK contract. Actual OSStatus classification still
// requires native validation; this fake does not execute Swift or Keychain APIs.
function useNativeVault(initial: typeof credentials | null = null) {
  const vault = { stored: initial, deletionError: null as Error | null };
  sdk.credentialsManager.saveCredentials.mockImplementation(async (value) => {
    vault.stored = value;
  });
  sdk.credentialsManager.getCredentials.mockImplementation(async () => {
    if (!vault.stored) {
      throw Object.assign(new CredentialsManagerError({} as never), {
        type: 'NO_CREDENTIALS',
        code: 'NO_CREDENTIALS',
      });
    }
    return vault.stored;
  });
  sdk.credentialsManager.clearCredentials.mockImplementation(async () => {
    if (vault.deletionError) throw vault.deletionError;
    vault.stored = null;
    return true; // Includes native errSecItemNotFound after the patch.
  });
  return vault;
}

it.each([null, credentials])(
  'clears a patched native vault idempotently (initial: %p)',
  async (initial) => {
    const vault = useNativeVault(initial);
    const client = createAuth0OidcClient(config);
    await expect(client.clearCredentials()).resolves.toBeUndefined();
    await expect(client.clearCredentials()).resolves.toBeUndefined();
    expect(vault.stored).toBeNull();
    expect(sdk.credentialsManager.clearCredentials).toHaveBeenCalledTimes(2);
    expect(sdk.credentialsManager.getCredentials).not.toHaveBeenCalled();
  },
);

it.each([
  'interactionNotAllowed',
  'missingEntitlement',
  'authFailed',
  'unknown',
])(
  'keeps native deletion failure %s observable and sanitized',
  async (kind) => {
    const vault = useNativeVault(credentials);
    vault.deletionError = new Error(`private fixture ${kind}`);
    const error = await createAuth0OidcClient(config)
      .clearCredentials()
      .catch((value: unknown) => value);
    expect(error).toEqual(new AuthError('storage'));
    expect(JSON.stringify(error)).not.toMatch(/private|fixture|cause/);
    expect(vault.stored).toEqual(credentials);
  },
);

it('allows first-launch restore, login, repeated logout and another login through the real adapter', async () => {
  const vault = useNativeVault();
  const manager = new SessionManager(
    () => createAuth0OidcClient(config),
    async () => {},
  );
  await manager.start();
  expect(manager.getSnapshot()).toMatchObject({
    status: 'unauthenticated',
    cleanupRequired: false,
    error: null,
  });
  await manager.login();
  expect(manager.getSnapshot().status).toBe('authenticated');
  expect(vault.stored).toEqual(credentials);
  await manager.logout();
  await manager.logout();
  expect(vault.stored).toBeNull();
  expect(manager.getSnapshot()).toMatchObject({
    status: 'unauthenticated',
    cleanupRequired: false,
    error: null,
  });
  await manager.login();
  expect(manager.getSnapshot().status).toBe('authenticated');
  expect(sdk.webAuth.authorize).toHaveBeenCalledTimes(2);
  expect(sdk.webAuth.clearSession).not.toHaveBeenCalled();
});

it('blocks login after genuine native deletion failure and recovers only after successful cleanup', async () => {
  const vault = useNativeVault(credentials);
  const manager = new SessionManager(
    () => createAuth0OidcClient(config),
    async () => {},
  );
  await manager.start();
  vault.deletionError = new Error('private fixture Keychain error');
  await manager.logout();
  expect(manager.getSnapshot()).toMatchObject({
    status: 'unauthenticated',
    cleanupRequired: true,
    error: 'storage',
  });
  await manager.login();
  expect(sdk.webAuth.authorize).not.toHaveBeenCalled();
  expect(vault.stored).toEqual(credentials);
  vault.deletionError = null;
  await manager.logout();
  expect(vault.stored).toBeNull();
  expect(manager.getSnapshot()).toMatchObject({
    cleanupRequired: false,
    error: null,
  });
  await manager.login();
  expect(manager.getSnapshot().status).toBe('authenticated');
});

it('delegates force refresh to CredentialsManager without custom token handling', async () => {
  const client = createAuth0OidcClient(config);

  await expect(client.getCredentials({ forceRefresh: true })).resolves.toEqual({
    accessToken: credentials.accessToken,
    expiresAt: credentials.expiresAt,
  });

  expect(sdk.credentialsManager.getCredentials).toHaveBeenCalledWith(
    undefined,
    undefined,
    undefined,
    true,
  );
  expect(sdk.credentialsManager.saveCredentials).not.toHaveBeenCalled();
});
