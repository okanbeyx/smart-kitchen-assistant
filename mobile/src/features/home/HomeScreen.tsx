import { StyleSheet, View } from 'react-native';

import { AnimatedEntrance } from '@/shared/components/AnimatedEntrance';
import { AppCard } from '@/shared/components/AppCard';
import { AppIcon } from '@/shared/components/AppIcon';
import { AppScreen } from '@/shared/components/AppScreen';
import { AppText } from '@/shared/components/AppText';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { spacing } from '@/shared/theme/tokens';

export function HomeScreen() {
  return (
    <AppScreen scroll size="wide">
      <View style={styles.section}>
        <AppText variant="label" tone="primary">
          MUTFAĞINA HOŞ GELDİN
        </AppText>
        <AppText variant="h1" accessibilityRole="header">
          Bugün mutfakta ne var?
        </AppText>
        <AppText tone="textSecondary">
          Küçük fikirler, lezzetli başlangıçlar.
        </AppText>
      </View>
      <AnimatedEntrance>
        <AppCard variant="soft">
          <AppIcon name="restaurant-outline" size="large" tone="primary" />
          <AppText variant="h2" accessibilityRole="header">
            Mutfağını birlikte planlayalım
          </AppText>
          <AppText tone="textSecondary">
            Malzemelerini tanı, tariflerden ilham al, mutfakta yeni olasılıkları
            keşfet.
          </AppText>
        </AppCard>
      </AnimatedEntrance>
      <View style={styles.section}>
        <AppText variant="h3" accessibilityRole="header">
          Sırada neler var?
        </AppText>
        <AppText variant="caption" tone="textMuted">
          Önizleme — özellikler henüz bağlı değil. Aşağıdaki kartlar gerçek veri
          veya sonuç göstermez.
        </AppText>
      </View>
      <AppCard>
        <AppIcon name="basket-outline" tone="primary" />
        <AppText variant="h3" accessibilityRole="header">
          Pantry
        </AppText>
        <AppText tone="textSecondary">
          Mutfağındaki malzemeleri düzenlemek için bir alan.
        </AppText>
        <StatusBadge label="Yakında" />
      </AppCard>
      <AppCard>
        <AppIcon name="book-outline" tone="primary" />
        <AppText variant="h3" accessibilityRole="header">
          Tarifler
        </AppText>
        <AppText tone="textSecondary">
          Yeni tatlar ve günlük yemekler için ilham.
        </AppText>
        <StatusBadge label="Yakında" />
      </AppCard>
      <AppCard>
        <AppIcon name="bulb-outline" tone="primary" />
        <AppText variant="h3" accessibilityRole="header">
          Akıllı Öneriler
        </AppText>
        <AppText tone="textSecondary">
          Elindeki malzemelerle neler yapabileceğini keşfet.
        </AppText>
        <StatusBadge label="Yakında" />
      </AppCard>
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  section: { gap: spacing.sm },
});
