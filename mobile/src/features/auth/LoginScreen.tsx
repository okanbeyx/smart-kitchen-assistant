import { StyleSheet, View } from 'react-native';

import { useAuth } from '@/core/auth/AuthContext';
import { authErrorMessage } from '@/core/auth/authError';

import { AnimatedEntrance } from '@/shared/components/AnimatedEntrance';
import { AppButton } from '@/shared/components/AppButton';
import { AppCard } from '@/shared/components/AppCard';
import { AppScreen } from '@/shared/components/AppScreen';
import { AppText } from '@/shared/components/AppText';
import { KitchenMark } from '@/shared/components/KitchenMark';
import { spacing } from '@/shared/theme/tokens';

export function LoginScreen() {
  const { login, logout, retryRestore, pending, error, cleanupRequired } =
    useAuth();
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
          Mutfağına giriş yap
        </AppText>
        <AppText tone="textSecondary">
          Güvenli giriş yapmak veya hesap oluşturmak için devam et.
        </AppText>
        <AppButton
          label="Giriş yap / Kaydol"
          loading={pending === 'login'}
          disabled={pending !== null || cleanupRequired}
          onPress={() => {
            void login();
          }}
          accessibilityHint="Güvenli giriş sayfasını tarayıcıda açar."
        />
        {error && (
          <AppText accessibilityRole="alert">{authErrorMessage(error)}</AppText>
        )}
        {cleanupRequired && (
          <AppButton
            label="Oturum temizliğini tekrar dene"
            loading={pending === 'logout'}
            onPress={() => {
              void logout();
            }}
          />
        )}
        {!cleanupRequired &&
          error &&
          ['network', 'provider', 'internal'].includes(error) && (
            <AppButton
              label="Oturumu tekrar kontrol et"
              disabled={pending !== null}
              onPress={() => {
                void retryRestore();
              }}
            />
          )}
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
