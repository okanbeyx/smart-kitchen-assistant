import { act, render } from '@testing-library/react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createRef, RefObject, useImperativeHandle } from 'react';
import { Text } from 'react-native';

import { AuthProvider, useAuth } from '@/core/auth/AuthContext';
import type { OidcClient } from '@/core/auth/auth0OidcClient';
import { AuthError } from '@/core/auth/authError';
import { SessionManager } from '@/core/auth/sessionManager';

type AuthValue = ReturnType<typeof useAuth>;

function Consumer({ authRef }: { authRef: RefObject<AuthValue | null> }) {
  const auth = useAuth();
  useImperativeHandle(authRef, () => auth, [auth]);

  return <Text>{auth.status}</Text>;
}

let provider: jest.Mocked<OidcClient>;
let queryClient: QueryClient;

beforeEach(() => {
  queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  provider = {
    authorize: jest.fn().mockResolvedValue(undefined),
    getCredentials: jest.fn().mockResolvedValue({
      accessToken: 'fixture-access',
      expiresAt: Date.now() / 1000 + 120,
    }),
    clearCredentials: jest.fn().mockResolvedValue(undefined),
    clearSession: jest.fn(),
  };
});

afterEach(() => queryClient.clear());

function createManager(): SessionManager {
  return new SessionManager(
    () => provider,
    async () => {
      try {
        await queryClient.cancelQueries();
      } finally {
        queryClient.clear();
      }
    },
  );
}

async function renderAuth() {
  const authRef = createRef<AuthValue>();
  const manager = createManager();

  const view = await render(
    <QueryClientProvider client={queryClient}>
      <AuthProvider manager={manager}>
        <Consumer authRef={authRef} />
      </AuthProvider>
    </QueryClientProvider>,
  );

  return {
    view,
    manager,
    getAuth: () => authRef.current!,
  };
}

it('subscribes to restore and exposes no token API or arbitrary authentication bridge', async () => {
  const { view, getAuth } = await renderAuth();

  expect(await view.findByText('authenticated')).toBeTruthy();

  expect(Object.keys(getAuth()).sort()).toEqual([
    'cleanupRequired',
    'error',
    'login',
    'logout',
    'pending',
    'retryRestore',
    'status',
  ]);

  expect(JSON.stringify(getAuth())).not.toContain('fixture-access');
});

it('cancels pending queries and clears cache through the supplied SessionManager on logout', async () => {
  const { getAuth } = await renderAuth();

  queryClient.setQueryData(['pantry'], ['private fixture']);

  let aborted = false;

  const fetch = queryClient
    .fetchQuery({
      queryKey: ['pending'],
      queryFn: ({ signal }) =>
        new Promise<void>((resolve) => {
          signal.addEventListener('abort', () => {
            aborted = true;
            resolve();
          });
        }),
    })
    .catch(() => undefined);

  await act(async () => {
    await getAuth().logout();
  });

  await fetch;

  expect(aborted).toBe(true);
  expect(queryClient.getQueryCache().getAll()).toHaveLength(0);
  expect(getAuth().status).toBe('unauthenticated');
  expect(provider.clearCredentials).toHaveBeenCalledTimes(1);
});

it('keeps logout storage failure visible after leaving authenticated UI', async () => {
  const { getAuth } = await renderAuth();

  provider.clearCredentials.mockRejectedValue(new AuthError('storage'));

  await act(async () => {
    await getAuth().logout();
  });

  expect(getAuth()).toMatchObject({
    status: 'unauthenticated',
    error: 'storage',
    cleanupRequired: true,
  });
});

it('maps a cancelled login to an unauthenticated state without an error alert', async () => {
  provider.getCredentials.mockRejectedValue(new AuthError('no-session'));

  const { getAuth } = await renderAuth();

  provider.authorize.mockRejectedValue(new AuthError('cancelled'));

  await act(async () => {
    await getAuth().login();
  });

  expect(getAuth()).toMatchObject({
    status: 'unauthenticated',
    error: null,
    pending: null,
  });
});
