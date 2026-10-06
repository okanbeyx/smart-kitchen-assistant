import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import {
  copyFileSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const {
  verifyAuth0Patch,
  verifyContents,
} = require('../scripts/verify-auth0-patch.cjs');
const projectRoot = resolve(__dirname, '..');
const sdkRoot = join(projectRoot, 'node_modules', 'react-native-auth0');
const nativeRelative = 'node_modules/react-native-auth0/ios/NativeBridge.swift';
const patchRelative = 'patches/react-native-auth0+5.11.1.patch';
const read = (path: string) => readFileSync(path, 'utf8');
const snapshot = () => ({
  appPackage: JSON.parse(read(join(projectRoot, 'package.json'))),
  sdkPackage: JSON.parse(read(join(sdkRoot, 'package.json'))),
  podspec: read(join(sdkRoot, 'A0Auth0.podspec')),
  nativeSource: read(join(projectRoot, nativeRelative)),
  patch: read(join(projectRoot, patchRelative)),
});

it('verifies the installed patch and the required install hook without writing', () => {
  const before = snapshot();
  expect(() => verifyAuth0Patch(projectRoot)).not.toThrow();
  expect(() => verifyAuth0Patch(projectRoot)).not.toThrow();
  expect(snapshot()).toEqual(before);
  expect(before.appPackage.devDependencies['patch-package']).toBe('8.0.1');
  expect(before.appPackage.scripts.postinstall).toBe(
    'patch-package --error-on-fail && node scripts/verify-auth0-patch.cjs',
  );
});

it.each(['appPackage', 'sdkPackage'] as const)(
  'rejects a changed %s SDK version',
  (key) => {
    const input = snapshot();
    if (key === 'appPackage')
      input.appPackage.dependencies['react-native-auth0'] = '^5.11.1';
    else input.sdkPackage.version = '5.11.2';
    expect(() => verifyContents(input)).toThrow(
      'exactly react-native-auth0 5.11.1',
    );
  },
);

it.each(['2.25.0', '1.3.0'])(
  'rejects a changed native dependency %s',
  (version) => {
    const input = snapshot();
    input.podspec = input.podspec.replace(version, '0.0.0');
    expect(() => verifyContents(input)).toThrow(
      'native dependency versions changed',
    );
  },
);

it.each(['patch', 'nativeSource'] as const)(
  'rejects missing or altered %s',
  (key) => {
    const input = snapshot();
    input[key] = '';
    expect(() => verifyContents(input)).toThrow();
    input[key] = snapshot()[key].replace(
      'return removed && !deletionFailed',
      'return true',
    );
    expect(() => verifyContents(input)).toThrow();
  },
);

it('normalizes only checkout line endings', () => {
  const input = snapshot();
  input.patch = input.patch.replace(/\r?\n/g, '\r\n');
  input.nativeSource = input.nativeSource.replace(/\r?\n/g, '\r\n');
  expect(() => verifyContents(input)).not.toThrow();
});

// This checks real patch-package application, not native Swift/Keychain execution.
it('applies to pristine 5.11.1 source, reapplies idempotently, and fails on incompatible source', () => {
  const prefix = join(tmpdir(), 'auth0-patch-test-');
  const fixture = mkdtempSync(prefix);
  const patchPackage = join(
    projectRoot,
    'node_modules',
    'patch-package',
    'index.js',
  );
  const apply = (...args: string[]) =>
    spawnSync(process.execPath, [patchPackage, '--error-on-fail', ...args], {
      cwd: fixture,
      encoding: 'utf8',
      timeout: 20_000,
    });
  try {
    for (const relative of [
      'package.json',
      patchRelative,
      nativeRelative,
      'node_modules/react-native-auth0/package.json',
      'node_modules/react-native-auth0/A0Auth0.podspec',
    ]) {
      const destination = join(fixture, relative);
      mkdirSync(resolve(destination, '..'), { recursive: true });
      copyFileSync(join(projectRoot, relative), destination);
    }
    expect(apply('--reverse').status).toBe(0);
    const nativePath = join(fixture, nativeRelative);
    const original = read(nativePath);
    expect(original).not.toContain('A0IdempotentCredentialsStorage');
    expect(
      createHash('sha256')
        .update(original.replace(/\r\n/g, '\n'))
        .digest('hex'),
    ).toBe('e4e5ea1ca89d0ad66a2564d9746a129a619254f06339ea1142777d5655532023');
    const first = apply();
    expect({
      status: first.status,
      error: first.error,
      stderr: first.stderr,
    }).toEqual({ status: 0, error: undefined, stderr: '' });
    expect(() => verifyAuth0Patch(fixture)).not.toThrow();
    const patched = read(nativePath);
    expect(apply().status).toBe(0);
    expect(read(nativePath)).toBe(patched);
    expect(() => verifyAuth0Patch(fixture)).not.toThrow();
    writeFileSync(
      nativePath,
      original.replace(
        'let removed = credentialsManager.clear()',
        'let removed = false',
      ),
    );
    expect(apply().status).toBe(1);
    expect(() => verifyAuth0Patch(fixture)).toThrow('missing or modified');
  } finally {
    if (!fixture.startsWith(prefix))
      throw new Error('Unexpected test fixture path.');
    rmSync(fixture, { recursive: true, force: true });
  }
}, 30_000);
