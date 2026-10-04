import { render } from '@testing-library/react-native';
import { StyleSheet } from 'react-native';
import { AppText } from '@/shared/components/AppText';
import { colors, typography } from '@/shared/theme/tokens';

describe('AppText', () => {
  it('defaults to system body text with scaling enabled', async () => {
    const view = await render(<AppText>İı Şş Ğğ Üü Öö Çç</AppText>);
    const text = view.getByText('İı Şş Ğğ Üü Öö Çç');
    expect(text).toHaveStyle(typography.body);
    expect(text).toHaveStyle({ color: colors.textPrimary });
    expect(text.props.allowFontScaling).not.toBe(false);
    expect(StyleSheet.flatten(text.props.style).fontFamily).toBeUndefined();
  });

  it('forwards accessible heading props and applies a semantic variant', async () => {
    const view = await render(
      <AppText
        variant="h2"
        tone="primary"
        accessibilityRole="header"
        accessibilityLabel="Başlık"
      >
        Başlık
      </AppText>,
    );
    expect(view.getByRole('header', { name: 'Başlık' })).toHaveStyle({
      ...typography.h2,
      color: colors.primary,
    });
  });
});
