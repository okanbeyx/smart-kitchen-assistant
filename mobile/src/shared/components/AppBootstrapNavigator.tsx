import { Redirect } from 'expo-router';

import { AuthStatus } from '@/core/auth/authSession';
import { AppBootstrapScreen } from '@/shared/components/AppBootstrapScreen';

interface AppBootstrapNavigatorProps {
  status: AuthStatus;
}

export function AppBootstrapNavigator({ status }: AppBootstrapNavigatorProps) {
  if (status === 'bootstrapping') {
    return <AppBootstrapScreen />;
  }

  return (
    <Redirect href={status === 'authenticated' ? '/(app)' : '/(auth)/login'} />
  );
}
