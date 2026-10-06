import type { AccessCredentials, OidcClient } from './auth0OidcClient';
import { AuthError, AuthErrorKind } from './authError';
import { AuthSessionState, initialAuthSessionState } from './authSession';

export interface SessionRequestCredentials extends AccessCredentials {
  generation: number;
}
function safeError(error: unknown): AuthError {
  return new AuthError(error instanceof AuthError ? error.kind : 'internal');
}

// Cancels only this waiter. Native work remains tracked until it settles.
function waitFor<T>(
  promise: Promise<T>,
  timeoutMs: number,
  signal?: AbortSignal,
): Promise<T> {
  return new Promise((resolve, reject) => {
    const cancel = () => finish(() => reject(new AuthError('cancelled')));
    const timer = setTimeout(
      () => finish(() => reject(new AuthError('network'))),
      timeoutMs,
    );
    function finish(action: () => void) {
      clearTimeout(timer);
      signal?.removeEventListener('abort', cancel);
      action();
    }
    promise.then(
      (value) => finish(() => resolve(value)),
      (error) => finish(() => reject(error)),
    );
    if (signal?.aborted) cancel();
    else signal?.addEventListener('abort', cancel, { once: true });
  });
}

export class SessionManager {
  private state: AuthSessionState = { ...initialAuthSessionState };
  private generation = 0;
  private listeners = new Set<() => void>();
  private tail: Promise<unknown> = Promise.resolve();
  private client?: OidcClient;
  private started?: Promise<void>;
  private loginRequest?: Promise<void>;
  private logoutRequest?: Promise<void>;
  private credentialsRequest?: {
    generation: number;
    promise: Promise<AccessCredentials>;
  };
  private refreshRequest?: {
    generation: number;
    rejectedAccessToken: string;
    promise: Promise<AccessCredentials>;
  };

  constructor(
    private readonly createClient: () => OidcClient,
    private readonly clearUserCache: () => Promise<void>,
    private readonly waitTimeoutMs = 15_000,
  ) {}

  getSnapshot = (): AuthSessionState => this.state;
  getGeneration = (): number => this.generation;
  subscribe = (listener: () => void): (() => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };

  private update(patch: Partial<AuthSessionState>) {
    this.state = { ...this.state, ...patch };
    this.listeners.forEach((listener) => listener());
  }
  private provider(): OidcClient {
    return (this.client ??= this.createClient());
  }
  private assertCurrent(generation: number) {
    if (generation !== this.generation) throw new AuthError('stale');
  }
  // serialize *all* vault operations, including authorize's SDK save.
  private enqueue<T>(operation: () => Promise<T>): Promise<T> {
    const request = this.tail.then(operation);
    this.tail = request.then(
      () => undefined,
      () => undefined,
    );
    return request;
  }

  start = (): Promise<void> => {
    // A delayed mount effect must not override an explicit login/logout.
    return (this.started ??=
      this.generation === 0 ? this.restore() : Promise.resolve());
  };
  retryRestore = (): Promise<void> => {
    if (
      this.state.pending ||
      this.state.cleanupRequired ||
      this.state.status === 'authenticated'
    )
      return Promise.resolve();
    return this.restore();
  };

  private credentials(generation: number): Promise<AccessCredentials> {
    if (this.credentialsRequest?.generation === generation)
      return this.credentialsRequest.promise;
    const promise = this.enqueue(async () => {
      this.assertCurrent(generation);
      const credentials = await this.provider().getCredentials();
      this.assertCurrent(generation);
      if (
        !credentials.accessToken ||
        !Number.isFinite(credentials.expiresAt) ||
        credentials.expiresAt <= Date.now() / 1000
      ) {
        throw new AuthError('invalid-session');
      }
      return credentials;
    });
    const request = { generation, promise };
    this.credentialsRequest = request;
    const clear = () => {
      if (this.credentialsRequest === request)
        this.credentialsRequest = undefined;
    };
    void promise.then(clear, clear);
    return promise;
  }

  private refreshCredentials(
    generation: number,
    rejectedAccessToken: string,
  ): Promise<AccessCredentials> {
    if (
      this.refreshRequest?.generation === generation &&
      this.refreshRequest.rejectedAccessToken === rejectedAccessToken
    ) {
      return this.refreshRequest.promise;
    }

    const promise = this.enqueue(async () => {
      this.assertCurrent(generation);
      const credentials = await this.provider().getCredentials({
        forceRefresh: true,
      });
      this.assertCurrent(generation);

      if (
        !credentials.accessToken ||
        !Number.isFinite(credentials.expiresAt) ||
        credentials.expiresAt <= Date.now() / 1000
      ) {
        throw new AuthError('invalid-session');
      }

      return credentials;
    });

    const request = { generation, rejectedAccessToken, promise };
    this.refreshRequest = request;

    const clear = () => {
      if (this.refreshRequest === request) {
        this.refreshRequest = undefined;
      }
    };

    void promise.then(clear, clear);
    return promise;
  }
  private async restore(): Promise<void> {
    const generation = ++this.generation;
    this.update({ status: 'bootstrapping', pending: 'restore', error: null });
    try {
      await waitFor(this.credentials(generation), this.waitTimeoutMs);
      this.assertCurrent(generation);
      this.update({ status: 'authenticated', pending: null });
    } catch (error) {
      if (generation !== this.generation) return;
      const failure = safeError(error);
      if (failure.kind === 'invalid-session' || failure.kind === 'no-session') {
        await this.signOut(failure.kind === 'no-session' ? null : failure.kind);
      } else {
        // No destructive cleanup for a network, provider, or unknown failure.
        this.update({
          status: 'unauthenticated',
          pending: null,
          error: failure.kind,
        });
      }
    }
  }

