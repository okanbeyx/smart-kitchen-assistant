import { StyleSheet, View } from 'react-native';

import { AnimatedEntrance } from '@/shared/components/AnimatedEntrance';
import { AppButton } from '@/shared/components/AppButton';
import { AppCard } from '@/shared/components/AppCard';
import { AppScreen } from '@/shared/components/AppScreen';
import { AppText } from '@/shared/components/AppText';
import { KitchenMark } from '@/shared/components/KitchenMark';
import { spacing } from '@/shared/theme/tokens';

export function LoginScreen() {
  return (
    <AppScreen scroll centered>
      <AnimatedEntrance>
        <View style={styles.hero}>
          <KitchenMark />
          <AppText variant="label" tone="primary" style={styles.center}>
            MUTFAĞINDAN İLHAM AL
          </AppText>
          <AppText
            variant="display"
            accessibilityRole="header"
            style={styles.center}
          >
            Smart Kitchen Assistant
          </AppText>
          <AppText tone="textSecondary" style={styles.center}>
            Elindeki malzemeler, yeni fikirlerin başlangıcı.
          </AppText>
        </View>
      </AnimatedEntrance>
      <AppCard elevated>
        <AppText variant="h3" accessibilityRole="header">
          Güzel şeyler hazırlanıyor
        </AppText>
        <AppText tone="textSecondary">
          Güvenli giriş entegrasyonu henüz hazır değil. Bu ekran yalnızca
          uygulamanın tanıtımıdır; şu anda giriş yapılamaz.
        </AppText>
        <AppButton
          label="Giriş yakında"
          disabled
          accessibilityHint="Güvenli giriş entegrasyonu sonraki çalışmada eklenecek."
        />
      </AppCard>
      <AppText variant="caption" tone="textMuted" style={styles.center}>
        Daha az israf, daha çok mutfak keyfi.
      </AppText>
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  hero: { gap: spacing.lg },
  center: { textAlign: 'center' },
});
