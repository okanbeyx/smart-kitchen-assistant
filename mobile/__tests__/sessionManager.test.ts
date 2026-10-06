import { AuthError } from '@/core/auth/authError';
import type { OidcClient } from '@/core/auth/auth0OidcClient';
import { SessionManager } from '@/core/auth/sessionManager';

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (error: unknown) => void;
  const promise = new Promise<T>((yes, no) => {
    resolve = yes;
    reject = no;
  });
  return { promise, resolve, reject };
}
async function drain() {
  for (let i = 0; i < 20; i++) await Promise.resolve();
}
const usable = () => ({
  accessToken: 'fixture-access',
  expiresAt: Date.now() / 1000 + 120,
});
let provider: jest.Mocked<OidcClient>;
let cleanup: jest.Mock;
let manager: SessionManager;
beforeEach(() => {
  provider = {
    authorize: jest.fn().mockResolvedValue(undefined),
    getCredentials: jest.fn().mockImplementation(async () => usable()),
    clearCredentials: jest.fn().mockResolvedValue(undefined),
    clearSession: jest.fn(),
  };
  cleanup = jest.fn().mockResolvedValue(undefined);
  manager = new SessionManager(() => provider, cleanup);
});

it('starts bootstrapping without fetching at construction', () => {
  expect(manager.getSnapshot()).toMatchObject({
    status: 'bootstrapping',
    pending: 'restore',
  });
  expect(provider.getCredentials).not.toHaveBeenCalled();
});
it('restores only after usable credentials resolve and starts once', async () => {
  const work = deferred<ReturnType<typeof usable>>();
  provider.getCredentials.mockReturnValue(work.promise);
  const first = manager.start();
  expect(manager.start()).toBe(first);
  await drain();
  expect(manager.getSnapshot().status).toBe('bootstrapping');
  work.resolve(usable());
  await first;
  expect(manager.getSnapshot().status).toBe('authenticated');
  expect(provider.getCredentials).toHaveBeenCalledTimes(1);
  expect(JSON.stringify(manager.getSnapshot())).not.toContain('fixture-access');
});
it.each(['no-session', 'invalid-session'] as const)(
  'handles %s restore centrally',
  async (kind) => {
    provider.getCredentials.mockRejectedValue(new AuthError(kind));
    await manager.start();
    expect(manager.getSnapshot()).toMatchObject({
      status: 'unauthenticated',
      pending: null,
      cleanupRequired: false,
      error: kind === 'no-session' ? null : kind,
    });
    expect(cleanup).toHaveBeenCalledTimes(1);
  },
);
it.each(['network', 'provider', 'internal'] as const)(
  'preserves the vault after %s restore failure',
  async (kind) => {
    provider.getCredentials.mockRejectedValue(new AuthError(kind));
    await manager.start();
    expect(manager.getSnapshot()).toMatchObject({
      status: 'unauthenticated',
      pending: null,
      error: kind,
    });
    expect(provider.clearCredentials).not.toHaveBeenCalled();
    provider.getCredentials.mockResolvedValue(usable());
    await manager.retryRestore();
    expect(manager.getSnapshot().status).toBe('authenticated');
  },
);
it('sanitizes unexpected errors and exits bootstrapping', async () => {
  provider.getCredentials.mockRejectedValue(
    new Error('private fixture payload'),
  );
  await manager.start();
  expect(manager.getSnapshot()).toMatchObject({
    status: 'unauthenticated',
    error: 'internal',
  });
  expect(JSON.stringify(manager.getSnapshot())).not.toContain('private');
  expect(provider.clearCredentials).not.toHaveBeenCalled();
});
it('bounds restore waiting and ignores late completion', async () => {
  jest.useFakeTimers();
  try {
    const work = deferred<ReturnType<typeof usable>>();
    provider.getCredentials.mockReturnValue(work.promise);
    const start = manager.start();
    await drain();
    await jest.advanceTimersByTimeAsync(15_000);
    await start;
    expect(manager.getSnapshot()).toMatchObject({
      status: 'unauthenticated',
      error: 'network',
    });
    work.resolve(usable());
    await drain();
    expect(manager.getSnapshot().status).toBe('unauthenticated');
    expect(provider.clearCredentials).not.toHaveBeenCalled();
  } finally {
    jest.useRealTimers();
  }
});
it('rejects an expired credential as unusable', async () => {
  provider.getCredentials.mockResolvedValue({
    accessToken: 'fixture-expired',
    expiresAt: 1,
  });
  await manager.start();
  expect(manager.getSnapshot().status).toBe('unauthenticated');
});
it('coordinates duplicate login and waits for provider save', async () => {
  const work = deferred<void>();
  provider.authorize.mockReturnValue(work.promise);
  const first = manager.login();
  const generation = manager.getGeneration();
  expect(manager.login()).toBe(first);
  await drain();
  expect(manager.getSnapshot()).toMatchObject({
    status: 'unauthenticated',
    pending: 'login',
  });
  work.resolve();
  await first;
  expect(manager.getSnapshot().status).toBe('authenticated');
  expect(manager.getGeneration()).toBe(generation);
  expect(provider.authorize).toHaveBeenCalledTimes(1);
  expect(cleanup).toHaveBeenCalledTimes(1);
});
it.each(['cancelled', 'provider', 'storage'] as const)(
  'handles login %s without authenticating',
  async (kind) => {
    provider.authorize.mockRejectedValue(new AuthError(kind));
    await manager.login();
    expect(manager.getSnapshot()).toMatchObject({
      status: 'unauthenticated',
      pending: null,
      error: kind === 'cancelled' ? null : kind,
    });
  },
);
it('immediately invalidates the session and completes local logout', async () => {
  await manager.start();
  const before = manager.getGeneration();
  const logout = manager.logout();
  expect(manager.getGeneration()).toBeGreaterThan(before);
  expect(manager.getSnapshot()).toMatchObject({
    status: 'unauthenticated',
    pending: 'logout',
  });
  await expect(manager.getCredentials()).rejects.toMatchObject({
    kind: 'no-session',
  });
  await logout;
  expect(provider.clearCredentials).toHaveBeenCalledTimes(1);
  expect(cleanup).toHaveBeenCalledTimes(1);
  expect(provider.clearSession).not.toHaveBeenCalled();
});
it('reports failed cleanup, blocks restore/login, and permits retrying logout', async () => {
  const vault = {
    credentials: usable() as ReturnType<typeof usable> | null,
    deletionFails: true,
  };
  provider.clearCredentials.mockImplementation(async () => {
    if (vault.deletionFails) throw new AuthError('storage');
    vault.credentials = null;
  });
  provider.authorize.mockImplementation(async () => {
    vault.credentials = usable();
  });
  await manager.start();
  await manager.logout();
  expect(manager.getSnapshot()).toMatchObject({
    status: 'unauthenticated',
    error: 'storage',
    cleanupRequired: true,
  });
  await manager.login();
  await manager.retryRestore();
  expect(provider.authorize).not.toHaveBeenCalled();
  expect(provider.getCredentials).toHaveBeenCalledTimes(1);
  expect(vault.credentials).not.toBeNull();
  vault.deletionFails = false;
  await manager.logout();
  expect(manager.getSnapshot().cleanupRequired).toBe(false);
  expect(vault.credentials).toBeNull();
  await manager.login();
  expect(manager.getSnapshot().status).toBe('authenticated');
  expect(vault.credentials).not.toBeNull();
});

