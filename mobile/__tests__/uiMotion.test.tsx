import { render } from '@testing-library/react-native';
import { Animated, StyleSheet } from 'react-native';
import { AnimatedEntrance } from '@/shared/components/AnimatedEntrance';
import { AppText } from '@/shared/components/AppText';
import { KitchenMark } from '@/shared/components/KitchenMark';
import { useReducedMotion } from '@/shared/hooks/useReducedMotion';

jest.mock('@/shared/hooks/useReducedMotion', () => ({
  useReducedMotion: jest.fn(() => true),
}));

describe('decorative motion policy', () => {
  beforeEach(() => jest.mocked(useReducedMotion).mockReturnValue(true));
  afterEach(() => jest.restoreAllMocks());

  it('does not start entrances or steam loops with reduced motion', async () => {
    const timing = jest.spyOn(Animated, 'timing');
    const loop = jest.spyOn(Animated, 'loop');
    const view = await render(
      <AnimatedEntrance>
        <AppText>Görünür içerik</AppText>
        <KitchenMark animated />
      </AnimatedEntrance>,
    );
    expect(timing).not.toHaveBeenCalled();
    expect(loop).not.toHaveBeenCalled();
    expect(view.getByText('Görünür içerik')).toBeTruthy();
    expect(view.root).toHaveStyle({
      opacity: 1,
      transform: [{ translateY: 0 }],
    });
  });

  it('starts a native entrance and stops it with visible final content on preference change', async () => {
    jest.mocked(useReducedMotion).mockReturnValue(false);
    const animation = { start: jest.fn(), stop: jest.fn(), reset: jest.fn() };
    const timing = jest.spyOn(Animated, 'timing').mockReturnValue(animation);
    const view = await render(
      <AnimatedEntrance>
        <AppText>İçerik</AppText>
      </AnimatedEntrance>,
    );
    expect(timing).toHaveBeenCalledWith(
      expect.anything(),
      expect.objectContaining({ useNativeDriver: true, toValue: 1 }),
    );
    expect(animation.start).toHaveBeenCalledTimes(1);
    jest.mocked(useReducedMotion).mockReturnValue(true);
    await view.rerender(
      <AnimatedEntrance>
        <AppText>İçerik</AppText>
      </AnimatedEntrance>,
    );
    expect(animation.stop).toHaveBeenCalledTimes(1);
    expect(view.root).toHaveStyle({
      opacity: 1,
      transform: [{ translateY: 0 }],
    });
    expect(view.getByText('İçerik')).toBeTruthy();
  });

  it('runs steam only when requested and stops the loop on reduced motion', async () => {
    jest.mocked(useReducedMotion).mockReturnValue(false);
    const animation = { start: jest.fn(), stop: jest.fn(), reset: jest.fn() };
    const loop = jest.spyOn(Animated, 'loop').mockReturnValue(animation);
    const view = await render(<KitchenMark />);
    expect(loop).not.toHaveBeenCalled();
    await view.rerender(<KitchenMark animated />);
    expect(animation.start).toHaveBeenCalledTimes(1);
    jest.mocked(useReducedMotion).mockReturnValue(true);
    await view.rerender(<KitchenMark animated />);
    expect(animation.stop).toHaveBeenCalledTimes(1);
    const staticSteam = view.container.queryAll(
      (node) => StyleSheet.flatten(node.props.style)?.opacity === 0.7,
    );
    expect(staticSteam).toHaveLength(1);
    expect(staticSteam[0]).toHaveStyle({ transform: [{ translateY: 0 }] });
  });

  it('cleans up active loops and entrances on unmount', async () => {
    jest.mocked(useReducedMotion).mockReturnValue(false);
    const animation = { start: jest.fn(), stop: jest.fn(), reset: jest.fn() };
    jest.spyOn(Animated, 'loop').mockReturnValue(animation);
    jest.spyOn(Animated, 'timing').mockReturnValue(animation);
    const view = await render(
      <AnimatedEntrance>
        <KitchenMark animated />
      </AnimatedEntrance>,
    );
    await view.unmount();
    expect(animation.stop).toHaveBeenCalledTimes(2);
  });

  it('hides all food composition parts from the accessibility tree', async () => {
    const view = await render(<KitchenMark />);
    expect(view.root?.props.accessibilityElementsHidden).toBe(true);
    expect(view.root?.props.importantForAccessibility).toBe(
      'no-hide-descendants',
    );
  });
});
