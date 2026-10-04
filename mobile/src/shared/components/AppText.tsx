import { Text, TextProps, StyleSheet } from 'react-native';

import { colors, typography } from '@/shared/theme/tokens';

type TextTone =
  | 'textPrimary'
  | 'textSecondary'
  | 'textMuted'
  | 'primary'
  | 'primaryPressed'
  | 'onPrimary'
  | 'success'
  | 'warning'
  | 'error'
  | 'neutral';
interface AppTextProps extends TextProps {
  variant?: keyof typeof typography;
  tone?: TextTone;
}

export function AppText({
  variant = 'body',
  tone = 'textPrimary',
  style,
  ...props
}: AppTextProps) {
  return (
    <Text
      {...props}
      style={[styles.base, typography[variant], { color: colors[tone] }, style]}
    />
  );
}

const styles = StyleSheet.create({ base: { flexShrink: 1 } });