it('keeps empty-vault startup and repeated local logout compatible with a later login', async () => {
  const vault = { credentials: null as ReturnType<typeof usable> | null };
  provider.getCredentials.mockImplementation(async () => {
    if (!vault.credentials) throw new AuthError('no-session');
    return vault.credentials;
  });
  provider.clearCredentials.mockImplementation(async () => {
    vault.credentials = null;
  });
  provider.authorize.mockImplementation(async () => {
    vault.credentials = usable();
  });
  await manager.start();
  expect(manager.getSnapshot()).toMatchObject({
    status: 'unauthenticated',
    cleanupRequired: false,
    error: null,
  });
  await manager.login();
  expect(manager.getSnapshot().status).toBe('authenticated');
  await manager.logout();
  await manager.logout();
  expect(vault.credentials).toBeNull();
  expect(manager.getSnapshot()).toMatchObject({
    status: 'unauthenticated',
    cleanupRequired: false,
    error: null,
  });
  await manager.login();
  expect(manager.getSnapshot().status).toBe('authenticated');
  await expect(manager.getCredentials()).resolves.toEqual(vault.credentials);
  expect(provider.authorize).toHaveBeenCalledTimes(2);
});
it('shares in-flight credentials but does not cache resolved tokens', async () => {
  await manager.start();
  provider.getCredentials.mockClear();
  const work = deferred<ReturnType<typeof usable>>();
  provider.getCredentials.mockReturnValueOnce(work.promise);
  const a = manager.getCredentials();
  const b = manager.getCredentials();
  await drain();
  expect(provider.getCredentials).toHaveBeenCalledTimes(1);
  work.resolve(usable());
  await Promise.all([a, b]);
  await manager.getCredentials();
  expect(provider.getCredentials).toHaveBeenCalledTimes(2);
});
it('cancelling one waiter leaves the shared native request available to another', async () => {
  await manager.start();
  provider.getCredentials.mockClear();
  const work = deferred<ReturnType<typeof usable>>();
  provider.getCredentials.mockReturnValue(work.promise);
  const cancel = new AbortController();
  const a = manager.getCredentials(cancel.signal);
  const rejected = expect(a).rejects.toMatchObject({ kind: 'cancelled' });
  const b = manager.getCredentials();
  cancel.abort();
  await rejected;
  work.resolve(usable());
  await expect(b).resolves.toMatchObject({ accessToken: 'fixture-access' });
  expect(provider.getCredentials).toHaveBeenCalledTimes(1);
  expect(manager.getSnapshot().status).toBe('authenticated');
});
it('cleans the vault after an in-flight restore settles on logout', async () => {
  const work = deferred<ReturnType<typeof usable>>();
  const entered = deferred<void>();
  const vault = { credentials: usable() as ReturnType<typeof usable> | null };
  const operations: string[] = [];
  const statuses: string[] = [];
  manager.subscribe(() => statuses.push(manager.getSnapshot().status));
  provider.getCredentials.mockImplementation(async () => {
    operations.push('restore:start');
    entered.resolve();
    const credentials = await work.promise;
    vault.credentials = credentials;
    operations.push('restore:save');
    return credentials;
  });
  provider.clearCredentials.mockImplementation(async () => {
    vault.credentials = null;
    operations.push('clear');
  });
  const start = manager.start();
  await entered.promise;
  const logout = manager.logout();
  let loggedOut = false;
  void logout.then(() => {
    loggedOut = true;
  });
  await drain();
  expect(loggedOut).toBe(false);
  expect(provider.clearCredentials).not.toHaveBeenCalled();
  expect(operations).toEqual(['restore:start']);
  work.resolve(usable());
  await Promise.all([start, logout]);
  expect(provider.clearCredentials).toHaveBeenCalledTimes(1);
  expect(operations).toEqual(['restore:start', 'restore:save', 'clear']);
  expect(vault.credentials).toBeNull();
  expect(statuses).not.toContain('authenticated');
  expect(manager.getSnapshot().status).toBe('unauthenticated');
});
it('rejects credential results from before logout', async () => {
  await manager.start();
  const work = deferred<ReturnType<typeof usable>>();
  provider.getCredentials.mockReturnValue(work.promise);
  const credentials = manager.getCredentials();
  const rejected = expect(credentials).rejects.toMatchObject({ kind: 'stale' });
  await drain();
  const logout = manager.logout();
  work.resolve(usable());
  await Promise.all([rejected, logout]);
  expect(manager.getSnapshot().status).toBe('unauthenticated');
});
it('starts a new generation for login and ignores an old restore failure', async () => {
  const work = deferred<ReturnType<typeof usable>>();
  provider.getCredentials.mockReturnValue(work.promise);
  const start = manager.start();
  await drain();
  const generation = manager.getGeneration();
  const login = manager.login();
  expect(manager.getGeneration()).toBeGreaterThan(generation);
  work.reject(new AuthError('invalid-session'));
  await Promise.all([start, login]);
  expect(manager.getSnapshot()).toMatchObject({
    status: 'authenticated',
    error: null,
  });
  expect(provider.clearCredentials).toHaveBeenCalledTimes(1);
});
it('logout waits for authorize save, blocks new login, then allows a clean new account', async () => {
  const work = deferred<void>();
  const entered = deferred<void>();
  const vault = { credentials: null as ReturnType<typeof usable> | null };
  const operations: string[] = [];
  const statuses: string[] = [];
  manager.subscribe(() => statuses.push(manager.getSnapshot().status));
  provider.clearCredentials.mockImplementation(async () => {
    vault.credentials = null;
    operations.push('clear');
  });
  provider.authorize.mockImplementationOnce(async () => {
    operations.push('authorize:start');
    entered.resolve();
    await work.promise;
    vault.credentials = usable();
    operations.push('authorize:save');
  });
  const firstLogin = manager.login();
  await entered.promise;
  const logout = manager.logout();
  let loggedOut = false;
  void logout.then(() => {
    loggedOut = true;
  });
  await manager.login();
  await drain();
  expect(loggedOut).toBe(false);
  expect(provider.clearCredentials).toHaveBeenCalledTimes(1);
  expect(operations).toEqual(['clear', 'authorize:start']);
  expect(provider.authorize).toHaveBeenCalledTimes(1);
  work.resolve();
  await Promise.all([firstLogin, logout]);
  expect(operations).toEqual([
    'clear',
    'authorize:start',
    'authorize:save',
    'clear',
  ]);
  expect(vault.credentials).toBeNull();
  expect(statuses).not.toContain('authenticated');
  expect(manager.getSnapshot().status).toBe('unauthenticated');
  provider.authorize.mockImplementationOnce(async () => {
    vault.credentials = { ...usable(), accessToken: 'fixture-new-account' };
  });
  await manager.login();
  expect(manager.getSnapshot().status).toBe('authenticated');
  expect(vault.credentials?.accessToken).toBe('fixture-new-account');
  expect(provider.authorize).toHaveBeenCalledTimes(2);
});
it('removes a failed credential request so a later attempt can retry', async () => {
  await manager.start();
  provider.getCredentials.mockRejectedValueOnce(new AuthError('network'));
  await expect(manager.getCredentials()).rejects.toMatchObject({
    kind: 'network',
  });
  await expect(manager.getCredentials()).resolves.toMatchObject({
    accessToken: 'fixture-access',
  });
  expect(provider.clearCredentials).not.toHaveBeenCalled();
});
it('centrally signs out when active credentials are invalid', async () => {
  await manager.start();
  provider.getCredentials.mockRejectedValue(new AuthError('invalid-session'));
  await expect(manager.getCredentials()).rejects.toMatchObject({
    kind: 'invalid-session',
  });
  expect(manager.getSnapshot().status).toBe('unauthenticated');
  expect(cleanup).toHaveBeenCalledTimes(1);
});
it('unsubscribes state listeners', async () => {
  const listener = jest.fn();
  const unsubscribe = manager.subscribe(listener);
  unsubscribe();
  await manager.start();
  expect(listener).not.toHaveBeenCalled();
});

