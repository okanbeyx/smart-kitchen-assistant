import { useEffect } from 'react';
import { Animated, StyleSheet, useAnimatedValue, View } from 'react-native';

import { useReducedMotion } from '@/shared/hooks/useReducedMotion';
import { motion } from '@/shared/theme/motion';
import { colors, radius, spacing } from '@/shared/theme/tokens';

export function KitchenMark({ animated = false }: { animated?: boolean }) {
  const reducedMotion = useReducedMotion();
  const steam = useAnimatedValue(0);
  const moving = animated && !reducedMotion;

  useEffect(() => {
    steam.stopAnimation();
    steam.setValue(0);
    if (!moving) return;
    const animation = Animated.loop(
      Animated.sequence([
        Animated.timing(steam, {
          toValue: 1,
          duration: motion.duration.ambient,
          easing: motion.easing.standard,
          useNativeDriver: true,
          isInteraction: false,
        }),
        Animated.timing(steam, {
          toValue: 0,
          duration: motion.duration.ambient,
          easing: motion.easing.standard,
          useNativeDriver: true,
          isInteraction: false,
        }),
      ]),
    );
    animation.start();
    return () => animation.stop();
  }, [moving, steam]);

  return (
    <View
      accessible={false}
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
      style={styles.mark}
    >
      <View style={styles.halo} />
      <View style={[styles.ingredient, styles.tomato]} />
      <View style={[styles.ingredient, styles.leaf]} />
      <View style={[styles.ingredient, styles.seed]} />
      <Animated.View
        style={[
          styles.steam,
          {
            opacity: moving
              ? steam.interpolate({
                  inputRange: [0, 1],
                  outputRange: [0.45, 0.9],
                })
              : 0.7,
            transform: [
              {
                translateY: moving
                  ? steam.interpolate({
                      inputRange: [0, 1],
                      outputRange: [0, -motion.steamOffset],
                    })
                  : 0,
              },
            ],
          },
        ]}
      >
        <View style={styles.steamLine} />
        <View style={[styles.steamLine, styles.tallSteam]} />
        <View style={styles.steamLine} />
      </Animated.View>
      <View style={styles.rim} />
      <View style={styles.bowl} />
      <View style={styles.base} />
    </View>
  );
}

// Fixed geometry is confined to this bounded decorative composition, never text/layout.
const styles = StyleSheet.create({
  mark: { width: 208, height: 184, alignSelf: 'center', maxWidth: '100%' },
  halo: {
    position: 'absolute',
    width: 176,
    height: 176,
    borderRadius: radius.pill,
    backgroundColor: colors.primarySoft,
    left: 16,
    top: 4,
  },
  bowl: {
    position: 'absolute',
    width: 120,
    height: 60,
    left: 44,
    top: 96,
    borderBottomLeftRadius: 60,
    borderBottomRightRadius: 60,
    backgroundColor: colors.brand,
  },
  rim: {
    position: 'absolute',
    width: 132,
    height: 12,
    left: 38,
    top: 90,
    borderRadius: radius.pill,
    backgroundColor: colors.primaryPressed,
  },
  base: {
    position: 'absolute',
    width: 48,
    height: 6,
    left: 80,
    top: 158,
    borderRadius: radius.pill,
    backgroundColor: colors.primaryPressed,
  },
  steam: {
    position: 'absolute',
    left: 75,
    top: 48,
    flexDirection: 'row',
    gap: spacing.md,
    alignItems: 'flex-end',
  },
  steamLine: {
    width: 6,
    height: 24,
    borderRadius: radius.pill,
    backgroundColor: colors.primary,
  },
  tallSteam: { height: 32 },
  ingredient: { position: 'absolute', borderRadius: radius.pill },
  tomato: {
    width: 16,
    height: 16,
    left: 22,
    top: 60,
    backgroundColor: colors.brand,
  },
  leaf: {
    width: 22,
    height: 12,
    left: 169,
    top: 74,
    backgroundColor: colors.success,
    transform: [{ rotate: '-35deg' }],
  },
  seed: {
    width: 10,
    height: 10,
    left: 154,
    top: 28,
    backgroundColor: colors.warning,
  },
});
