import { render } from '@testing-library/react-native';
import { LoginScreen } from '@/features/auth/LoginScreen';
import { HomeScreen } from '@/features/home/HomeScreen';
import { AppBootstrapScreen } from '@/shared/components/AppBootstrapScreen';

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
  it('makes login informational with no credential inputs or active CTA', async () => {
    const view = await render(<LoginScreen />);
    expect(
      view.getByRole('header', { name: 'Smart Kitchen Assistant' }),
    ).toBeTruthy();
    expect(view.getByText(/şu anda giriş yapılamaz/)).toBeTruthy();
    expect(view.getByRole('button', { name: 'Giriş yakında' })).toBeDisabled();
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
    expect(view.queryByRole('button')).toBeNull();
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
