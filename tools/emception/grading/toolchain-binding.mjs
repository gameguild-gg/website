// Each adapter version retains its exact executable identity. Updating the compiler
// requires a new adapter and separately retained historical runtime/CDN artifacts.
export const CODE_TOOLCHAIN_V1 = Object.freeze({
  artifactVersion: '4.4.0',
  runtimeAbi: 'emception-browser-v1',
  toolchainLockHash: 'eb7e07aacb067ae32910fbebcb3762902da756b1a7ce64746f14f77a25057720',
});

export const CODE_TOOLCHAIN_V2 = Object.freeze({
  artifactVersion: '4.4.1',
  runtimeAbi: 'emception-browser-v1',
  toolchainLockHash: 'b6ff526fa5c050d061b5f20a14cc1aec4c88cd266732162ac4a49987954b61d9',
});

export function requireFrozenCodeToolchain(binding, manifest) {
  const names = Object.keys(CODE_TOOLCHAIN_V1);
  if (!binding || Object.keys(binding).length !== names.length ||
      names.some((name) => !Object.hasOwn(binding, name)) ||
      ![CODE_TOOLCHAIN_V1, CODE_TOOLCHAIN_V2].some((identity) =>
        names.every((name) => binding[name] === identity[name]))) {
    throw new Error('Unsupported frozen Code toolchain identity.');
  }
  if (manifest?.schemaVersion !== 2 ||
      names.some((name) => manifest[name] !== binding[name]) ||
      !/^[a-f0-9]{64}$/.test(manifest.buildReceiptHash ?? '') ||
      !/^[a-f0-9]{64}$/.test(manifest.buildFingerprint ?? '')) {
    throw new Error('Code grading requires verified artifacts matching its frozen toolchain.');
  }
}
