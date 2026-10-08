import type { ConfigContext, ExpoConfig } from 'expo/config';

import appConfig from '../app.config';
import appJson from '../app.json';

const previousDomain = process.env.EXPO_PUBLIC_AUTH0_DOMAIN;
afterEach(() => {
  if (previousDomain === undefined) {
    delete process.env.EXPO_PUBLIC_AUTH0_DOMAIN;
  } else {
    process.env.EXPO_PUBLIC_AUTH0_DOMAIN = previousDomain;
  }
});

const context = { config: appJson.expo as ExpoConfig } as ConfigContext;

it('preserves static settings and adds exactly one environment-specific Auth0 plugin', () => {
  process.env.EXPO_PUBLIC_AUTH0_DOMAIN = ' Tenant.EU.Auth0.com ';
  const config = appConfig(context);
  expect(config).toEqual({
    ...appJson.expo,
    plugins: [
      ...appJson.expo.plugins,
      ['react-native-auth0', { domain: 'tenant.eu.auth0.com' }],
    ],
  });
  expect(config.android?.package).toBe(
    'com.okanbeyx.smartkitchenassistant.dev',
  );
  expect(config.ios?.bundleIdentifier).toBe(
    'com.okanbeyx.smartkitchenassistant.dev',
  );
  expect(config.scheme).toBe('smartkitchenassistant');
});

it('fails config evaluation when the native callback domain is absent', () => {
  delete process.env.EXPO_PUBLIC_AUTH0_DOMAIN;
  expect(() => appConfig(context)).toThrow('EXPO_PUBLIC_AUTH0_DOMAIN');
});

it('rejects a URL in the native callback hostname', () => {
  process.env.EXPO_PUBLIC_AUTH0_DOMAIN = 'https://tenant.eu.auth0.com/path';
  expect(() => appConfig(context)).toThrow('EXPO_PUBLIC_AUTH0_DOMAIN');
});
