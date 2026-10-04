import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';

import {
  colors,
  iconSizes,
  layout,
  radius,
  shadows,
  spacing,
  typography,
} from '@/shared/theme/tokens';
import { motion } from '@/shared/theme/motion';

function luminance(hex: string) {
  const values = hex
    .slice(1)
    .match(/../g)!
    .map((value) => parseInt(value, 16) / 255)
    .map((value) =>
      value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4,
    );
  return values[0] * 0.2126 + values[1] * 0.7152 + values[2] * 0.0722;
}
function contrast(a: string, b: string) {
  const values = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (values[0] + 0.05) / (values[1] + 0.05);
}

describe('design token contract', () => {
  it('exports the approved semantic palette and foundation scales', () => {
    expect(colors.brand).toBe('#E94B35');
    expect(colors.primary).toBe('#C93625');
    expect(colors.background).toBe('#FFF8F3');
    expect(Object.keys(colors)).toEqual(
      expect.arrayContaining([
        'surface',
        'textPrimary',
        'textSecondary',
        'textMuted',
        'border',
        'primaryPressed',
        'primarySoft',
        'success',
        'successSoft',
        'warning',
        'warningSoft',
        'error',
        'errorSoft',
        'neutral',
      ]),
    );
    expect(Object.values(spacing)).toEqual([4, 8, 12, 16, 20, 24, 32, 40]);
    expect(Object.keys(typography)).toEqual([
      'display',
      'h1',
      'h2',
      'h3',
      'body',
      'bodyStrong',
      'label',
      'caption',
      'button',
    ]);
    expect(Object.keys(radius)).toEqual(['small', 'medium', 'large', 'pill']);
    expect(Object.keys(shadows)).toEqual(['subtle', 'card', 'floating']);
    expect(layout).toEqual({ compact: 520, wide: 720, touchTarget: 48 });
    expect(iconSizes.medium).toBe(24);
    expect(motion.duration.fast).toBeLessThan(motion.duration.normal);
  });

  it.each([
    [colors.onPrimary, colors.primary],
    [colors.onPrimary, colors.primaryPressed],
    [colors.textPrimary, colors.background],
    [colors.textSecondary, colors.surface],
    [colors.textSecondary, colors.primarySoft],
    [colors.textMuted, colors.background],
    [colors.textSecondary, colors.neutralSoft],
    [colors.primaryPressed, colors.primarySoft],
    [colors.neutral, colors.neutralSoft],
    [colors.success, colors.successSoft],
    [colors.warning, colors.warningSoft],
    [colors.error, colors.errorSoft],
  ])('keeps normal text contrast for %s on %s', (foreground, background) => {
    expect(contrast(foreground, background)).toBeGreaterThanOrEqual(4.5);
  });

  it('uses strong borders for controls and keeps brand red decorative', () => {
    expect(
      contrast(colors.borderStrong, colors.surface),
    ).toBeGreaterThanOrEqual(3);
    expect(contrast(colors.onPrimary, colors.brand)).toBeLessThan(4.5);
  });

  it('keeps hex colors in the token file, not application components', () => {
    const source = path.join(__dirname, '../src');
    function inspect(directory: string): string[] {
      return readdirSync(directory, { withFileTypes: true }).flatMap(
        (entry) => {
          const file = path.join(directory, entry.name);
          if (entry.isDirectory()) return inspect(file);
          if (
            !/\.tsx?$/.test(file) ||
            file === path.join(source, 'shared/theme/tokens.ts')
          )
            return [];
          return /#[\da-f]{3,8}\b/i.test(readFileSync(file, 'utf8'))
            ? [path.relative(source, file)]
            : [];
        },
      );
    }
    expect(inspect(source)).toEqual([]);
  });

  it('aligns native light/splash config without font plugins or identity changes', () => {
    const { expo } = JSON.parse(
      readFileSync(path.join(__dirname, '../app.json'), 'utf8'),
    );
    expect(expo.scheme).toBe('smartkitchenassistant');
    expect(expo.name).toBe('Smart Kitchen Assistant');
    expect(expo.slug).toBe('smart-kitchen-assistant');
    expect(expo.userInterfaceStyle).toBe('light');
    expect(expo.plugins).toContainEqual([
      'expo-splash-screen',
      { backgroundColor: colors.background },
    ]);
    expect(expo.plugins).not.toContain('expo-font');
    expect(expo.plugins).not.toContain('@react-native-vector-icons/ionicons');
  });
});
