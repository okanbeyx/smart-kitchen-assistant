import { render } from '@testing-library/react-native';
import { StyleSheet } from 'react-native';
import { AppCard } from '@/shared/components/AppCard';
import { AppIcon } from '@/shared/components/AppIcon';
import { AppText } from '@/shared/components/AppText';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { colors, iconSizes } from '@/shared/theme/tokens';

jest.mock('@react-native-vector-icons/ionicons', () => {
  const React = jest.requireActual<typeof import('react')>('react');
  const { Text } =
    jest.requireActual<typeof import('react-native')>('react-native');
  return {
    __esModule: true,
    default: (props: object) =>
      React.createElement(Text, { ...props, testID: 'ionicon' }),
  };
});

describe('card, status and icon primitives', () => {
  it('renders a card as a nonpressable visual container', async () => {
    const view = await render(
      <AppCard variant="soft">
        <AppText>Önizleme</AppText>
      </AppCard>,
    );
    expect(view.queryByRole('button')).toBeNull();
    expect(
      view.container.queryAll((node) => !!node.props.onPress),
    ).toHaveLength(0);
    expect(
      view.container.queryAll(
        (node) =>
          StyleSheet.flatten(node.props.style)?.backgroundColor ===
          colors.primarySoft,
      ),
    ).toHaveLength(1);
  });

  it.each(['neutral', 'success', 'warning', 'error'] as const)(
    'keeps textual meaning for %s status',
    async (tone) => {
      const view = await render(
        <StatusBadge tone={tone} label="Durum açıklaması" />,
      );
      expect(view.getByText('Durum açıklaması')).toHaveStyle({
        color: colors[tone],
      });
      expect(
        view.getByText('Durum açıklaması').props.accessibilityElementsHidden,
      ).not.toBe(true);
    },
  );

  it('hides decorative icons and uses semantic size/color', async () => {
    const view = await render(
      <AppIcon name="basket-outline" size="large" tone="primary" />,
    );
    const icon = view.getByTestId('ionicon', { includeHiddenElements: true });
    expect(icon.props.size).toBe(iconSizes.large);
    expect(icon.props.color).toBe(colors.primary);
    expect(icon.props.accessible).toBe(false);
    expect(icon.props.accessibilityElementsHidden).toBe(true);
    expect(icon.props.importantForAccessibility).toBe('no-hide-descendants');
  });

  it('allows a meaningful accessible label when an icon is not decorative', async () => {
    const view = await render(
      <AppIcon name="basket-outline" label="Malzeme sepeti" />,
    );
    expect(view.getByRole('image', { name: 'Malzeme sepeti' })).toBeTruthy();
  });
});
