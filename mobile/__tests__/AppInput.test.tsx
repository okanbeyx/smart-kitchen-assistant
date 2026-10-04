import { fireEvent, render } from '@testing-library/react-native';

import { AppInput } from '@/shared/components/AppInput';
import { colors, layout } from '@/shared/theme/tokens';

describe('AppInput', () => {
  it('has an accessible label and helper with an ergonomic default state', async () => {
    const view = await render(
      <AppInput label="Malzeme" helper="Malzeme adını yazın" />,
    );
    const input = view.getByLabelText('Malzeme');
    expect(input.props.accessibilityLabelledBy).toBeTruthy();
    expect(input.props.accessibilityHint).toBe('Malzeme adını yazın');
    expect(input.props.editable).toBe(true);
    expect(input).toHaveStyle({
      minHeight: layout.touchTarget,
      borderColor: colors.borderStrong,
    });
    expect(view.getByText('Malzeme adını yazın')).toBeTruthy();
  });

  it('changes focus styling and forwards focus/blur callbacks', async () => {
    const onFocus = jest.fn();
    const onBlur = jest.fn();
    const view = await render(
      <AppInput label="Malzeme" onFocus={onFocus} onBlur={onBlur} />,
    );
    await fireEvent(view.getByLabelText('Malzeme'), 'focus', {});
    expect(view.getByLabelText('Malzeme')).toHaveStyle({
      borderColor: colors.primary,
    });
    await fireEvent(view.getByLabelText('Malzeme'), 'blur', {});
    expect(view.getByLabelText('Malzeme')).toHaveStyle({
      borderColor: colors.borderStrong,
    });
    expect(onFocus).toHaveBeenCalledTimes(1);
    expect(onBlur).toHaveBeenCalledTimes(1);
  });

  it('gives errors precedence over helper and focused state', async () => {
    const view = await render(
      <AppInput label="Miktar" helper="Gram" error="Miktar gerekli" />,
    );
    await fireEvent(view.getByLabelText('Miktar'), 'focus', {});
    expect(view.getByLabelText('Miktar')).toHaveStyle({
      borderColor: colors.error,
    });
    expect(view.getByLabelText('Miktar').props.accessibilityHint).toBe(
      'Hata: Miktar gerekli',
    );
    expect(view.getByText('Miktar gerekli').props.accessibilityLiveRegion).toBe(
      'polite',
    );
    expect(view.queryByText('Gram')).toBeNull();
  });

  it('makes disabled fields noneditable and forwards native value props', async () => {
    const view = await render(
      <AppInput label="Malzeme" value="Süt" disabled />,
    );
    const input = view.getByLabelText('Malzeme');
    expect(input.props.editable).toBe(false);
    expect(input).toBeDisabled();
    expect(input.props.value).toBe('Süt');
    expect(input).toHaveStyle({ backgroundColor: colors.neutralSoft });
    expect(input.props.allowFontScaling).not.toBe(false);
  });
});
