import { render } from '@testing-library/react-native';

import AppLayout from '@/app/(app)/_layout';
import AuthLayout from '@/app/(auth)/_layout';
import { useAuth } from '@/core/auth/AuthContext';
import { AuthStatus } from '@/core/auth/authSession';

jest.mock('@/core/auth/AuthContext', () => ({ useAuth: jest.fn() }));

jest.mock('expo-router', () => {
  const React = jest.requireActual<typeof import('react')>('react');
  const { Text } =
    jest.requireActual<typeof import('react-native')>('react-native');

  return {
    Redirect: ({ href }: { href: string }) =>
      React.createElement(Text, { testID: 'redirect' }, href),
    Stack: () => React.createElement(Text, { testID: 'stack' }, 'Stack'),
  };
});

function setAuthStatus(status: AuthStatus) {
  jest.mocked(useAuth).mockReturnValue({
    status,
    pending: null,
    error: null,
    cleanupRequired: false,
    login: jest.fn(),
    retryRestore: jest.fn(),
    logout: jest.fn(),
  });
}

describe('route group auth guards', () => {
  it('redirects a direct unauthenticated app route to login without a stack', async () => {
    setAuthStatus('unauthenticated');
    const view = await render(<AppLayout />);

    expect(view.getByTestId('redirect').props.children).toBe('/(auth)/login');
    expect(view.queryByTestId('stack')).toBeNull();
  });

  it('allows the authenticated app stack', async () => {
    setAuthStatus('authenticated');
    const view = await render(<AppLayout />);

    expect(view.getByTestId('stack')).toBeTruthy();
    expect(view.queryByTestId('redirect')).toBeNull();
  });

  it('does not render protected content while bootstrapping', async () => {
    setAuthStatus('bootstrapping');
    const view = await render(<AppLayout />);

    expect(view.getByText('Uygulama hazırlanıyor…')).toBeTruthy();
    expect(view.queryByTestId('stack')).toBeNull();
    expect(view.queryByTestId('redirect')).toBeNull();
  });

  it('redirects an authenticated direct login route to the app', async () => {
    setAuthStatus('authenticated');
    const view = await render(<AuthLayout />);

    expect(view.getByTestId('redirect').props.children).toBe('/(app)');
    expect(view.queryByTestId('stack')).toBeNull();
  });

  it('allows the unauthenticated login stack without redirecting back', async () => {
    setAuthStatus('unauthenticated');
    const view = await render(<AuthLayout />);

    expect(view.getByTestId('stack')).toBeTruthy();
    expect(view.queryByTestId('redirect')).toBeNull();
  });

  it('waits during bootstrap in the auth group too', async () => {
    setAuthStatus('bootstrapping');
    const view = await render(<AuthLayout />);

    expect(view.getByText('Uygulama hazırlanıyor…')).toBeTruthy();
    expect(view.queryByTestId('stack')).toBeNull();
  });

  it('redirects on logout and renders the destination without a loop', async () => {
    setAuthStatus('authenticated');
    const view = await render(<AppLayout />);
    setAuthStatus('unauthenticated');
    await view.rerender(<AppLayout />);
    expect(view.getByTestId('redirect').props.children).toBe('/(auth)/login');
    await view.rerender(<AuthLayout />);
    expect(view.getByTestId('stack')).toBeTruthy();
    expect(view.queryByTestId('redirect')).toBeNull();
  });
});
