import { describe, expect, it, vi } from 'vitest'
import { deepEqual } from './utils'

describe('test runner deep equality', () => {
  it.each([
    { label: 'equal primitives', a: 3, b: 3, expected: true },
    { label: 'distinct primitives', a: 3, b: 4, expected: false },
    { label: 'matching nested arrays', a: [1, { x: [2] }], b: [1, { x: [2] }], expected: true },
    { label: 'different array lengths', a: [1], b: [1, 2], expected: false },
    { label: 'an empty array against an empty object', a: [], b: {}, expected: false },
    { label: 'an empty object against an empty array', a: {}, b: [], expected: false },
    { label: 'an array against numeric object keys', a: [1], b: { 0: 1 }, expected: false },
    { label: 'different object keys', a: { x: 1 }, b: { y: 1 }, expected: false },
    { label: 'matching keys in different insertion orders', a: { a: 1, b: 2 }, b: { b: 2, a: 1 }, expected: true },
    { label: 'different nested values', a: { item: [1, 2] }, b: { item: [1, 3] }, expected: false },
    { label: 'null against an object', a: null, b: {}, expected: false },
    { label: 'the existing strict signed-zero behavior', a: 0, b: -0, expected: true },
    { label: 'the existing strict NaN behavior', a: NaN, b: NaN, expected: false },
    { label: 'undefined against an absent property', a: { a: undefined }, b: {}, expected: false },
  ])('compares $label', ({ a, b, expected }) => {
    expect(deepEqual(a, b)).toBe(expected)
  })

  it('compares wide objects independently of key insertion order', () => {
    const entries = Array.from({ length: 512 }, (_, index) => [`key-${index}`, index] as const)
    const a = Object.fromEntries(entries)
    const b = Object.fromEntries([...entries].reverse())

    expect(deepEqual(a, b)).toBe(true)
    expect(deepEqual(a, { ...b, 'key-511': -1 })).toBe(false)
  })

  it('requires matching enumerable keys when a matching property is non-enumerable', () => {
    const b = Object.defineProperty({ other: 1 }, 'value', { value: 1, enumerable: false })
    expect(deepEqual({ value: 1 }, b)).toBe(false)
  })

  it('reads a compared getter once and preserves its original error', () => {
    const original = new Error('getter failed')
    const get = vi.fn(() => { throw original })
    const b = Object.defineProperty({}, 'value', { enumerable: true, get })
    let caught: unknown

    try {
      deepEqual({ value: 1 }, b)
    } catch (error) {
      caught = error
    }

    expect(get).toHaveBeenCalledOnce()
    expect(caught).toBe(original)
  })
})
