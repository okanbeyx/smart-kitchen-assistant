/// <reference types="node" />

import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { runInNewContext } from 'node:vm';

import {
  EnvironmentConfigurationError,
  parseApiBaseUrl,
  parseAuth0Domain,
  parseAuth0Environment,
} from '@/core/config/environment';

describe('environment configuration', () => {
  it('accepts a valid absolute API URL', () => {
    expect(parseApiBaseUrl('https://api.example.test')).toBe(
      'https://api.example.test',
    );
  });

  it('rejects an invalid API URL', () => {
    expect(() => parseApiBaseUrl('not-a-url')).toThrow(
      EnvironmentConfigurationError,
    );
    expect(() => parseApiBaseUrl('ftp://api.example.test')).toThrow(
      EnvironmentConfigurationError,
    );
  });

  it('rejects a missing API URL without relying on module import state', () => {
    expect(() => parseApiBaseUrl(undefined)).toThrow(
      EnvironmentConfigurationError,
    );
  });

  it('removes trailing slashes', () => {
    expect(parseApiBaseUrl('http://10.0.2.2:5011///')).toBe(
      'http://10.0.2.2:5011',
    );
  });

  it('inlines the runtime wrapper API URL with the production Expo transform', () => {
    const babel = jest.requireActual('@babel/core');
    const source = readFileSync(
      resolve(__dirname, '../src/core/config/environment.ts'),
      'utf8',
    );
    const previousValue = process.env.EXPO_PUBLIC_API_BASE_URL;
    const buildTimeUrl = 'https://build-time.example.test/';

    try {
      process.env.EXPO_PUBLIC_API_BASE_URL = buildTimeUrl;
      const transformed = babel.transformSync(source, {
        filename: 'environment.ts',
        configFile: false,
        babelrc: false,
        presets: ['babel-preset-expo'],
        plugins: ['@babel/plugin-transform-modules-commonjs'],
        caller: {
          name: 'metro',
          platform: 'android',
          isDev: false,
          isServer: false,
          bundler: 'metro',
        },
      });

      expect(transformed.metadata.publicEnvVars).toContain(
        'EXPO_PUBLIC_API_BASE_URL',
      );
      expect(transformed.code).toContain(buildTimeUrl);

      // The release wrapper must work without a Node process.env at runtime.
      const exports: { getEnvironment?: () => { apiBaseUrl: string } } = {};
      runInNewContext(transformed.code, { exports, URL, require });
      expect(exports.getEnvironment?.()).toEqual({
        apiBaseUrl: 'https://build-time.example.test',
      });
    } finally {
      if (previousValue === undefined) {
        delete process.env.EXPO_PUBLIC_API_BASE_URL;
      } else {
        process.env.EXPO_PUBLIC_API_BASE_URL = previousValue;
      }
    }
  });
});

describe('Auth0 public configuration', () => {
  const valid = {
    domain: 'tenant.eu.auth0.com',
    clientId: 'PUBLIC_NATIVE_ID',
    audience: 'urn:example:api',
  };

  it('normalizes the hostname and trims public identifiers', () => {
    expect(
      parseAuth0Environment({
        domain: ' Tenant.EU.Auth0.com ',
        clientId: ' PUBLIC_NATIVE_ID ',
        audience: ' urn:example:api ',
      }),
    ).toEqual(valid);
  });

  it.each([
    undefined,
    '',
    ' ',
    'https://tenant.auth0.com',
    'tenant.auth0.com/',
    'user@tenant.auth0.com',
    'tenant.auth0.com:443',
    'tenant.auth0.com?x=1',
    'tenant.auth0.com#x',
    'localhost',
    '127.0.0.1',
    '-tenant.auth0.com',
    'tenant..com',
    'tenant_name.auth0.com',
  ])('rejects invalid domain %p', (domain) => {
    expect(() => parseAuth0Domain(domain)).toThrow(
      EnvironmentConfigurationError,
    );
  });

  it.each([undefined, '', ' ', 'client id', '<client-id>', 'client/secret'])(
    'rejects invalid client ID %p',
    (clientId) => {
      expect(() => parseAuth0Environment({ ...valid, clientId })).toThrow(
        EnvironmentConfigurationError,
      );
    },
  );

  it.each([
    undefined,
    '',
    ' ',
    'urn:api with spaces',
    '<audience>',
    'urn:api\u0000',
  ])('rejects invalid audience %p', (audience) => {
    expect(() => parseAuth0Environment({ ...valid, audience })).toThrow(
      EnvironmentConfigurationError,
    );
  });

  it('accepts an HTTPS API identifier without rewriting it', () => {
    const audience = 'https://api.example.test/';
    expect(parseAuth0Environment({ ...valid, audience }).audience).toBe(
      audience,
    );
  });

  it('does not include invalid values in errors', () => {
    expect.assertions(1);
    const unsafe = 'invalid value must not escape';
    try {
      parseAuth0Environment({ ...valid, clientId: unsafe });
    } catch (error) {
      expect(String(error)).not.toContain(unsafe);
    }
  });
});
