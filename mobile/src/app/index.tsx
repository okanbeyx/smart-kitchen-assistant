import { useAuth } from '@/core/auth/AuthContext';
import { AppBootstrapNavigator } from '@/shared/components/AppBootstrapNavigator';

export default function IndexRoute() {
  const { status } = useAuth();

  return <AppBootstrapNavigator status={status} />;
}
