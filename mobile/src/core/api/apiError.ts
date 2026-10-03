export type ApiErrorKind =
  | 'http'
  | 'validation'
  | 'auth'
  | 'network'
  | 'timeout'
  | 'cancelled'
  | 'parse';

export interface ApiErrorOptions {
  kind: ApiErrorKind;
  status?: number;
  code?: string;
  title?: string;
  detail?: string;
  validationErrors?: Record<string, string[]>;
}

export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  readonly status?: number;
  readonly code?: string;
  readonly title?: string;
  readonly detail?: string;
  readonly validationErrors?: Record<string, string[]>;

  constructor(options: ApiErrorOptions) {
    super(options.title ?? defaultMessage(options.kind));
    this.name = 'ApiError';
    this.kind = options.kind;
    this.status = options.status;
    this.code = options.code;
    this.title = options.title;
    this.detail = options.detail;
    this.validationErrors = options.validationErrors;
  }
}

export function parseApiErrorResponse(
  status: number,
  contentType: string | null,
  responseBody: string,
): ApiError {
  if (!isJsonContentType(contentType)) {
    return new ApiError({ kind: 'parse', status });
  }

  let payload: unknown;

  try {
    payload = JSON.parse(responseBody);
  } catch {
    return new ApiError({ kind: 'parse', status });
  }

  if (!isRecord(payload)) {
    return new ApiError({ kind: 'parse', status });
  }

  const validationErrors = readValidationErrors(payload.errors);

  return new ApiError({
    kind: validationErrors ? 'validation' : 'http',
    status,
    code: readString(payload.code),
    title: readString(payload.title),
    detail: readString(payload.detail),
    validationErrors,
  });
}

function defaultMessage(kind: ApiErrorKind): string {
  switch (kind) {
    case 'auth':
      return 'Authentication is required.';
    case 'network':
      return 'The network request failed.';
    case 'timeout':
      return 'The request timed out.';
    case 'cancelled':
      return 'The request was cancelled.';
    case 'parse':
      return 'The server response could not be read safely.';
    default:
      return 'The API request failed.';
  }
}

function isJsonContentType(contentType: string | null): boolean {
  if (!contentType) {
    return false;
  }

  const normalized = contentType.toLowerCase();
  return (
    normalized.includes('application/json') ||
    normalized.includes('application/problem+json')
  );
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function readString(value: unknown): string | undefined {
  return typeof value === 'string' ? value : undefined;
}

function readValidationErrors(
  value: unknown,
): Record<string, string[]> | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const entries = Object.entries(value);
  const errors: Record<string, string[]> = {};

  for (const [field, messages] of entries) {
    if (
      !Array.isArray(messages) ||
      !messages.every((message) => typeof message === 'string')
    ) {
      return undefined;
    }

    errors[field] = messages;
  }

  return entries.length > 0 ? errors : undefined;
}
