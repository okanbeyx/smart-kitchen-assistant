import { PropsWithChildren, useEffect } from 'react';
import { Animated, useAnimatedValue } from 'react-native';

import { useReducedMotion } from '@/shared/hooks/useReducedMotion';
import { motion } from '@/shared/theme/motion';

export function AnimatedEntrance({ children }: PropsWithChildren) {
  const reducedMotion = useReducedMotion();
  const progress = useAnimatedValue(1);

  useEffect(() => {
    progress.stopAnimation();
    if (reducedMotion) {
      progress.setValue(1);
      return;
    }
    progress.setValue(0);
    const animation = Animated.timing(progress, {
      toValue: 1,
      duration: motion.duration.normal,
      easing: motion.easing.emphasized,
      useNativeDriver: true,
      isInteraction: false,
    });
    animation.start();
    return () => animation.stop();
  }, [progress, reducedMotion]);

  return (
    <Animated.View
      style={{
        opacity: reducedMotion ? 1 : progress,
        transform: [
          {
            translateY: reducedMotion
              ? 0
              : progress.interpolate({
                  inputRange: [0, 1],
                  outputRange: [motion.entranceOffset, 0],
                }),
          },
        ],
      }}
    >
      {children}
    </Animated.View>
  );
}
