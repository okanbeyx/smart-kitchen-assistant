import { StyleSheet, View } from 'react-native';

import { AppText } from '@/shared/components/AppText';
import { colors, radius, spacing } from '@/shared/theme/tokens';

type StatusTone = 'neutral' | 'success' | 'warning' | 'error';
export function StatusBadge({
  label,
  tone = 'neutral',
}: {
  label: string;
  tone?: StatusTone;
}) {
  return (
    <View style={[styles.badge, { backgroundColor: colors[`${tone}Soft`] }]}>
      <AppText variant="caption" tone={tone}>
        {label}
      </AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  badge: {
    alignSelf: 'flex-start',
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.xs,
    borderRadius: radius.pill,
  },
});
