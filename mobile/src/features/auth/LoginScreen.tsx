import { StyleSheet, Text, useWindowDimensions, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

export function LoginScreen() {
  const { width } = useWindowDimensions();
  const contentWidth = Math.min(Math.max(width - 32, 0), 520);

  return (
    <SafeAreaView style={styles.screen}>
      <View style={[styles.content, { width: contentWidth }]}>
        <Text accessibilityRole="header" style={styles.title}>
          Smart Kitchen Assistant
        </Text>
        <Text style={styles.description}>
          Güvenli giriş entegrasyonu ayrı authentication çalışmasında eklenecek.
        </Text>
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#f8fafc',
  },
  content: {
    alignItems: 'center',
    gap: 12,
    padding: 24,
  },
  title: {
    color: '#0f172a',
    fontSize: 28,
    fontWeight: '700',
    textAlign: 'center',
  },
  description: {
    color: '#475569',
    fontSize: 16,
    lineHeight: 24,
    textAlign: 'center',
  },
});