it('never publishes authenticated during an obsolete login completion', async () => {
  const work = deferred<void>();
  provider.authorize.mockReturnValue(work.promise);
  const statuses: string[] = [];
  manager.subscribe(() => statuses.push(manager.getSnapshot().status));
  const login = manager.login();
  await drain();
  const logout = manager.logout();
  work.resolve();
  await Promise.all([login, logout]);
  expect(statuses).not.toContain('authenticated');
});
it('does not start a queued obsolete login after logout', async () => {
  const login = manager.login();
  const logout = manager.logout();
  await Promise.all([login, logout]);
  expect(provider.authorize).not.toHaveBeenCalled();
});
it('reports cache cleanup failure while still clearing the provider vault', async () => {
  await manager.start();
  cleanup.mockRejectedValue(new Error('private fixture payload'));
  await manager.logout();
  expect(provider.clearCredentials).toHaveBeenCalledTimes(1);
  expect(manager.getSnapshot()).toMatchObject({
    status: 'unauthenticated',
    cleanupRequired: true,
  });
});
it('does not construct a provider eagerly and sanitizes configuration failure', async () => {
  const factory = jest.fn(() => {
    throw new AuthError('configuration');
  });
  const invalid = new SessionManager(factory, cleanup);
  expect(factory).not.toHaveBeenCalled();
  await invalid.start();
  expect(invalid.getSnapshot()).toMatchObject({
    status: 'unauthenticated',
    error: 'configuration',
  });
});
it('does not enqueue a native credential request for an already cancelled consumer', async () => {
  await manager.start();
  provider.getCredentials.mockClear();
  const cancel = new AbortController();
  cancel.abort();
  await expect(manager.getCredentials(cancel.signal)).rejects.toMatchObject({
    kind: 'cancelled',
  });
  expect(provider.getCredentials).not.toHaveBeenCalled();
});

