import { QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { AuthProvider } from '@/core/auth/AuthContext';
import { getEnvironment } from '@/core/config/environment';
import { appQueryClient } from '@/core/query/queryClient';
import { colors } from '@/shared/theme/tokens';

export default function RootLayout() {
  getEnvironment();

  return (
    <SafeAreaProvider>
      <QueryClientProvider client={appQueryClient}>
        <AuthProvider>
          <StatusBar style="dark" />
          <Stack
            screenOptions={{
              headerShown: false,
              contentStyle: { backgroundColor: colors.background },
            }}
          >
            <Stack.Screen name="index" />
            <Stack.Screen name="(auth)" />
            <Stack.Screen name="(app)" />
          </Stack>
        </AuthProvider>
      </QueryClientProvider>
    </SafeAreaProvider>
  );
}
