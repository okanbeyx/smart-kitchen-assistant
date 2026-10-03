import { Redirect, Stack } from 'expo-router';

import { useAuth } from '@/core/auth/AuthContext';
import { AppBootstrapScreen } from '@/shared/components/AppBootstrapScreen';

export default function AuthLayout() {
  const { status } = useAuth();

  if (status === 'bootstrapping') {
    return <AppBootstrapScreen />;
  }

  if (status === 'authenticated') {
    return <Redirect href="/(app)" />;
  }

  return <Stack screenOptions={{ headerShown: false }} />;
}
