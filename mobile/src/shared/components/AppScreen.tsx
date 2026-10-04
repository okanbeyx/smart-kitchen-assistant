import { PropsWithChildren } from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';
import { Edge, SafeAreaView } from 'react-native-safe-area-context';

import { colors, layout, spacing } from '@/shared/theme/tokens';

interface AppScreenProps extends PropsWithChildren {
  scroll?: boolean;
  size?: 'compact' | 'wide';
  centered?: boolean;
  edges?: Edge[];
}

export function AppScreen({
  children,
  scroll = false,
  size = 'compact',
  centered = false,
  edges = ['top', 'right', 'bottom', 'left'],
}: AppScreenProps) {
  const content = (
    <View style={[styles.content, { maxWidth: layout[size] }]}>{children}</View>
  );
  const containerStyle = [styles.container, centered && styles.centered];

  return (
    <SafeAreaView edges={edges} style={styles.screen}>
      {scroll ? (
        <ScrollView
          contentContainerStyle={containerStyle}
          keyboardShouldPersistTaps="handled"
        >
          {content}
        </ScrollView>
      ) : (
        <View style={containerStyle}>{content}</View>
      )}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.background },
  container: {
    flexGrow: 1,
    alignItems: 'center',
    paddingHorizontal: spacing.xl,
    paddingVertical: spacing.xxl,
  },
  centered: { justifyContent: 'center' },
  content: { width: '100%', gap: spacing.xxl },
});
