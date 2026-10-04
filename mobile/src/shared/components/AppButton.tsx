import { useEffect } from 'react';
import {
  Animated,
  Pressable,
  PressableProps,
  StyleSheet,
  useAnimatedValue,
} from 'react-native';

import { AppText } from '@/shared/components/AppText';
import { useReducedMotion } from '@/shared/hooks/useReducedMotion';
import { motion } from '@/shared/theme/motion';
import { colors, layout, radius, spacing } from '@/shared/theme/tokens';

interface AppButtonProps extends Omit<PressableProps, 'children' | 'style'> {
  label: string;
  variant?: 'primary' | 'secondary' | 'ghost';
  loading?: boolean;
}

export function AppButton({
  label,
  variant = 'primary',
  loading = false,
  disabled = false,
  accessibilityLabel,
  accessibilityState,
  onPress,
  onPressIn,
  onPressOut,
  ...props
}: AppButtonProps) {
  const blocked = disabled || loading;
  const reducedMotion = useReducedMotion();
  const scale = useAnimatedValue(1);

  useEffect(() => {
    if (reducedMotion || blocked) {
      scale.stopAnimation();
      scale.setValue(1);
    }
    return () => scale.stopAnimation();
  }, [blocked, reducedMotion, scale]);

  function animateScale(toValue: number) {
    scale.stopAnimation();
    if (reducedMotion || blocked) {
      scale.setValue(1);
      return;
    }
    Animated.timing(scale, {
      toValue,
      duration: motion.duration.fast,
      easing: motion.easing.standard,
      useNativeDriver: true,
    }).start();
  }

  return (
    <Animated.View
      style={{ transform: [{ scale: reducedMotion || blocked ? 1 : scale }] }}
    >
      <Pressable
        {...props}
        accessibilityRole="button"
        accessibilityLabel={accessibilityLabel ?? label}
        accessibilityState={{
          ...accessibilityState,
          disabled: blocked,
          busy: loading,
        }}
        disabled={blocked}
        onPress={(event) => {
          if (!blocked) onPress?.(event);
        }}
        onPressIn={(event) => {
          if (!blocked) {
            animateScale(motion.pressedScale);
            onPressIn?.(event);
          }
        }}
        onPressOut={(event) => {
          animateScale(1);
          if (!blocked) onPressOut?.(event);
        }}
        style={({ pressed }) => [
          styles.button,
          styles[variant],
          pressed && !blocked && styles[`${variant}Pressed`],
          blocked && styles.disabled,
        ]}
      >
        <AppText
          variant="button"
          tone={
            blocked
              ? 'textSecondary'
              : variant === 'primary'
                ? 'onPrimary'
                : 'primaryPressed'
          }
          style={styles.label}
        >
          {label}
        </AppText>
        {loading && (
          <AppText variant="caption" tone="textSecondary">
            Hazırlanıyor…
          </AppText>
        )}
      </Pressable>
    </Animated.View>
  );
}

const styles = StyleSheet.create({
  button: {
    minHeight: layout.touchTarget,
    minWidth: layout.touchTarget,
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.xl,
    borderRadius: radius.medium,
    alignItems: 'center',
    justifyContent: 'center',
    gap: spacing.xs,
  },
  primary: { backgroundColor: colors.primary },
  primaryPressed: { backgroundColor: colors.primaryPressed },
  secondary: { backgroundColor: colors.primarySoft },
  secondaryPressed: { backgroundColor: colors.neutralSoft },
  ghost: { backgroundColor: colors.background },
  ghostPressed: { backgroundColor: colors.primarySoft },
  disabled: { backgroundColor: colors.neutralSoft },
  label: { textAlign: 'center' },
});
