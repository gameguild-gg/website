// Snapshot preview1 errno values: SUCCESS and EFAULT.
const SUCCESS = 0
const BAD_ADDRESS = 21

/** Preserve UTF-8 bytes across WASI reads, including reads smaller than a character. */
export function createWasiStdinReader(input: string, getMemory: () => WebAssembly.Memory) {
  const bytes = new TextEncoder().encode(input)
  let cursor = 0

  return (iovsPointer: number, iovsLength: number, resultPointer: number): number => {
    // WASM i32 arguments may arrive as signed JavaScript numbers.
    const pointer = iovsPointer >>> 0
    const count = iovsLength >>> 0
    const result = resultPointer >>> 0
    const buffer = getMemory().buffer
    const inBounds = (offset: number, length: number) =>
      offset <= buffer.byteLength && length <= buffer.byteLength - offset
    if (!inBounds(result, 4) || !inBounds(pointer, count * 8)) return BAD_ADDRESS

    // Guest output may overlap its iovec table. Snapshot the descriptors before
    // any writes and validate every destination before consuming any input.
    const table = new DataView(new Uint8Array(buffer, pointer, count * 8).slice().buffer)
    for (let index = 0; index < count; index += 1) {
      if (!inBounds(table.getUint32(index * 8, true), table.getUint32(index * 8 + 4, true))) {
        return BAD_ADDRESS
      }
    }

    let read = 0
    for (let index = 0; index < count && cursor < bytes.length; index += 1) {
      const destination = table.getUint32(index * 8, true)
      const length = Math.min(table.getUint32(index * 8 + 4, true), bytes.length - cursor)
      new Uint8Array(buffer, destination, length).set(bytes.subarray(cursor, cursor + length))
      cursor += length
      read += length
    }
    new DataView(buffer).setUint32(result, read, true)
    return SUCCESS
  }
}
