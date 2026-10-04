import Ionicons, {
  IoniconsIconName,
} from '@react-native-vector-icons/ionicons';

import { colors, iconSizes } from '@/shared/theme/tokens';

interface AppIconProps {
  name: IoniconsIconName;
  size?: keyof typeof iconSizes;
  tone?: 'primary' | 'textSecondary' | 'success' | 'warning' | 'error';
  label?: string;
}

export function AppIcon({
  name,
  size = 'medium',
  tone = 'textSecondary',
  label,
}: AppIconProps) {
  return (
    <Ionicons
      name={name}
      size={iconSizes[size]}
      color={colors[tone]}
      accessible={!!label}
      accessibilityLabel={label}
      accessibilityRole={label ? 'image' : undefined}
      accessibilityElementsHidden={!label}
      importantForAccessibility={label ? 'yes' : 'no-hide-descendants'}
    />
  );
}
