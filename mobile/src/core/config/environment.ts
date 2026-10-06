const API_BASE_URL_VARIABLE = 'EXPO_PUBLIC_API_BASE_URL';

export interface AppEnvironment {
  apiBaseUrl: string;
}

export interface Auth0Environment {
  domain: string;
  clientId: string;
  audience: string;
}

export function parseAuth0Domain(value: string | undefined): string {
  const domain = value?.trim().toLowerCase();
  // Accept a DNS hostname, never a URL, port, path, or embedded credentials.
  if (
    !domain ||
    domain.length > 253 ||
    !domain.includes('.') ||
    domain
      .split('.')
      .some((label) => !/^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$/.test(label)) ||
    /^\d+(?:\.\d+){3}$/.test(domain)
  ) {
    throw new EnvironmentConfigurationError(
      'EXPO_PUBLIC_AUTH0_DOMAIN must be a DNS hostname without a scheme or path.',
    );
  }
  return domain;
}

export function parseAuth0Environment(values: {
  domain?: string;
  clientId?: string;
  audience?: string;
}): Auth0Environment {
  const domain = parseAuth0Domain(values.domain);
  const clientId = values.clientId?.trim();
  const audience = values.audience?.trim();
  if (!clientId || !/^[a-zA-Z0-9_-]+$/.test(clientId)) {
    throw new EnvironmentConfigurationError(
      'EXPO_PUBLIC_AUTH0_CLIENT_ID must be a non-empty native client identifier.',
    );
  }
  // Audience is an opaque API identifier; URNs and HTTPS identifiers are valid.
  if (!audience || /[\s\x00-\x1f\x7f<>]/.test(audience)) {
    throw new EnvironmentConfigurationError(
      'EXPO_PUBLIC_AUTH0_AUDIENCE must be a non-empty API identifier without whitespace.',
    );
  }
  return { domain, clientId, audience };
}

// Validate at auth initialization, independently of the existing API-only shell.
export function getAuth0Environment(): Auth0Environment {
  return parseAuth0Environment({
    domain: process.env.EXPO_PUBLIC_AUTH0_DOMAIN,
    clientId: process.env.EXPO_PUBLIC_AUTH0_CLIENT_ID,
    audience: process.env.EXPO_PUBLIC_AUTH0_AUDIENCE,
  });
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
