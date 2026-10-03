import * as SecureStore from 'expo-secure-store';

import { secureSessionStore } from '@/core/storage/secureSessionStore';

jest.mock('expo-secure-store', () => ({
  getItemAsync: jest.fn(),
  setItemAsync: jest.fn(),
  deleteItemAsync: jest.fn(),
}));

const mockedSecureStore = jest.mocked(SecureStore);

describe('secureSessionStore', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('writes and reads an opaque credential', async () => {
    mockedSecureStore.setItemAsync.mockResolvedValue();
    mockedSecureStore.getItemAsync.mockResolvedValue('opaque-credential');

    await secureSessionStore.write('opaque-credential');

    expect(await secureSessionStore.read()).toBe('opaque-credential');
    expect(mockedSecureStore.setItemAsync).toHaveBeenCalledWith(
      expect.any(String),
      'opaque-credential',
    );
  });

  it('returns null when no credential exists', async () => {
    mockedSecureStore.getItemAsync.mockResolvedValue(null);

    await expect(secureSessionStore.read()).resolves.toBeNull();
  });

  it('deletes the credential', async () => {
    mockedSecureStore.deleteItemAsync.mockResolvedValue();

    await secureSessionStore.delete();

    expect(mockedSecureStore.deleteItemAsync).toHaveBeenCalledWith(
      expect.any(String),
    );
  });

  it('wraps native storage failures without exposing the original message', async () => {
    mockedSecureStore.getItemAsync.mockRejectedValue(
      new Error('native keystore internal detail'),
    );

    await expect(secureSessionStore.read()).rejects.toMatchObject({
      name: 'SecureStorageError',
      operation: 'read',
    });
    await expect(secureSessionStore.read()).rejects.not.toThrow(
      'native keystore internal detail',
    );
  });
});
