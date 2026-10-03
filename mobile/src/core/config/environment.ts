const API_BASE_URL_VARIABLE = 'EXPO_PUBLIC_API_BASE_URL';

export interface AppEnvironment {
  apiBaseUrl: string;
}

export class EnvironmentConfigurationError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'EnvironmentConfigurationError';
  }
}

export function parseApiBaseUrl(value: string | undefined): string {
  const candidate = value?.trim();

  if (!candidate) {
    throw new EnvironmentConfigurationError(
      `${API_BASE_URL_VARIABLE} is required.`,
    );
  }

  let url: URL;

  try {
    url = new URL(candidate);
  } catch {
    throw new EnvironmentConfigurationError(
      `${API_BASE_URL_VARIABLE} must be an absolute HTTP or HTTPS URL.`,
    );
  }

  if (
    (url.protocol !== 'http:' && url.protocol !== 'https:') ||
    url.username ||
    url.password ||
    url.search ||
    url.hash
  ) {
    throw new EnvironmentConfigurationError(
      `${API_BASE_URL_VARIABLE} must be a safe HTTP or HTTPS base URL.`,
    );
  }

  return url.toString().replace(/\/+$/, '');
}

export function getEnvironment(): AppEnvironment {
  return {
    apiBaseUrl: parseApiBaseUrl(process.env.EXPO_PUBLIC_API_BASE_URL),
  };
}
