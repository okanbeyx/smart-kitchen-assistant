import { ApiError, parseApiErrorResponse } from '@/core/api/apiError';
import { getEnvironment, parseApiBaseUrl } from '@/core/config/environment';

export type RequestAuthentication = 'public' | 'required';

export interface ApiRequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  auth?: RequestAuthentication;
  body?: unknown;
  headers?: Record<string, string>;
  signal?: AbortSignal;
  timeoutMs?: number;
}

export interface ApiRequestCredentials {
  accessToken: string;
  generation: number;
}

export interface ApiAuthSession {
  assertRequestGeneration(generation: number): void;
  getRequestCredentials(signal?: AbortSignal): Promise<ApiRequestCredentials>;
  recoverRequestCredentials(
    generation: number,
    rejectedAccessToken: string,
    signal?: AbortSignal,
  ): Promise<ApiRequestCredentials>;
}

export interface ApiClientOptions {
  baseUrl?: string;
  authSession?: ApiAuthSession;
  fetchImplementation?: typeof fetch;
  timeoutMs?: number;
}

export interface ApiClient {
  request<T>(path: string, options?: ApiRequestOptions): Promise<T>;
}

const DEFAULT_TIMEOUT_MS = 15_000;

export function createApiClient(options: ApiClientOptions = {}): ApiClient {
  const baseUrl = options.baseUrl
    ? parseApiBaseUrl(options.baseUrl)
    : getEnvironment().apiBaseUrl;
  const fetchImplementation = options.fetchImplementation ?? fetch;
  const defaultTimeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT_MS;

  return {
    async request<T>(
      path: string,
      requestOptions: ApiRequestOptions = {},
    ): Promise<T> {
      if (requestOptions.signal?.aborted) {
        throw new ApiError({ kind: 'cancelled' });
      }

      const auth = requestOptions.auth ?? 'public';
      const method = requestOptions.method ?? 'GET';

      const headers: Record<string, string> = {
        ...Object.fromEntries(
          Object.entries(requestOptions.headers ?? {}).filter(
            ([name]) => name.toLowerCase() !== 'authorization',
          ),
        ),
        Accept: 'application/json',
      };

      if (requestOptions.body !== undefined) {
        headers['Content-Type'] = 'application/json';
      }

      let requestCredentials: ApiRequestCredentials | undefined;

      if (auth === 'required') {
        if (!options.authSession) {
          throw new ApiError({ kind: 'auth' });
        }

        try {
          requestCredentials = await options.authSession.getRequestCredentials(
            requestOptions.signal,
          );
        } catch (error) {
          throw mapAuthSessionError(error);
        }

        if (!isValidRequestCredentials(requestCredentials)) {
          throw new ApiError({ kind: 'auth' });
        }

        headers.Authorization = `Bearer ${requestCredentials.accessToken}`;
      }

      if (requestOptions.signal?.aborted) {
        throw new ApiError({ kind: 'cancelled' });
      }

      const timeoutMs = requestOptions.timeoutMs ?? defaultTimeoutMs;

      if (!Number.isFinite(timeoutMs) || timeoutMs <= 0) {
        throw new RangeError('Request timeout must be a positive number.');
      }

      const controller = new AbortController();
      let abortKind: 'timeout' | 'cancelled' | undefined;

      const abortRequest = (kind: 'timeout' | 'cancelled') => {
        if (!abortKind) {
          abortKind = kind;
          controller.abort();
        }
      };

      const relayAbort = () => abortRequest('cancelled');

      requestOptions.signal?.addEventListener('abort', relayAbort, {
        once: true,
      });

      const timeoutHandle = setTimeout(() => {
        abortRequest('timeout');
      }, timeoutMs);

      const requestBody =
        requestOptions.body === undefined
          ? undefined
          : JSON.stringify(requestOptions.body);

      const fetchOnce = async (
        attemptHeaders: Record<string, string>,
      ): Promise<Response> => {
        try {
          return await fetchImplementation(buildUrl(baseUrl, path), {
            method,
            headers: attemptHeaders,
            body: requestBody,
            signal: controller.signal,
          });
        } catch (error) {
          if (isAbortError(error)) {
            throw new ApiError({ kind: 'cancelled' });
          }

          throw new ApiError({ kind: 'network' });
        }
      };

      const readBody = async (response: Response): Promise<string> => {
        try {
          return await response.text();
        } catch {
          throw new ApiError({ kind: 'parse', status: response.status });
        }
      };

      try {
        let response = await fetchOnce(headers);
        let responseBody = await readBody(response);

        if (abortKind) {
          throw new ApiError({ kind: abortKind });
        }

        const canRecover =
          auth === 'required' &&
          method === 'GET' &&
          response.status === 401 &&
          requestCredentials !== undefined &&
          options.authSession !== undefined;

        if (canRecover) {
          let recovered: ApiRequestCredentials;

          try {
            recovered = await options.authSession!.recoverRequestCredentials(
              requestCredentials!.generation,
              requestCredentials!.accessToken,
              controller.signal,
            );
          } catch (error) {
            throw mapAuthSessionError(error);
          }

          if (
            !isValidRequestCredentials(recovered) ||
            recovered.generation !== requestCredentials!.generation
          ) {
            throw new ApiError({ kind: 'auth' });
          }

          if (abortKind) {
            throw new ApiError({ kind: abortKind });
          }

          const replayHeaders = {
            ...headers,
            Authorization: `Bearer ${recovered.accessToken}`,
          };

          response = await fetchOnce(replayHeaders);
          responseBody = await readBody(response);

          if (abortKind) {
            throw new ApiError({ kind: abortKind });
          }
        }

        if (!response.ok) {
          if (auth === 'required' && response.status === 401) {
            throw new ApiError({ kind: 'auth', status: 401 });
          }

          throw parseApiErrorResponse(
            response.status,
            response.headers.get('content-type'),
            responseBody,
          );
        }

        let data = undefined as T;
        if (responseBody) {
          try {
            data = JSON.parse(responseBody) as T;
          } catch {
            throw new ApiError({ kind: 'parse', status: response.status });
          }
        }

        // Check after body reading/parsing, including empty successes and replays.
        // Keep this synchronous with delivery so old account data cannot escape.
        if (auth === 'required') {
          try {
            options.authSession!.assertRequestGeneration(
              requestCredentials!.generation,
            );
          } catch (error) {
            throw mapAuthSessionError(error);
          }
        }

        return data;
      } catch (error) {
        if (abortKind) {
          throw new ApiError({ kind: abortKind });
        }

        throw error;
      } finally {
        clearTimeout(timeoutHandle);
        requestOptions.signal?.removeEventListener('abort', relayAbort);
      }
    },
  };
}

function buildUrl(baseUrl: string, path: string): string {
  const normalizedPath = path.replace(/^\/+/, '');
  return normalizedPath ? `${baseUrl}/${normalizedPath}` : baseUrl;
}

function isAbortError(error: unknown): boolean {
  return error instanceof Error && error.name === 'AbortError';
}

function isValidRequestCredentials(
  value: ApiRequestCredentials | undefined,
): value is ApiRequestCredentials {
  return (
    value !== undefined &&
    typeof value.accessToken === 'string' &&
    value.accessToken.length > 0 &&
    Number.isInteger(value.generation) &&
    value.generation >= 0
  );
}

function mapAuthSessionError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  if (
    typeof error === 'object' &&
    error !== null &&
    'kind' in error &&
    (error as { kind?: unknown }).kind === 'cancelled'
  ) {
    return new ApiError({ kind: 'cancelled' });
  }

  // Provider/session internals are intentionally not leaked through the API layer.
  // Authentication recovery failures also must not trigger TanStack's generic
  // network retry path.
  return new ApiError({ kind: 'auth' });
}
