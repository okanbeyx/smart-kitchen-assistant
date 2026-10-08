const { createHash } = require('node:crypto');
const { readFileSync } = require('node:fs');
const { join, resolve } = require('node:path');

const sdkVersion = '5.11.1';
const nativeHash =
  '4db54a0c08c6cdcf4d0505a5db8c584434430a80f99e2514744ef9ba976eca95';
const patchHash =
  '774bfa3c353525a1746de98979544ef7bd6115c53574c1370483c910ea8ee11a';

function hash(text) {
  // Git checkout line endings must not change the result on Windows.
  return createHash('sha256').update(text.replace(/\r\n/g, '\n')).digest('hex');
}

function verifyContents({
  appPackage,
  sdkPackage,
  podspec,
  nativeSource,
  patch,
}) {
  if (
    appPackage.dependencies?.['react-native-auth0'] !== sdkVersion ||
    sdkPackage.version !== sdkVersion
  ) {
    throw new Error(
      'Auth0 cleanup patch requires exactly react-native-auth0 5.11.1.',
    );
  }
  if (
    !/^\s*s\.dependency 'Auth0', '2\.25\.0'\s*$/m.test(podspec) ||
    !/^\s*s\.dependency 'SimpleKeychain', '1\.3\.0'\s*$/m.test(podspec)
  ) {
    throw new Error('Auth0 cleanup patch native dependency versions changed.');
  }
  if (hash(patch) !== patchHash) {
    throw new Error(
      'Auth0 cleanup patch file differs from the reviewed patch.',
    );
  }
  if (hash(nativeSource) !== nativeHash) {
    throw new Error(
      'Auth0 native cleanup patch is missing or modified. Run npm run postinstall.',
    );
  }
}

function verifyAuth0Patch(projectRoot = resolve(__dirname, '..')) {
  const sdkRoot = join(projectRoot, 'node_modules', 'react-native-auth0');
  const read = (file) => readFileSync(file, 'utf8');
  verifyContents({
    appPackage: JSON.parse(read(join(projectRoot, 'package.json'))),
    sdkPackage: JSON.parse(read(join(sdkRoot, 'package.json'))),
    podspec: read(join(sdkRoot, 'A0Auth0.podspec')),
    nativeSource: read(join(sdkRoot, 'ios', 'NativeBridge.swift')),
    patch: read(
      join(projectRoot, 'patches', `react-native-auth0+${sdkVersion}.patch`),
    ),
  });
}

if (require.main === module) {
  try {
    verifyAuth0Patch();
    console.log('Auth0 5.11.1 iOS cleanup patch verified.');
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}

module.exports = { verifyAuth0Patch, verifyContents };
