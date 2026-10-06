import type { QueryClient } from '@tanstack/react-query';

import { createApiClient, type ApiClient } from '@/core/api/apiClient';
import {
  createAuth0OidcClient,
  type OidcClient,
} from '@/core/auth/auth0OidcClient';
import { SessionManager } from '@/core/auth/sessionManager';
import { appQueryClient } from '@/core/query/queryClient';

export interface AppServices {
  sessionManager: SessionManager;
  apiClient: ApiClient;
}

export function createAppServices(
  queryClient: QueryClient,
  oidcClientFactory: () => OidcClient = createAuth0OidcClient,
): AppServices {
  const sessionManager = new SessionManager(oidcClientFactory, async () => {
    // No public-only cache namespace exists yet; clear the shared client at
    // account boundaries so one account cannot observe another account's data.
    try {
      await queryClient.cancelQueries();
    } finally {
      queryClient.clear();
    }
  });

  const apiClient = createApiClient({
    authSession: sessionManager,
  });

  return {
    sessionManager,
    apiClient,
  };
}

let singleton: AppServices | undefined;

export function getAppServices(): AppServices {
  if (!singleton) {
    singleton = createAppServices(appQueryClient);
  }

  return singleton;
}
