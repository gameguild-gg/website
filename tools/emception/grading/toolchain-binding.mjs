// Keep this identity aligned with the immutable Code adapter version 1. Updating
// the compiler requires a new adapter version and retention of the old artifacts.
export const CODE_TOOLCHAIN_V1 = Object.freeze({
  artifactVersion: '4.4.0',
  runtimeAbi: 'emception-browser-v1',
  toolchainLockHash: 'af57ce823121b58cb48fa97a265e1da7ea9dce376cb332e36666fef5826a73ef',
});

export function requireFrozenCodeToolchain(binding, manifest) {
  const names = Object.keys(CODE_TOOLCHAIN_V1);
  if (!binding || Object.keys(binding).length !== names.length ||
      names.some((name) => binding[name] !== CODE_TOOLCHAIN_V1[name])) {
    throw new Error('Unsupported frozen Code toolchain identity.');
  }
  if (manifest?.schemaVersion !== 2 ||
      names.some((name) => manifest[name] !== binding[name]) ||
      !/^[a-f0-9]{64}$/.test(manifest.buildReceiptHash ?? '') ||
      !/^[a-f0-9]{64}$/.test(manifest.buildFingerprint ?? '')) {
    throw new Error('Code grading requires verified artifacts matching its frozen toolchain.');
  }
}
