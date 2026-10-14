// Keep this identity aligned with the immutable Code adapter version 1. Updating
// the compiler requires a new adapter version and retention of the old artifacts.
export const CODE_TOOLCHAIN_V1 = Object.freeze({
  artifactVersion: '4.4.0',
  runtimeAbi: 'emception-browser-v1',
  toolchainLockHash: '0fe7f8b3f9e24facd491c64c7474ea2a39b04e710664c2966e43eafec2ba2df7',
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
