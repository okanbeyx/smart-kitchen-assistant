import { Platform, TextStyle, ViewStyle } from 'react-native';

export const colors = {
  brand: '#E94B35',
  background: '#FFF8F3',
  surface: '#FFFFFF',
  textPrimary: '#2B211D',
  textSecondary: '#6B554B',
  textMuted: '#80685D',
  border: '#E7D8CD',
  borderStrong: '#9C8173',
  primary: '#C93625',
  primaryPressed: '#B83225',
  primarySoft: '#FDE9E3',
  onPrimary: '#FFFFFF',
  success: '#34764A',
  successSoft: '#EAF4E9',
  warning: '#9A4F0A',
  warningSoft: '#FFF0D9',
  error: '#B83225',
  errorSoft: '#FCE8E5',
  neutral: '#6B554B',
  neutralSoft: '#F3E8DF',
} as const;

export const spacing = {
  xs: 4,
  sm: 8,
  md: 12,
  lg: 16,
  xl: 20,
  xxl: 24,
  xxxl: 32,
  huge: 40,
} as const;
export const radius = { small: 8, medium: 12, large: 20, pill: 999 } as const;
export const layout = { compact: 520, wide: 720, touchTarget: 48 } as const;
export const iconSizes = { small: 18, medium: 24, large: 32 } as const;

export const typography = {
  display: { fontSize: 32, lineHeight: 40, fontWeight: '700' },
  h1: { fontSize: 28, lineHeight: 36, fontWeight: '700' },
  h2: { fontSize: 24, lineHeight: 32, fontWeight: '700' },
  h3: { fontSize: 20, lineHeight: 28, fontWeight: '600' },
  body: { fontSize: 16, lineHeight: 24, fontWeight: '400' },
  bodyStrong: { fontSize: 16, lineHeight: 24, fontWeight: '600' },
  label: { fontSize: 14, lineHeight: 20, fontWeight: '600' },
  caption: { fontSize: 12, lineHeight: 18, fontWeight: '400' },
  button: { fontSize: 16, lineHeight: 24, fontWeight: '600' },
} as const satisfies Record<string, TextStyle>;

function shadow(
  opacity: number,
  offset: number,
  blur: number,
  elevation: number,
): ViewStyle {
  return Platform.select({
    ios: {
      shadowColor: colors.textPrimary,
      shadowOpacity: opacity,
      shadowOffset: { width: 0, height: offset },
      shadowRadius: blur,
    },
    android: { elevation, shadowColor: colors.textPrimary },
    default: {},
  });
}

export const shadows = {
  subtle: shadow(0.04, 1, 3, 1),
  card: shadow(0.06, 2, 8, 2),
  floating: shadow(0.08, 4, 12, 4),
} as const;