  login = (): Promise<void> => {
    if (this.state.pending === 'login' && this.loginRequest)
      return this.loginRequest;
    if (
      this.state.cleanupRequired ||
      this.state.pending === 'logout' ||
      this.state.status === 'authenticated'
    )
      return Promise.resolve();
    const generation = ++this.generation;
    this.update({ status: 'unauthenticated', pending: 'login', error: null });
    const request = this.enqueue(async () => {
      this.assertCurrent(generation);
      await this.clearUserCache();
      this.assertCurrent(generation);
      // Clear any unverified previous account before starting a new login.
      await this.provider().clearCredentials();
      this.assertCurrent(generation);
      await this.provider().authorize();
      this.assertCurrent(generation);
    }).then(
      () => {
        if (generation === this.generation)
          this.update({ status: 'authenticated', pending: null });
      },
      async (error: unknown) => {
        if (generation !== this.generation) return;
        const failure = safeError(error);
        // A failed save may have partially persisted; cleanup is mandatory.
        if (failure.kind === 'configuration') {
          this.update({ pending: null, error: failure.kind });
          return;
        }
        await this.signOut(failure.kind === 'cancelled' ? null : failure.kind);
      },
    );
    this.loginRequest = request;
    void request.then(() => {
      if (this.loginRequest === request) this.loginRequest = undefined;
    });
    return request;
  };

  logout = (): Promise<void> => this.signOut(null);

  private signOut(reason: AuthErrorKind | null): Promise<void> {
    if (this.state.pending === 'logout' && this.logoutRequest)
      return this.logoutRequest;
    const generation = ++this.generation;
    this.update({
      status: 'unauthenticated',
      pending: 'logout',
      error: reason,
      cleanupRequired: true,
    });
    // Cancel queries promptly; the queue still prevents overlap with a new account.
    const cacheCleanup = Promise.resolve()
      .then(this.clearUserCache)
      .then(
        () => true,
        () => false,
      );
    const request = this.enqueue(async () => {
      const cacheCleared = await cacheCleanup;
      await this.provider().clearCredentials();
      if (!cacheCleared) throw new AuthError('internal');
    }).then(
      () => {
        if (generation === this.generation)
          this.update({ pending: null, error: reason, cleanupRequired: false });
      },
      () => {
        if (generation === this.generation)
          this.update({
            pending: null,
            error: 'storage',
            cleanupRequired: true,
          });
      },
    );
    this.logoutRequest = request;
    return request;
  }

  getRequestCredentials = async (
    signal?: AbortSignal,
  ): Promise<SessionRequestCredentials> => {
    const generation = this.generation;
    const credentials = await this.getCredentials(signal);
    this.assertCurrent(generation);

    return { ...credentials, generation };
  };

  recoverRequestCredentials = async (
    generation: number,
    rejectedAccessToken: string,
    signal?: AbortSignal,
  ): Promise<SessionRequestCredentials> => {
    if (signal?.aborted) throw new AuthError('cancelled');
    if (this.state.status !== 'authenticated')
      throw new AuthError('no-session');

    this.assertCurrent(generation);

    try {
      const current = await waitFor(
        this.credentials(generation),
        this.waitTimeoutMs,
        signal,
      );
      this.assertCurrent(generation);

      // Another request may already have refreshed this rejected token.
      if (current.accessToken !== rejectedAccessToken) {
        return { ...current, generation };
      }

      const refreshed = await waitFor(
        this.refreshCredentials(generation, rejectedAccessToken),
        this.waitTimeoutMs,
        signal,
      );
      this.assertCurrent(generation);

      return { ...refreshed, generation };
    } catch (error) {
      this.assertCurrent(generation);
      const failure = safeError(error);

      if (failure.kind === 'invalid-session' || failure.kind === 'no-session') {
        await this.signOut(failure.kind);
      }

      throw failure;
    }
  };
  // Service-layer API only. React context never exposes credentials.
  getCredentials = async (signal?: AbortSignal): Promise<AccessCredentials> => {
    if (signal?.aborted) throw new AuthError('cancelled');
    if (this.state.status !== 'authenticated')
      throw new AuthError('no-session');
    const generation = this.generation;
    try {
      const credentials = await waitFor(
        this.credentials(generation),
        this.waitTimeoutMs,
        signal,
      );
      this.assertCurrent(generation);
      return credentials;
    } catch (error) {
      this.assertCurrent(generation);
      const failure = safeError(error);
      if (failure.kind === 'invalid-session' || failure.kind === 'no-session')
        await this.signOut(failure.kind);
      throw failure;
    }
  };
}
