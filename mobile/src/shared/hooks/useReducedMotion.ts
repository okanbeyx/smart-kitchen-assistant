import { useEffect, useState } from 'react';
import { AccessibilityInfo } from 'react-native';

export function useReducedMotion() {
  // Unknown or unreadable preferences never opt the user into decorative motion.
  const [reducedMotion, setReducedMotion] = useState(true);

  useEffect(() => {
    let active = true;
    let eventReceived = false;
    const subscription = AccessibilityInfo.addEventListener(
      'reduceMotionChanged',
      (enabled) => {
        eventReceived = true;
        if (active) setReducedMotion(enabled);
      },
    );

    async function readPreference() {
      try {
        const enabled = await AccessibilityInfo.isReduceMotionEnabled();
        if (active && !eventReceived) setReducedMotion(enabled);
      } catch {
        if (active && !eventReceived) setReducedMotion(true);
      }
    }
    void readPreference();

    return () => {
      active = false;
      subscription.remove();
    };
  }, []);

  return reducedMotion;
}
