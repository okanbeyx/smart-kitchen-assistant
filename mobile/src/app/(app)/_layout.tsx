import { Redirect, Stack } from 'expo-router';

import { useAuth } from '@/core/auth/AuthContext';
import { AppBootstrapScreen } from '@/shared/components/AppBootstrapScreen';

export default function AppLayout() {
  const { status } = useAuth();

  if (status === 'bootstrapping') {
    return <AppBootstrapScreen />;
  }

  if (status === 'unauthenticated') {
    return <Redirect href="/(auth)/login" />;
  }

  return <Stack screenOptions={{ headerShown: false }} />;
}
