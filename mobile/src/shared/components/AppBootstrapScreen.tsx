import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';

export function AppBootstrapScreen() {
  return (
    <View style={styles.container} accessibilityRole="progressbar">
      <ActivityIndicator size="large" color="#2563eb" />
      <Text style={styles.label}>Uygulama hazırlanıyor…</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 16,
    backgroundColor: '#f8fafc',
  },
  label: {
    color: '#334155',
    fontSize: 16,
  },
});
