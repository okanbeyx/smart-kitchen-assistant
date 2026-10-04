import { useId, useState } from 'react';
import { StyleSheet, TextInput, TextInputProps, View } from 'react-native';

import { AppText } from '@/shared/components/AppText';
import {
  colors,
  layout,
  radius,
  spacing,
  typography,
} from '@/shared/theme/tokens';

interface AppInputProps extends Omit<TextInputProps, 'style' | 'editable'> {
  label: string;
  helper?: string;
  error?: string;
  disabled?: boolean;
}

export function AppInput({
  label,
  helper,
  error,
  disabled = false,
  onFocus,
  onBlur,
  accessibilityLabel,
  accessibilityState,
  ...props
}: AppInputProps) {
  const [focused, setFocused] = useState(false);
  const labelId = useId();
  const message = error || helper;
  return (
    <View style={styles.container}>
      <AppText nativeID={labelId} variant="label">
        {label}
      </AppText>
      <TextInput
        {...props}
        accessibilityLabel={accessibilityLabel ?? label}
        accessibilityLabelledBy={labelId}
        accessibilityHint={error ? `Hata: ${error}` : helper}
        accessibilityState={{ ...accessibilityState, disabled }}
        editable={!disabled}
        placeholderTextColor={colors.textMuted}
        selectionColor={colors.primary}
        onFocus={(event) => {
          setFocused(true);
          onFocus?.(event);
        }}
        onBlur={(event) => {
          setFocused(false);
          onBlur?.(event);
        }}
        style={[
          styles.input,
          focused && styles.focused,
          !!error && styles.error,
          disabled && styles.disabled,
        ]}
      />
      {!!message && (
        <AppText
          variant="caption"
          tone={error ? 'error' : 'textSecondary'}
          accessibilityLiveRegion={error ? 'polite' : 'none'}
        >
          {message}
        </AppText>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: spacing.sm },
  input: {
    ...typography.body,
    minHeight: layout.touchTarget,
    backgroundColor: colors.surface,
    color: colors.textPrimary,
    borderColor: colors.borderStrong,
    borderWidth: 1,
    borderRadius: radius.medium,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
  },
  focused: { borderColor: colors.primary },
  error: { borderColor: colors.error },
  disabled: {
    backgroundColor: colors.neutralSoft,
    color: colors.textSecondary,
  },
});
