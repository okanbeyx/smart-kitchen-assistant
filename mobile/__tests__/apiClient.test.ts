import { createApiClient } from '@/core/api/apiClient';

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
    const getAccessToken = jest.fn(() => 'access-token');
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetchImplementation,
      getAccessToken,
    });

    await client.request('/health');

    const request = fetchImplementation.mock.calls[0][1];
    expect(request?.headers).not.toHaveProperty('Authorization');
    expect(getAccessToken).not.toHaveBeenCalled();
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
      getAccessToken: () => 'access-token',
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
      getAccessToken: () => null,
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
      const getAccessToken = jest.fn(() => 'real-token');
      const client = createApiClient({
        baseUrl: 'https://api.example.test',
        fetchImplementation,
        getAccessToken,
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
      expect(getAccessToken).not.toHaveBeenCalled();
    },
  );

  it('uses only the provider bearer token for protected requests', async () => {
    const fetchImplementation = jest
      .fn()
      .mockResolvedValue(response(200, '{}'));
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetchImplementation,
      getAccessToken: () => 'real-token',
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
    const getAccessToken = jest.fn(() => 'real-token');
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetchImplementation,
      getAccessToken,
    });

    await expect(
      client.request('/api/pantry', {
        auth: 'required',
        signal: controller.signal,
      }),
    ).rejects.toMatchObject({ kind: 'cancelled' });
    expect(getAccessToken).not.toHaveBeenCalled();
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
