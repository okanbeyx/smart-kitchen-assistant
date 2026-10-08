import { createApiClient } from '@/core/api/apiClient';
import type { OidcClient } from '@/core/auth/auth0OidcClient';
import { SessionManager } from '@/core/auth/sessionManager';

function response(
  status: number,
  body: string,
  contentType = 'application/json',
): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: { get: () => contentType },
    text: async () => body,
  } as unknown as Response;
}

function createAuthSession(accessToken = 'access-token', generation = 1) {
  return {
    assertRequestGeneration: jest.fn(),
    getRequestCredentials: jest.fn().mockResolvedValue({
      accessToken,
      generation,
    }),
    recoverRequestCredentials: jest.fn().mockResolvedValue({
      accessToken,
      generation,
    }),
  };
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((yes) => {
    resolve = yes;
  });
  return { promise, resolve };
}

describe('successful response session boundaries', () => {
  async function setup() {
    const provider: jest.Mocked<OidcClient> = {
      authorize: jest.fn().mockResolvedValue(undefined),
      getCredentials: jest.fn().mockResolvedValue({
        accessToken: 'fixture-account-a',
        expiresAt: Date.now() / 1000 + 120,
      }),
      revokeRefreshToken: jest.fn(),
      clearCredentials: jest.fn().mockResolvedValue(undefined),
      clearSession: jest.fn(),
    };
    const manager = new SessionManager(
      () => provider,
      async () => {},
    );
    await manager.start();
    const guard = jest.spyOn(manager, 'assertRequestGeneration');
    return { manager, provider, guard };
  }

  const successes = [
    { method: 'GET', status: 200, body: '{"account":"fixture-account-a"}' },
    { method: 'POST', status: 201, body: '{"created":true}' },
    { method: 'PUT', status: 200, body: '{"updated":true}' },
    { method: 'PATCH', status: 200, body: '{"updated":true}' },
    { method: 'DELETE', status: 204, body: '' },
  ] as const;

  describe.each(['headers', 'body'] as const)('while awaiting %s', (stage) => {
    it.each(successes)(
      'rejects stale protected $method $status without replay or extra vault reads',
      async ({ method, status, body: payload }) => {
        const { manager, provider, guard } = await setup();
        const headers = deferred<Response>();
        const body = deferred<string>();
        const fetchStarted = deferred<void>();
        const bodyStarted = deferred<void>();
        const delayedResponse = {
          ...response(status, ''),
          text: () => {
            bodyStarted.resolve();
            return body.promise;
          },
        } as Response;
        const fetchImplementation = jest.fn<
          ReturnType<typeof fetch>,
          Parameters<typeof fetch>
        >(() => {
          fetchStarted.resolve();
          return headers.promise;
        });
        const client = createApiClient({
          baseUrl: 'https://api.example.test',
          authSession: manager,
          fetchImplementation,
        });
        const request = client.request('/api/pantry', {
          auth: 'required',
          method,
        });
        const rejected = expect(request).rejects.toMatchObject({
          kind: 'auth',
        });
        await fetchStarted.promise;
        expect(manager.getGeneration()).toBe(1);
        expect(fetchImplementation.mock.calls[0][1]?.headers).toHaveProperty(
          'Authorization',
          'Bearer fixture-account-a',
        );

        if (stage === 'body') {
          headers.resolve(delayedResponse);
          await bodyStarted.promise;
        }

        await manager.logout();
        await manager.login();
        expect(manager.getGeneration()).toBe(3);
        expect(manager.getSnapshot().status).toBe('authenticated');
        headers.resolve(delayedResponse);
        body.resolve(payload);

        await rejected;
        expect(guard).toHaveBeenCalledTimes(1);
        expect(guard).toHaveBeenCalledWith(1);
        expect(fetchImplementation).toHaveBeenCalledTimes(1);
        expect(provider.getCredentials).toHaveBeenCalledTimes(2);
        expect(provider.getCredentials).not.toHaveBeenCalledWith({
          forceRefresh: true,
        });
      },
    );
  });

  it('keeps a delayed public success independent from session changes', async () => {
    const { manager, provider, guard } = await setup();
    const credentials = jest.spyOn(manager, 'getRequestCredentials');
    const recovery = jest.spyOn(manager, 'recoverRequestCredentials');
    const body = deferred<string>();
    const bodyStarted = deferred<void>();
    const fetchImplementation = jest.fn<
      ReturnType<typeof fetch>,
      Parameters<typeof fetch>
    >(
      async () =>
        ({
          ...response(200, ''),
          text: () => {
            bodyStarted.resolve();
            return body.promise;
          },
        }) as Response,
    );
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      authSession: manager,
      fetchImplementation,
    });
    const request = client.request('/health', { auth: 'public' });
    await bodyStarted.promise;
    await manager.logout();
    await manager.login();
    body.resolve('{"healthy":true}');

    await expect(request).resolves.toEqual({ healthy: true });
    expect(manager.getGeneration()).toBe(3);
    expect(guard).not.toHaveBeenCalled();
    expect(credentials).not.toHaveBeenCalled();
    expect(recovery).not.toHaveBeenCalled();
    expect(provider.getCredentials).toHaveBeenCalledTimes(1);
    expect(fetchImplementation.mock.calls[0][1]?.headers).not.toHaveProperty(
      'Authorization',
    );
  });

  it.each(successes)(
    'delivers same-generation protected $method $status without extra vault reads',
    async ({ method, status, body }) => {
      const { manager, provider, guard } = await setup();
      const fetchImplementation = jest
        .fn()
        .mockResolvedValue(response(status, body));
      const client = createApiClient({
        baseUrl: 'https://api.example.test',
        authSession: manager,
        fetchImplementation,
      });

      await expect(
        client.request('/api/pantry', { auth: 'required', method }),
      ).resolves.toEqual(body ? JSON.parse(body) : undefined);
      expect(guard).toHaveBeenCalledTimes(1);
      expect(guard).toHaveBeenCalledWith(1);
      expect(provider.getCredentials).toHaveBeenCalledTimes(2);
      expect(fetchImplementation).toHaveBeenCalledTimes(1);
    },
  );

  it('rejects a stale successful GET replay when the session changes during body reading', async () => {
    const { manager, provider, guard } = await setup();
    let accessToken = 'fixture-account-a';
    provider.getCredentials.mockImplementation(async (options) => {
      if (options?.forceRefresh) accessToken = 'fixture-refreshed';
      return { accessToken, expiresAt: Date.now() / 1000 + 120 };
    });
    const body = deferred<string>();
    const bodyStarted = deferred<void>();
    const fetchImplementation = jest
      .fn()
      .mockResolvedValueOnce(response(401, '{}'))
      .mockResolvedValueOnce({
        ...response(200, ''),
        text: () => {
          bodyStarted.resolve();
          return body.promise;
        },
      } as Response);
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      authSession: manager,
      fetchImplementation,
    });
    const request = client.request('/api/pantry', { auth: 'required' });
    const rejected = expect(request).rejects.toMatchObject({ kind: 'auth' });
    await bodyStarted.promise;
    await manager.logout();
    await manager.login();
    body.resolve('{"account":"fixture-account-a"}');

    await rejected;
    expect(guard).toHaveBeenCalledTimes(1);
    expect(guard).toHaveBeenCalledWith(1);
    expect(fetchImplementation).toHaveBeenCalledTimes(2);
    expect(provider.getCredentials).toHaveBeenCalledTimes(4);
    expect(
      provider.getCredentials.mock.calls.filter(
        ([options]) => options?.forceRefresh,
      ),
    ).toHaveLength(1);
  });
});
describe('apiClient', () => {
  afterEach(() => {
    jest.useRealTimers();
    jest.restoreAllMocks();
  });

  it('does not send a token for public requests', async () => {
    const fetchImplementation = jest
      .fn()
      .mockResolvedValue(
        response(200, JSON.stringify({ healthy: true })),
      ) as jest.MockedFunction<typeof fetch>;
    const authSession = createAuthSession('access-token');
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetchImplementation,
      authSession,
    });

    await client.request('/health');

    const request = fetchImplementation.mock.calls[0][1];
    expect(request?.headers).not.toHaveProperty('Authorization');
    expect(authSession.getRequestCredentials).not.toHaveBeenCalled();
  });

  it('sends a bearer token for protected requests', async () => {
    const fetchImplementation = jest
      .fn()
      .mockResolvedValue(response(200, '{}')) as jest.MockedFunction<
      typeof fetch
    >;
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetchImplementation,
      authSession: createAuthSession('access-token'),
    });

    await client.request('/api/pantry', { auth: 'required' });

    expect(fetchImplementation.mock.calls[0][1]?.headers).toHaveProperty(
      'Authorization',
      'Bearer access-token',
    );
  });

  it('rejects a protected request locally when no token exists', async () => {
    const fetchImplementation = jest.fn() as jest.MockedFunction<typeof fetch>;
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetchImplementation,
      authSession: createAuthSession(''),
    });

    await expect(
      client.request('/api/pantry', { auth: 'required' }),
    ).rejects.toMatchObject({ kind: 'auth' });
    expect(fetchImplementation).not.toHaveBeenCalled();
  });

  it('relays an AbortSignal and reports cancellation safely', async () => {
    const fetchImplementation = jest.fn(
      (_input: RequestInfo | URL, init?: RequestInit) =>
        new Promise<Response>((_resolve, reject) => {
          init?.signal?.addEventListener('abort', () => {
            const abortError = new Error('aborted');
            abortError.name = 'AbortError';
            reject(abortError);
          });
        }),
    ) as jest.MockedFunction<typeof fetch>;
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetchImplementation,
    });
    const controller = new AbortController();

    const request = client.request('/health', { signal: controller.signal });
    controller.abort();

    await expect(request).rejects.toMatchObject({ kind: 'cancelled' });
    expect(fetchImplementation.mock.calls[0][1]?.signal?.aborted).toBe(true);
  });

  it('does not expose an unknown raw error response', async () => {
    const fetchImplementation = jest
      .fn()
      .mockResolvedValue(
        response(500, '<html>SQL internal detail</html>', 'text/html'),
      ) as jest.MockedFunction<typeof fetch>;
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetchImplementation,
    });

    const request = client.request('/health');

    await expect(request).rejects.toMatchObject({
      kind: 'parse',
      detail: undefined,
    });
    await expect(request).rejects.not.toThrow('SQL internal detail');
  });

  it.each(['Authorization', 'authorization', 'AUTHORIZATION', 'aUtHoRiZaTiOn'])(
    'strips the custom %s header from public requests',
    async (headerName) => {
      const fetchImplementation = jest
        .fn()
        .mockResolvedValue(response(200, '{}'));
      const authSession = createAuthSession('real-token');
      const client = createApiClient({
        baseUrl: 'https://api.example.test',
        fetchImplementation,
        authSession,
      });

      await client.request('/health', {
        auth: 'public',
        headers: { [headerName]: 'Bearer caller-token', 'X-Request': 'kept' },
      });

      const headers = fetchImplementation.mock.calls[0][1].headers;
      expect(
        Object.keys(headers).some(
          (name) => name.toLowerCase() === 'authorization',
        ),
      ).toBe(false);
      expect(headers['X-Request']).toBe('kept');
      expect(authSession.getRequestCredentials).not.toHaveBeenCalled();
    },
  );

  it('uses only the provider bearer token for protected requests', async () => {
    const fetchImplementation = jest
      .fn()
      .mockResolvedValue(response(200, '{}'));
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetchImplementation,
      authSession: createAuthSession('real-token'),
    });

    await client.request('/api/pantry', {
      auth: 'required',
      headers: {
        Authorization: 'fake',
        authorization: 'fake-lower',
        AUTHORIZATION: 'fake-upper',
      },
    });

    const headers = fetchImplementation.mock.calls[0][1].headers;
    const authHeaders = Object.entries(headers).filter(
      ([name]) => name.toLowerCase() === 'authorization',
    );
    expect(authHeaders).toEqual([['Authorization', 'Bearer real-token']]);
  });

  it('rejects an already cancelled request without token lookup or network', async () => {
    const controller = new AbortController();
    controller.abort();
    const fetchImplementation = jest.fn();
    const authSession = createAuthSession('real-token');
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetchImplementation,
      authSession,
    });

    await expect(
      client.request('/api/pantry', {
        auth: 'required',
        signal: controller.signal,
      }),
    ).rejects.toMatchObject({ kind: 'cancelled' });
    expect(authSession.getRequestCredentials).not.toHaveBeenCalled();
    expect(fetchImplementation).not.toHaveBeenCalled();
  });

  it('times out before response headers and cleans up the listener and timer', async () => {
    jest.useFakeTimers();
    const caller = new AbortController();
    const removeListener = jest.spyOn(caller.signal, 'removeEventListener');
    const fetchImplementation = jest.fn(
      (_input: RequestInfo | URL, init?: RequestInit) =>
        pendingUntilAbort<Response>(init!.signal!),
    );
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetchImplementation,
      timeoutMs: 100,
    });
    const request = client.request('/health', { signal: caller.signal });
    const assertion = expect(request).rejects.toMatchObject({
      kind: 'timeout',
    });

    await jest.advanceTimersByTimeAsync(100);

    await assertion;
    expect(removeListener).toHaveBeenCalledWith('abort', expect.any(Function));
    expect(jest.getTimerCount()).toBe(0);
  });

  it.each(['cancelled', 'timeout'] as const)(
    'keeps %s effective while the response body is pending',
    async (expectedKind) => {
      jest.useFakeTimers();
      const caller = new AbortController();
      const removeListener = jest.spyOn(caller.signal, 'removeEventListener');
      let bodyStarted!: () => void;
      const started = new Promise<void>((resolve) => {
        bodyStarted = resolve;
      });
      const fetchImplementation = jest.fn(
        async (_input: RequestInfo | URL, init?: RequestInit) =>
          ({
            ...response(200, ''),
            text: () => {
              bodyStarted();
              return pendingUntilAbort<string>(init!.signal!);
            },
          }) as Response,
      );
      const client = createApiClient({
        baseUrl: 'https://api.example.test',
        fetchImplementation,
        timeoutMs: 100,
      });
      const request = client.request('/health', { signal: caller.signal });
      const assertion = expect(request).rejects.toMatchObject({
        kind: expectedKind,
      });
      await started;

      if (expectedKind === 'cancelled') {
        caller.abort();
      } else {
        await jest.advanceTimersByTimeAsync(100);
      }

      await assertion;
      expect(fetchImplementation.mock.calls[0][1]?.signal?.aborted).toBe(true);
      expect(removeListener).toHaveBeenCalledWith(
        'abort',
        expect.any(Function),
      );
      expect(jest.getTimerCount()).toBe(0);
    },
  );

  it.each(['cancelled', 'timeout'] as const)(
    'preserves %s when a second abort cause occurs before rejection',
    async (firstCause) => {
      jest.useFakeTimers();
      const caller = new AbortController();
      let rejectFetch!: (reason: Error) => void;
      const fetchImplementation = jest.fn(
        () =>
          new Promise<Response>((_resolve, reject) => {
            rejectFetch = reject;
          }),
      );
      const client = createApiClient({
        baseUrl: 'https://api.example.test',
        fetchImplementation,
        timeoutMs: 100,
      });
      const request = client.request('/health', { signal: caller.signal });
      const assertion = expect(request).rejects.toMatchObject({
        kind: firstCause,
      });

      if (firstCause === 'cancelled') {
        caller.abort();
        await jest.advanceTimersByTimeAsync(100);
      } else {
        await jest.advanceTimersByTimeAsync(100);
        caller.abort();
      }
      rejectFetch(new Error('aborted'));
      await assertion;
      expect(jest.getTimerCount()).toBe(0);
    },
  );

  it('retains cancellation during successful body reading and cleans up afterward', async () => {
    jest.useFakeTimers();
    const caller = new AbortController();
    const removeListener = jest.spyOn(caller.signal, 'removeEventListener');
    let finishBody!: (value: string) => void;
    let bodyStarted!: () => void;
    const started = new Promise<void>((resolve) => {
      bodyStarted = resolve;
    });
    const body = new Promise<string>((resolve) => {
      finishBody = resolve;
    });
    const fetchImplementation = jest.fn(
      async () =>
        ({
          ...response(200, ''),
          text: () => {
            bodyStarted();
            return body;
          },
        }) as Response,
    );
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetchImplementation,
      timeoutMs: 100,
    });
    const request = client.request('/health', { signal: caller.signal });
    await started;

    expect(jest.getTimerCount()).toBe(1);
    expect(removeListener).not.toHaveBeenCalled();
    finishBody('{"healthy":true}');
    await expect(request).resolves.toEqual({ healthy: true });
    expect(removeListener).toHaveBeenCalledWith('abort', expect.any(Function));
    expect(jest.getTimerCount()).toBe(0);
  });

  it.each([
    [200, 'not-json', 'parse'],
    [409, '{"code":"insufficient_stock"}', 'http'],
  ])(
    'cleans up after status %s parsing/error processing',
    async (status, body, kind) => {
      jest.useFakeTimers();
      const caller = new AbortController();
      const removeListener = jest.spyOn(caller.signal, 'removeEventListener');
      const fetchImplementation = jest
        .fn()
        .mockResolvedValue(response(status as number, body as string));
      const client = createApiClient({
        baseUrl: 'https://api.example.test',
        fetchImplementation,
      });

      await expect(
        client.request('/health', { signal: caller.signal }),
      ).rejects.toMatchObject({ kind });
      expect(removeListener).toHaveBeenCalledWith(
        'abort',
        expect.any(Function),
      );
      expect(jest.getTimerCount()).toBe(0);
    },
  );
});