it('a delayed startup cannot restore after explicit logout', async () => {
  const logout = manager.logout();
  await manager.start();
  await logout;
  expect(provider.getCredentials).not.toHaveBeenCalled();
  expect(manager.getSnapshot().status).toBe('unauthenticated');
});

it('returns request credentials with the generation that produced them', async () => {
  await manager.start();
  const generation = manager.getGeneration();

  await expect(manager.getRequestCredentials()).resolves.toEqual({
    ...usable(),
    generation,
  });
});

it('shares one force refresh across concurrent rejected-token recovery', async () => {
  await manager.start();
  provider.getCredentials.mockClear();

  const refreshed = deferred<ReturnType<typeof usable>>();

  provider.getCredentials.mockImplementation(async (options) => {
    if (options?.forceRefresh) {
      return refreshed.promise;
    }

    return usable();
  });

  const generation = manager.getGeneration();
  const first = manager.recoverRequestCredentials(generation, 'fixture-access');
  const second = manager.recoverRequestCredentials(
    generation,
    'fixture-access',
  );

  await drain();

  const forceRefreshCalls = provider.getCredentials.mock.calls.filter(
    ([options]) => options?.forceRefresh === true,
  );
  expect(forceRefreshCalls).toHaveLength(1);

  refreshed.resolve({
    ...usable(),
    accessToken: 'fixture-refreshed',
  });

  await expect(Promise.all([first, second])).resolves.toEqual([
    {
      ...usable(),
      accessToken: 'fixture-refreshed',
      generation,
    },
    {
      ...usable(),
      accessToken: 'fixture-refreshed',
      generation,
    },
  ]);
});

