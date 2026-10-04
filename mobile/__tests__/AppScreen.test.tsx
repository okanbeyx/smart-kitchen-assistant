import { render } from '@testing-library/react-native';
import { StyleSheet } from 'react-native';
import { AppScreen } from '@/shared/components/AppScreen';
import { AppText } from '@/shared/components/AppText';
import { colors, layout, spacing } from '@/shared/theme/tokens';

describe('AppScreen', () => {
  it('owns safe-area edges, warm background and compact width', async () => {
    const view = await render(
      <AppScreen>
        <AppText>İçerik</AppText>
      </AppScreen>,
    );
    const safeArea = view.container.queryAll((node) => !!node.props.edges)[0];
    // The native safe-area component normalizes the public edge array.
    expect(safeArea.props.edges).toEqual({
      top: 'additive',
      right: 'additive',
      bottom: 'additive',
      left: 'additive',
    });
    expect(safeArea).toHaveStyle({ backgroundColor: colors.background });
    expect(
      view.container.queryAll(
        (node) =>
          StyleSheet.flatten(node.props.style)?.maxWidth === layout.compact,
      ),
    ).toHaveLength(1);
    expect(
      view.container.queryAll((node) => node.props.keyboardShouldPersistTaps),
    ).toHaveLength(0);
  });

  it('supports reachable scrolling, bounded wide content and custom edges', async () => {
    const view = await render(
      <AppScreen scroll centered size="wide" edges={['bottom']}>
        <AppText>Uzun içerik</AppText>
      </AppScreen>,
    );
    const safeArea = view.container.queryAll((node) => !!node.props.edges)[0];
    expect(safeArea.props.edges).toEqual({
      top: 'off',
      right: 'off',
      bottom: 'additive',
      left: 'off',
    });
    const scroll = view.container.queryAll(
      (node) => node.props.keyboardShouldPersistTaps === 'handled',
    )[0];
    expect(scroll).toBeTruthy();
    expect(StyleSheet.flatten(scroll.props.contentContainerStyle)).toEqual(
      expect.objectContaining({
        flexGrow: 1,
        paddingHorizontal: spacing.xl,
        justifyContent: 'center',
      }),
    );
    expect(
      view.container.queryAll(
        (node) =>
          StyleSheet.flatten(node.props.style)?.maxWidth === layout.wide,
      ),
    ).toHaveLength(1);
    expect(view.getByText('Uzun içerik')).toBeTruthy();
  });
});
