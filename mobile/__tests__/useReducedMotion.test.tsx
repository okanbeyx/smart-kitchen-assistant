import { act, renderHook } from '@testing-library/react-native';
import { AccessibilityInfo } from 'react-native';

import { useReducedMotion } from '@/shared/hooks/useReducedMotion';

describe('useReducedMotion', () => {
  let event: (enabled: boolean) => void;
  let remove: jest.Mock;
  beforeEach(() => {
    remove = jest.fn();
    jest
      .spyOn(AccessibilityInfo, 'addEventListener')
      .mockImplementation((name, handler) => {
        expect(name).toBe('reduceMotionChanged');
        event = handler as unknown as (enabled: boolean) => void;
        return { remove } as unknown as ReturnType<
          typeof AccessibilityInfo.addEventListener
        >;
      });
  });
  afterEach(() => jest.restoreAllMocks());

  it('starts conservatively and asynchronously reads the preference', async () => {
    let resolve!: (value: boolean) => void;
    jest.spyOn(AccessibilityInfo, 'isReduceMotionEnabled').mockReturnValue(
      new Promise((done) => {
        resolve = done;
      }),
    );
    const hook = await renderHook(() => useReducedMotion());
    expect(hook.result.current).toBe(true);
    await act(async () => resolve(false));
    expect(hook.result.current).toBe(false);
  });

  it('honors an enabled initial preference', async () => {
    jest
      .spyOn(AccessibilityInfo, 'isReduceMotionEnabled')
      .mockResolvedValue(true);
    const hook = await renderHook(() => useReducedMotion());
    expect(hook.result.current).toBe(true);
  });

  it('tracks events in both directions and removes the subscription', async () => {
    jest
      .spyOn(AccessibilityInfo, 'isReduceMotionEnabled')
      .mockResolvedValue(false);
    const hook = await renderHook(() => useReducedMotion());
    expect(hook.result.current).toBe(false);
    await act(async () => event(true));
    expect(hook.result.current).toBe(true);
    await act(async () => event(false));
    expect(hook.result.current).toBe(false);
    await hook.unmount();
    expect(remove).toHaveBeenCalledTimes(1);
  });

  it('falls back to reduced motion when reading fails', async () => {
    jest
      .spyOn(AccessibilityInfo, 'isReduceMotionEnabled')
      .mockRejectedValue(new Error('unavailable'));
    const hook = await renderHook(() => useReducedMotion());
    expect(hook.result.current).toBe(true);
  });

  it('does not let a stale async result overwrite a newer event', async () => {
    let resolve!: (value: boolean) => void;
    jest.spyOn(AccessibilityInfo, 'isReduceMotionEnabled').mockReturnValue(
      new Promise((done) => {
        resolve = done;
      }),
    );
    const hook = await renderHook(() => useReducedMotion());
    await act(async () => event(true));
    await act(async () => resolve(false));
    expect(hook.result.current).toBe(true);
  });

  it('does not let a stale rejection overwrite a newer event', async () => {
    let reject!: (error: Error) => void;
    jest.spyOn(AccessibilityInfo, 'isReduceMotionEnabled').mockReturnValue(
      new Promise((_, fail) => {
        reject = fail;
      }),
    );
    const hook = await renderHook(() => useReducedMotion());
    await act(async () => event(false));
    await act(async () => reject(new Error('unavailable')));
    expect(hook.result.current).toBe(false);
  });

  it('ignores pending reads and events after unmount', async () => {
    let resolve!: (value: boolean) => void;
    jest.spyOn(AccessibilityInfo, 'isReduceMotionEnabled').mockReturnValue(
      new Promise((done) => {
        resolve = done;
      }),
    );
    const hook = await renderHook(() => useReducedMotion());
    await hook.unmount();
    await act(async () => {
      event(false);
      resolve(false);
    });
    expect(remove).toHaveBeenCalledTimes(1);
    expect(hook.result.current).toBe(true);
  });
});
