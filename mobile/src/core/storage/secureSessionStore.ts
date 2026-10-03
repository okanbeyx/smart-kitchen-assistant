import * as SecureStore from 'expo-secure-store';

const SESSION_CREDENTIAL_KEY =
  'smart-kitchen-assistant.auth.session-credential';

export type SecureStorageOperation = 'read' | 'write' | 'delete';

export class SecureStorageError extends Error {
  readonly operation: SecureStorageOperation;

  constructor(operation: SecureStorageOperation) {
    super(`Secure credential ${operation} failed.`);
    this.name = 'SecureStorageError';
    this.operation = operation;
  }
}

export interface SecureSessionStore {
  read(): Promise<string | null>;
  write(credential: string): Promise<void>;
  delete(): Promise<void>;
}

export const secureSessionStore: SecureSessionStore = {
  async read() {
    try {
      return await SecureStore.getItemAsync(SESSION_CREDENTIAL_KEY);
    } catch {
      throw new SecureStorageError('read');
    }
  },

  async write(credential) {
    if (!credential) {
      throw new TypeError('Secure credential must not be empty.');
    }

    try {
      await SecureStore.setItemAsync(SESSION_CREDENTIAL_KEY, credential);
    } catch {
      throw new SecureStorageError('write');
    }
  },

  async delete() {
    try {
      await SecureStore.deleteItemAsync(SESSION_CREDENTIAL_KEY);
    } catch {
      throw new SecureStorageError('delete');
    }
  },
};
