import { render } from '@testing-library/react-native';

import { AppBootstrapNavigator } from '@/shared/components/AppBootstrapNavigator';

jest.mock('expo-router', () => {
  const React = jest.requireActual<typeof import('react')>('react');
  const { Text } =
    jest.requireActual<typeof import('react-native')>('react-native');

  return {
    Redirect: ({ href }: { href: string }) =>
      React.createElement(Text, { testID: 'redirect-target' }, href),
  };
});

describe('AppBootstrapNavigator', () => {
  it('shows loading while authentication is bootstrapping', async () => {
    const view = await render(<AppBootstrapNavigator status="bootstrapping" />);

    expect(view.getByText('Uygulama hazırlanıyor…')).toBeTruthy();
    expect(view.queryByTestId('redirect-target')).toBeNull();
  });

  it('routes an unauthenticated session to login', async () => {
    const view = await render(
      <AppBootstrapNavigator status="unauthenticated" />,
    );

    expect(view.getByTestId('redirect-target').props.children).toBe(
      '/(auth)/login',
    );
  });

  it('routes an injected authenticated state to home', async () => {
    const view = await render(<AppBootstrapNavigator status="authenticated" />);

    expect(view.getByTestId('redirect-target').props.children).toBe('/(app)');
  });

  it('transitions from bootstrap to exactly one redirect', async () => {
    const view = await render(<AppBootstrapNavigator status="bootstrapping" />);

    await view.rerender(<AppBootstrapNavigator status="unauthenticated" />);

    expect(view.getAllByTestId('redirect-target')).toHaveLength(1);
  });
});