it('does not force refresh again for a late 401 from an already replaced token', async () => {
  let current = usable();
  let forceRefreshCount = 0;

  provider.getCredentials.mockImplementation(async (options) => {
    if (options?.forceRefresh) {
      forceRefreshCount += 1;
      current = {
        ...usable(),
        accessToken: `fixture-refreshed-${forceRefreshCount}`,
      };
    }

    return current;
  });

  await manager.start();
  const generation = manager.getGeneration();

  const first = await manager.recoverRequestCredentials(
    generation,
    'fixture-access',
  );
  expect(first.accessToken).toBe('fixture-refreshed-1');

  const late = await manager.recoverRequestCredentials(
    generation,
    'fixture-access',
  );

  expect(late.accessToken).toBe('fixture-refreshed-1');
  expect(forceRefreshCount).toBe(1);
});

it('rejects recovery from an old generation without refreshing the new session', async () => {
  await manager.start();
  const oldGeneration = manager.getGeneration();

  await manager.logout();
  provider.getCredentials.mockClear();

  await expect(
    manager.recoverRequestCredentials(oldGeneration, 'fixture-access'),
  ).rejects.toMatchObject({ kind: 'no-session' });

  expect(provider.getCredentials).not.toHaveBeenCalled();
});

it('preserves the authenticated session after a transient force-refresh failure', async () => {
  await manager.start();
  provider.getCredentials.mockClear();

  provider.getCredentials.mockImplementation(async (options) => {
    if (options?.forceRefresh) {
      throw new AuthError('network');
    }

    return usable();
  });

  const generation = manager.getGeneration();

  await expect(
    manager.recoverRequestCredentials(generation, 'fixture-access'),
  ).rejects.toMatchObject({ kind: 'network' });

  expect(manager.getSnapshot().status).toBe('authenticated');
  expect(provider.clearCredentials).not.toHaveBeenCalled();
});

it('signs out when force refresh proves the active session is invalid', async () => {
  await manager.start();
  provider.getCredentials.mockClear();

  provider.getCredentials.mockImplementation(async (options) => {
    if (options?.forceRefresh) {
      throw new AuthError('invalid-session');
    }

    return usable();
  });

  const generation = manager.getGeneration();

  await expect(
    manager.recoverRequestCredentials(generation, 'fixture-access'),
  ).rejects.toMatchObject({ kind: 'invalid-session' });

  expect(manager.getSnapshot()).toMatchObject({
    status: 'unauthenticated',
    cleanupRequired: false,
    error: 'invalid-session',
  });
  expect(provider.clearCredentials).toHaveBeenCalledTimes(1);
  expect(cleanup).toHaveBeenCalledTimes(1);
});
