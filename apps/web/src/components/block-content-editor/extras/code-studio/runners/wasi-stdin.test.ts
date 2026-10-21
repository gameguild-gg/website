import { describe, expect, it } from 'vitest'
import { createWasiStdinReader } from './wasi-stdin'

function vector(memory: WebAssembly.Memory, index: number, destination: number, length: number) {
  const view = new DataView(memory.buffer)
  view.setUint32(index * 8, destination, true)
  view.setUint32(index * 8 + 4, length, true)
}

describe('WASI stdin byte reads', () => {
  it('fills multiple iovecs and reports the total byte count', () => {
    const memory = new WebAssembly.Memory({ initial: 1 })
    const read = createWasiStdinReader('hello', () => memory)
    vector(memory, 0, 128, 2)
    vector(memory, 1, 256, 3)
    expect(read(0, 2, 24)).toBe(0)
    expect(Array.from(new Uint8Array(memory.buffer, 128, 2))).toEqual([104, 101])
    expect(Array.from(new Uint8Array(memory.buffer, 256, 3))).toEqual([108, 108, 111])
    expect(new DataView(memory.buffer).getUint32(24, true)).toBe(5)
  })

  it('preserves UTF-8 when repeated reads split individual characters and reach EOF', () => {
    const memory = new WebAssembly.Memory({ initial: 1 })
    const read = createWasiStdinReader('Aé🦊', () => memory)
    vector(memory, 0, 128, 1)
    const actual: number[] = []
    for (let index = 0; index < 7; index += 1) {
      expect(read(0, 1, 24)).toBe(0)
      expect(new DataView(memory.buffer).getUint32(24, true)).toBe(1)
      actual.push(new Uint8Array(memory.buffer)[128]!)
    }
    expect(actual).toEqual([65, 195, 169, 240, 159, 166, 138])
    for (let index = 0; index < 2; index += 1) {
      expect(read(0, 1, 24)).toBe(0)
      expect(new DataView(memory.buffer).getUint32(24, true)).toBe(0)
    }
  })

  it('does not consume input for empty vectors or a zero-length destination', () => {
    const memory = new WebAssembly.Memory({ initial: 1 })
    const read = createWasiStdinReader('A', () => memory)
    expect(read(0, 0, 24)).toBe(0)
    expect(new DataView(memory.buffer).getUint32(24, true)).toBe(0)
    vector(memory, 0, 128, 0)
    expect(read(0, 1, 24)).toBe(0)
    vector(memory, 0, 128, 1)
    expect(read(0, 1, 24)).toBe(0)
    expect(new Uint8Array(memory.buffer)[128]).toBe(65)
  })

  it.each([
    [65533, 1, 24], [0, 1, 65535], [-1, 1, 24], [0, -1, 24], [0, 1, -1],
  ])('rejects invalid pointers/counts without consuming input (%s, %s, %s)', (pointer, count, result) => {
    const memory = new WebAssembly.Memory({ initial: 1 })
    const read = createWasiStdinReader('A', () => memory)
    vector(memory, 0, 128, 1)
    expect(read(pointer, count, result)).toBe(21)
    expect(new Uint8Array(memory.buffer)[128]).toBe(0)
    expect(read(0, 1, 24)).toBe(0)
    expect(new Uint8Array(memory.buffer)[128]).toBe(65)
  })

  it('validates all destinations before any writes or input consumption', () => {
    const memory = new WebAssembly.Memory({ initial: 1 })
    const read = createWasiStdinReader('abc', () => memory)
    vector(memory, 0, 128, 1)
    vector(memory, 1, 65535, 2)
    expect(read(0, 2, 24)).toBe(21)
    expect(new Uint8Array(memory.buffer)[128]).toBe(0)
    vector(memory, 0, 128, 3)
    expect(read(0, 1, 24)).toBe(0)
    expect(Array.from(new Uint8Array(memory.buffer, 128, 3))).toEqual([97, 98, 99])
  })

  it('snapshots descriptors when the first payload overlaps the next descriptor', () => {
    const memory = new WebAssembly.Memory({ initial: 1 })
    const read = createWasiStdinReader('12345678Z', () => memory)
    vector(memory, 0, 8, 8)
    vector(memory, 1, 128, 1)
    expect(read(0, 2, 24)).toBe(0)
    expect(Array.from(new Uint8Array(memory.buffer, 8, 8))).toEqual([49, 50, 51, 52, 53, 54, 55, 56])
    expect(new Uint8Array(memory.buffer)[128]).toBe(90)
    expect(new DataView(memory.buffer).getUint32(24, true)).toBe(9)
  })

  it('uses the current memory buffer after the guest grows memory', () => {
    const memory = new WebAssembly.Memory({ initial: 1, maximum: 2 })
    const read = createWasiStdinReader('AB', () => memory)
    vector(memory, 0, 128, 1)
    expect(read(0, 1, 24)).toBe(0)
    expect(new Uint8Array(memory.buffer)[128]).toBe(65)
    memory.grow(1)
    vector(memory, 0, 65536, 1)
    expect(read(0, 1, 24)).toBe(0)
    expect(new Uint8Array(memory.buffer)[65536]).toBe(66)
    expect(new DataView(memory.buffer).getUint32(24, true)).toBe(1)
  })

  it('returns EOF for an empty input and still validates guest addresses', () => {
    const memory = new WebAssembly.Memory({ initial: 1 })
    const read = createWasiStdinReader('', () => memory)
    vector(memory, 0, 128, 1)
    expect(read(0, 1, 24)).toBe(0)
    expect(new DataView(memory.buffer).getUint32(24, true)).toBe(0)
    vector(memory, 0, 65535, 2)
    expect(read(0, 1, 24)).toBe(21)
  })
})
