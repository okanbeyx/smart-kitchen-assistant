import { fireEvent, render, userEvent } from '@testing-library/react-native';
import { Animated } from 'react-native';
import { AppButton } from '@/shared/components/AppButton';
import { useReducedMotion } from '@/shared/hooks/useReducedMotion';
import { colors, layout } from '@/shared/theme/tokens';

jest.mock('@/shared/hooks/useReducedMotion', () => ({
  useReducedMotion: jest.fn(() => true),
}));

describe('AppButton', () => {
  afterEach(() => jest.restoreAllMocks());
  beforeEach(() => jest.mocked(useReducedMotion).mockReturnValue(true));

  it('runs an enabled callback and exposes an ergonomic accessible button', async () => {
    const onPress = jest.fn();
    const view = await render(<AppButton label="Devam" onPress={onPress} />);
    const button = view.getByRole('button', { name: 'Devam' });
    expect(button).toBeEnabled();
    expect(button.props.accessibilityState).toEqual({
      disabled: false,
      busy: false,
    });
    expect(button).toHaveStyle({
      minHeight: layout.touchTarget,
      minWidth: layout.touchTarget,
      backgroundColor: colors.primary,
    });
    await userEvent.setup().press(button);
    expect(onPress).toHaveBeenCalledTimes(1);
  });

  it.each([{ disabled: true }, { loading: true }])(
    'blocks callbacks when %j',
    async (state) => {
      const onPress = jest.fn();
      const view = await render(
        <AppButton label="Kaydet" onPress={onPress} {...state} />,
      );
      const button = view.getByRole('button', { name: 'Kaydet' });
      expect(button).toBeDisabled();
      expect(button.props.accessibilityState.busy).toBe('loading' in state);
      await userEvent.setup().press(button);
      await fireEvent.press(button);
      expect(onPress).not.toHaveBeenCalled();
    },
  );

  it('preserves its accessible name and text while loading', async () => {
    const view = await render(
      <AppButton
        label="Kaydet"
        loading
        accessibilityLabel="Değişiklikleri kaydet"
      />,
    );
    expect(
      view.getByRole('button', { name: 'Değişiklikleri kaydet' }),
    ).toBeBusy();
    expect(view.getByText('Kaydet')).toBeTruthy();
    expect(view.getByText('Hazırlanıyor…')).toBeTruthy();
  });

  it.each(['primary', 'secondary', 'ghost'] as const)(
    'supports %s semantic styling',
    async (variant) => {
      const view = await render(<AppButton label="İşlem" variant={variant} />);
      expect(view.getByRole('button')).toHaveStyle({
        backgroundColor:
          variant === 'primary'
            ? colors.primary
            : variant === 'secondary'
              ? colors.primarySoft
              : colors.background,
      });
      expect(view.getByText('İşlem')).toHaveStyle({
        color: variant === 'primary' ? colors.onPrimary : colors.primaryPressed,
      });
    },
  );

  it('keeps immediate pressed color feedback without animation under reduced motion', async () => {
    const timing = jest.spyOn(Animated, 'timing');
    const view = await render(<AppButton label="Devam" />);
    const button = view.getByRole('button');
    await fireEvent(button, 'responderGrant', {
      persist: jest.fn(),
      nativeEvent: { timestamp: 0 },
    });
    expect(view.getByRole('button')).toHaveStyle({
      backgroundColor: colors.primaryPressed,
    });
    expect(timing).not.toHaveBeenCalled();
  });

  it('uses native transform feedback when motion is allowed and stops it when enabled', async () => {
    jest.mocked(useReducedMotion).mockReturnValue(false);
    const animation = { start: jest.fn(), stop: jest.fn(), reset: jest.fn() };
    const timing = jest.spyOn(Animated, 'timing').mockReturnValue(animation);
    const stop = jest.spyOn(Animated.Value.prototype, 'stopAnimation');
    const view = await render(<AppButton label="Devam" />);
    await userEvent.setup().press(view.getByRole('button'));
    expect(timing).toHaveBeenCalledWith(
      expect.anything(),
      expect.objectContaining({ toValue: 0.98, useNativeDriver: true }),
    );
    expect(animation.start).toHaveBeenCalled();
    stop.mockClear();
    jest.mocked(useReducedMotion).mockReturnValue(true);
    await view.rerender(<AppButton label="Devam" />);
    expect(stop).toHaveBeenCalled();
    expect(view.getByRole('button')).toBeTruthy();
  });
});
