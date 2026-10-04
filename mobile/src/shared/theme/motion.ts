import { Easing } from 'react-native';

export const motion = {
  duration: { fast: 120, normal: 220, slow: 320, ambient: 1600 },
  easing: {
    standard: Easing.inOut(Easing.ease),
    emphasized: Easing.out(Easing.cubic),
    exit: Easing.in(Easing.quad),
  },
  entranceOffset: 8,
  pressedScale: 0.98,
  steamOffset: 10,
} as const;
