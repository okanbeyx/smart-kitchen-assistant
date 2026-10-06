import { fireEvent, render } from '@testing-library/react-native';
import { useAuth } from '@/core/auth/AuthContext';
import { LoginScreen } from '@/features/auth/LoginScreen';
import { HomeScreen } from '@/features/home/HomeScreen';
import { AppBootstrapScreen } from '@/shared/components/AppBootstrapScreen';

jest.mock('@/core/auth/AuthContext', () => ({ useAuth: jest.fn() }));
const login = jest.fn();
const logout = jest.fn();
beforeEach(() => {
  jest.clearAllMocks();
  jest.mocked(useAuth).mockReturnValue({
    status: 'unauthenticated',
    pending: null,
    error: null,
    cleanupRequired: false,
    login,
    logout,
    retryRestore: jest.fn(),
  });
});

jest.mock('@/shared/hooks/useReducedMotion', () => ({
  useReducedMotion: () => true,
}));
jest.mock('@react-native-vector-icons/ionicons', () => {
  const React = jest.requireActual<typeof import('react')>('react');
  const { Text } =
    jest.requireActual<typeof import('react-native')>('react-native');
  return {
    __esModule: true,
    default: (props: object) => React.createElement(Text, props),
  };
});

describe('presentation-only shells', () => {
  it('starts provider login without local credential inputs', async () => {
    const view = await render(<LoginScreen />);
    expect(
      view.getByRole('header', { name: 'Smart Kitchen Assistant' }),
    ).toBeTruthy();
    const button = view.getByRole('button', { name: 'Giriş yap / Kaydol' });
    expect(button).not.toBeDisabled();
    await fireEvent.press(button);
    expect(login).toHaveBeenCalledTimes(1);
    expect(view.queryByRole('textbox')).toBeNull();
    expect(
      view.container.queryAll((node) => node.props.editable !== undefined),
    ).toHaveLength(0);
    expect(
      view.container.queryAll(
        (node) => node.props.keyboardShouldPersistTaps === 'handled',
      ).length,
    ).toBeGreaterThan(0);
  });

  it('shows explicit Home previews with no interactive feature actions', async () => {
    const view = await render(<HomeScreen />);
    expect(view.getByText(/özellikler henüz bağlı değil/)).toBeTruthy();
    expect(view.getByRole('header', { name: 'Pantry' })).toBeTruthy();
    expect(view.getByRole('header', { name: 'Tarifler' })).toBeTruthy();
    expect(view.getByRole('header', { name: 'Akıllı Öneriler' })).toBeTruthy();
    expect(view.getAllByText('Yakında')).toHaveLength(3);
    await fireEvent.press(
      view.getByRole('button', { name: 'Bu cihazdan çıkış yap' }),
    );
    expect(logout).toHaveBeenCalledTimes(1);
    expect(
      view.container.queryAll(
        (node) => node.props.keyboardShouldPersistTaps === 'handled',
      ).length,
    ).toBeGreaterThan(0);
  });

  it('exposes understandable bootstrap status without a generic spinner', async () => {
    const view = await render(<AppBootstrapScreen />);
    expect(
      view.getByRole('progressbar', { name: 'Uygulama hazırlanıyor…' }),
    ).toBeBusy();
    expect(view.getByText('Smart Kitchen Assistant')).toBeTruthy();
    expect(
      view.container.queryAll((node) =>
        node.type.includes('ActivityIndicator'),
      ),
    ).toHaveLength(0);
    expect(
      view.container.queryAll(
        (node) => node.props.keyboardShouldPersistTaps === 'handled',
      ).length,
    ).toBeGreaterThan(0);
  });
});

it('disables duplicate login while pending', async () => {
  jest.mocked(useAuth).mockReturnValue({ ...useAuth(), pending: 'login' });
  const view = await render(<LoginScreen />);
  const button = view.getByRole('button', { name: 'Giriş yap / Kaydol' });
  expect(button).toBeDisabled();
  expect(button).toBeBusy();
  await fireEvent.press(button);
  expect(login).not.toHaveBeenCalled();
});
it('shows only safe errors and offers restore retry', async () => {
  jest.mocked(useAuth).mockReturnValue({ ...useAuth(), error: 'network' });
  const view = await render(<LoginScreen />);
  expect(view.getByRole('alert').props.children).toBe(
    'Oturum doğrulanamadı. Bağlantınızı kontrol edip tekrar deneyin.',
  );
  await fireEvent.press(
    view.getByRole('button', { name: 'Oturumu tekrar kontrol et' }),
  );
  expect(useAuth().retryRestore).toHaveBeenCalledTimes(1);
});
it('keeps login blocked and offers cleanup retry when logout failed', async () => {
  jest
    .mocked(useAuth)
    .mockReturnValue({ ...useAuth(), error: 'storage', cleanupRequired: true });
  const view = await render(<LoginScreen />);
  expect(
    view.getByRole('button', { name: 'Giriş yap / Kaydol' }),
  ).toBeDisabled();
  await fireEvent.press(
    view.getByRole('button', { name: 'Oturum temizliğini tekrar dene' }),
  );
  expect(logout).toHaveBeenCalledTimes(1);
});
