export type AuthErrorKind =
  | 'cancelled'
  | 'provider'
  | 'storage'
  | 'configuration'
  | 'no-session'
  | 'invalid-session'
  | 'network'
  | 'internal'
  | 'stale';

const messages: Record<AuthErrorKind, string> = {
  cancelled: 'Giriş iptal edildi.',
  provider: 'Kimlik doğrulama işlemi tamamlanamadı. Lütfen tekrar deneyin.',
  storage: 'Güvenli oturum kaydı işlemi tamamlanamadı.',
  configuration: 'Giriş yapılandırması geçersiz.',
  'no-session': 'Kayıtlı oturum bulunamadı.',
  'invalid-session': 'Oturumunuz geçersiz. Lütfen yeniden giriş yapın.',
  network: 'Oturum doğrulanamadı. Bağlantınızı kontrol edip tekrar deneyin.',
  internal: 'Oturum işlemi tamamlanamadı. Lütfen tekrar deneyin.',
  stale: 'Oturum değişti. Lütfen tekrar deneyin.',
};

export function authErrorMessage(kind: AuthErrorKind): string {
  return messages[kind];
}

// Deliberately do not retain the SDK error, cause, or response payload.
export class AuthError extends Error {
  constructor(readonly kind: AuthErrorKind) {
    super(messages[kind]);
    this.name = 'AuthError';
  }
}
