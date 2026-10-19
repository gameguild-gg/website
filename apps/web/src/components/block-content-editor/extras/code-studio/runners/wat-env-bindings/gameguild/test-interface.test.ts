import { describe, expect, it, vi } from 'vitest'
import { createTest } from './test'

describe('GameGuild WAT assertion diagnostics', () => {
  const circular: { self?: unknown } = {}
  circular.self = circular

  it.each([
    { name: 'undefined', actual: undefined, text: 'undefined' },
    { name: 'null', actual: null, text: 'null' },
    { name: 'zero', actual: 0, text: '0' },
    { name: 'false', actual: false, text: 'false' },
    { name: 'NaN', actual: NaN, text: 'NaN' },
    { name: 'bigint', actual: 10n, text: '10' },
    { name: 'symbol', actual: Symbol('key'), text: 'Symbol(key)' },
    { name: 'object', actual: { answer: 42 }, text: '{"answer":42}' },
    { name: 'array', actual: [1, 2], text: '[1,2]' },
    { name: 'circular object', actual: circular, text: '[unserializable object]' },
    { name: 'empty JSON projection', actual: { toJSON: () => undefined }, text: '[object]' },
    { name: 'throwing JSON projection', actual: { toJSON: () => { throw new Error('projection') } }, text: '[unserializable object]' },
  ])('reports a failed strict comparison for $name', ({ actual, text }) => {
    const assertions = createTest(vi.fn(), vi.fn())
    expect(() => assertions.expect(actual)['to-be']('different'))
      .toThrowError('Expected different, got ' + text)
  })

  it('formats the expected operand as well as the actual operand', () => {
    const assertions = createTest(vi.fn(), vi.fn())
    expect(() => assertions.expect('actual')['to-be'](Symbol('expected')))
      .toThrowError('Expected Symbol(expected), got actual')
  })

  it.each([
    { actual: undefined, text: 'undefined' },
    { actual: null, text: 'null' },
    { actual: false, text: 'false' },
    { actual: 0, text: '0' },
    { actual: '', text: '' },
    { actual: NaN, text: 'NaN' },
  ])('retains the failed truthy assertion for $text', ({ actual, text }) => {
    const assertions = createTest(vi.fn(), vi.fn())
    expect(() => assertions.expect(actual)['to-be-truthy']())
      .toThrowError('Expected truthy value, got ' + text)
  })

  it.each([
    { actual: { value: 1 }, text: '{"value":1}' },
    { actual: [1], text: '[1]' },
    { actual: Symbol('truthy'), text: 'Symbol(truthy)' },
  ])('retains the failed falsy assertion for $text', ({ actual, text }) => {
    const assertions = createTest(vi.fn(), vi.fn())
    expect(() => assertions.expect(actual)['to-be-falsy']())
      .toThrowError('Expected falsy value, got ' + text)
  })

  it('retains passing assertions and structural equality', () => {
    const stdout = vi.fn()
    const stderr = vi.fn()
    const assertions = createTest(stdout, stderr)
    assertions.it('valid', () => {
      assertions.expect(7)['to-be'](7)
      assertions.expect({ nested: [1, 2] })['to-equal']({ nested: [1, 2] })
      assertions.expect('value')['to-be-truthy']()
      assertions.expect(null)['to-be-falsy']()
    })
    expect(stdout).toHaveBeenCalledWith('  ✓ valid')
    expect(stderr).not.toHaveBeenCalled()
  })
})