function pendingUntilAbort<T>(signal: AbortSignal): Promise<T> {
  return new Promise<T>((_resolve, reject) => {
    const rejectAbort = () => {
      const error = new Error('aborted');
      error.name = 'AbortError';
      reject(error);
    };
    if (signal.aborted) {
      rejectAbort();
    } else {
      signal.addEventListener('abort', rejectAbort, { once: true });
    }
  });
}

describe('session-aware 401 recovery', () => {
  function session() {
    return {
      assertRequestGeneration: jest.fn(),
      getRequestCredentials: jest.fn().mockResolvedValue({
        accessToken: 'old-token',
        generation: 7,
      }),
      recoverRequestCredentials: jest.fn().mockResolvedValue({
        accessToken: 'new-token',
        generation: 7,
      }),
    };
  }

  it('recovers a protected GET once after a 401 and replays with the new token', async () => {
    const authSession = session();
    const fetchImplementation = jest
      .fn()
      .mockResolvedValueOnce(response(401, '{"title":"Unauthorized"}'))
      .mockResolvedValueOnce(response(200, '{"value":42}'));

    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetchImplementation,
      authSession,
    });

    await expect(
      client.request<{ value: number }>('/api/pantry', {
        auth: 'required',
      }),
    ).resolves.toEqual({ value: 42 });

    expect(fetchImplementation).toHaveBeenCalledTimes(2);
    expect(fetchImplementation.mock.calls[0][1]?.headers).toHaveProperty(
      'Authorization',
      'Bearer old-token',
    );
    expect(fetchImplementation.mock.calls[1][1]?.headers).toHaveProperty(
      'Authorization',
      'Bearer new-token',
    );

    expect(authSession.recoverRequestCredentials).toHaveBeenCalledTimes(1);
    expect(
      authSession.recoverRequestCredentials.mock.calls[0].slice(0, 2),
    ).toEqual([7, 'old-token']);
  });

  it('stops after the single replay when the replay also returns 401', async () => {
    const authSession = session();
    const fetchImplementation = jest
      .fn()
      .mockResolvedValueOnce(response(401, '{}'))
      .mockResolvedValueOnce(response(401, '{}'));

    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetchImplementation,
      authSession,
    });

    await expect(
      client.request('/api/pantry', { auth: 'required' }),
    ).rejects.toMatchObject({
      kind: 'auth',
      status: 401,
    });

    expect(fetchImplementation).toHaveBeenCalledTimes(2);
    expect(authSession.recoverRequestCredentials).toHaveBeenCalledTimes(1);
  });

  it('does not refresh or replay a protected 403', async () => {
    const authSession = session();
    const fetchImplementation = jest
      .fn()
      .mockResolvedValue(response(403, '{"title":"Forbidden"}'));

    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetchImplementation,
      authSession,
    });

    await expect(
      client.request('/api/pantry', { auth: 'required' }),
    ).rejects.toMatchObject({
      kind: 'http',
      status: 403,
    });

    expect(fetchImplementation).toHaveBeenCalledTimes(1);
    expect(authSession.recoverRequestCredentials).not.toHaveBeenCalled();
  });

  it('does not automatically replay a protected mutation after 401', async () => {
    const authSession = session();
    const fetchImplementation = jest
      .fn()
      .mockResolvedValue(response(401, '{}'));

    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetchImplementation,
      authSession,
    });

    await expect(
      client.request('/api/pantry/consume', {
        method: 'POST',
        auth: 'required',
        body: { recipeId: 'fixture' },
      }),
    ).rejects.toMatchObject({
      kind: 'auth',
      status: 401,
    });

    expect(fetchImplementation).toHaveBeenCalledTimes(1);
    expect(authSession.recoverRequestCredentials).not.toHaveBeenCalled();
  });

  it('rejects a recovered credential from another session generation without replaying', async () => {
    const authSession = session();
    authSession.recoverRequestCredentials.mockResolvedValue({
      accessToken: 'new-account-token',
      generation: 8,
    });

    const fetchImplementation = jest
      .fn()
      .mockResolvedValue(response(401, '{}'));

    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetchImplementation,
      authSession,
    });

    await expect(
      client.request('/api/pantry', { auth: 'required' }),
    ).rejects.toMatchObject({ kind: 'auth' });

    expect(fetchImplementation).toHaveBeenCalledTimes(1);
  });

  it('never invokes the auth session for a public request', async () => {
    const authSession = session();
    const fetchImplementation = jest
      .fn()
      .mockResolvedValue(response(401, '{"title":"Unauthorized"}'));

    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetchImplementation,
      authSession,
    });

    await expect(client.request('/health')).rejects.toMatchObject({
      status: 401,
    });

    expect(authSession.getRequestCredentials).not.toHaveBeenCalled();
    expect(authSession.recoverRequestCredentials).not.toHaveBeenCalled();
    expect(fetchImplementation).toHaveBeenCalledTimes(1);
  });
});
