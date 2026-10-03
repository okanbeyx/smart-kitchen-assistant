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

export interface ApiClientOptions {
  baseUrl?: string;
  getAccessToken?: () => string | null | Promise<string | null>;
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

      if (auth === 'required') {
        const accessToken = await options.getAccessToken?.();

        if (!accessToken) {
          throw new ApiError({ kind: 'auth' });
        }

        headers.Authorization = `Bearer ${accessToken}`;
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

      let response: Response;

      try {
        try {
          response = await fetchImplementation(buildUrl(baseUrl, path), {
            method: requestOptions.method ?? 'GET',
            headers,
            body:
              requestOptions.body === undefined
                ? undefined
                : JSON.stringify(requestOptions.body),
            signal: controller.signal,
          });
        } catch (error) {
          if (isAbortError(error)) {
            throw new ApiError({ kind: 'cancelled' });
          }

          throw new ApiError({ kind: 'network' });
        }

        let responseBody: string;

        try {
          responseBody = await response.text();
        } catch {
          throw new ApiError({ kind: 'parse', status: response.status });
        }

        if (abortKind) {
          throw new ApiError({ kind: abortKind });
        }

        if (!response.ok) {
          throw parseApiErrorResponse(
            response.status,
            response.headers.get('content-type'),
            responseBody,
          );
        }

        if (!responseBody) {
          return undefined as T;
        }

        try {
          return JSON.parse(responseBody) as T;
        } catch {
          throw new ApiError({ kind: 'parse', status: response.status });
        }
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
