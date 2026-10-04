import { PropsWithChildren } from 'react';
import { StyleSheet, View } from 'react-native';

import { colors, radius, shadows, spacing } from '@/shared/theme/tokens';

interface AppCardProps extends PropsWithChildren {
  variant?: 'surface' | 'soft';
  elevated?: boolean;
}

export function AppCard({
  children,
  variant = 'surface',
  elevated = false,
}: AppCardProps) {
  return (
    <View
      style={[
        styles.card,
        variant === 'soft' && styles.soft,
        elevated && shadows.card,
      ]}
    >
      {children}
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: colors.surface,
    borderRadius: radius.large,
    padding: spacing.xxl,
    gap: spacing.md,
  },
  soft: { backgroundColor: colors.primarySoft },
});
