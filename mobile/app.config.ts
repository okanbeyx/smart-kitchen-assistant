import type { ConfigContext, ExpoConfig } from 'expo/config';

// Expo evaluates this import in Node; use the pinned Node 22.18+ TS support.
import { parseAuth0Domain } from './src/core/config/environment.ts';

// app.json owns static settings; only the environment-dependent plugin lives here.
export default function appConfig({ config }: ConfigContext): ExpoConfig {
  return {
    ...config,
    name: config.name!,
    slug: config.slug!,
    plugins: [
      ...(config.plugins ?? []),
      [
        'react-native-auth0',
        { domain: parseAuth0Domain(process.env.EXPO_PUBLIC_AUTH0_DOMAIN) },
      ],
    ],
  };
}
