import { QueryClient } from '@tanstack/react-query';

import { ApiError } from '@/core/api/apiError';

export function createAppQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry(failureCount, error) {
          if (
            error instanceof ApiError &&
            (error.kind === 'auth' ||
              (error.status !== undefined && error.status < 500))
          ) {
            return false;
          }

          return failureCount < 1;
        },
      },
      mutations: {
        retry: false,
      },
    },
  });
}

export const appQueryClient = createAppQueryClient();
