/// <reference types="node" />

import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { runInNewContext } from 'node:vm';

import {
  EnvironmentConfigurationError,
  parseApiBaseUrl,
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
