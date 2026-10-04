import { StyleSheet, View } from 'react-native';

import { AppScreen } from '@/shared/components/AppScreen';
import { AppText } from '@/shared/components/AppText';
import { KitchenMark } from '@/shared/components/KitchenMark';
import { spacing } from '@/shared/theme/tokens';

export function AppBootstrapScreen() {
  return (
    <AppScreen scroll centered>
      <KitchenMark animated />
      <AppText variant="h1" style={styles.center}>
        Smart Kitchen Assistant
      </AppText>
      <View
        accessible
        accessibilityRole="progressbar"
        accessibilityLabel="Uygulama hazırlanıyor…"
        accessibilityState={{ busy: true }}
      >
        <AppText tone="textSecondary" style={styles.center}>
          Uygulama hazırlanıyor…
        </AppText>
      </View>
      <AppText variant="caption" tone="textMuted" style={styles.center}>
        Mutfağında yeni bir başlangıç.
      </AppText>
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  center: { textAlign: 'center', marginHorizontal: spacing.sm },
});
