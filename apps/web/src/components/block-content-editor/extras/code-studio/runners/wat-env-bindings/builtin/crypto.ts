/**
 * Crypto bindings (crypto namespace)
 */
export function createCryptoBindings() {
  return {
    getRandomValues: (arrayPtr: number, length: number, memory: WebAssembly.Memory) => {
      const array = new Uint8Array(memory.buffer, arrayPtr, length)
      crypto.getRandomValues(array)
    },

    getRandomValuesN: (n: number) => {
      const array = new Uint8Array(n)
      crypto.getRandomValues(array)
      return array
    },
  }
}
