import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { WatRunner } from './wat-runner'
import { createJSBindings } from './wat-env-bindings/builtin/js'
import { createObjectBindings } from './wat-env-bindings/builtin/object'
import { createReflectBindings } from './wat-env-bindings/builtin/reflect'

// Only the Wabt browser-asset boundary is replaced. Every fixture below is a
// real WASM module, instantiated by WebAssembly with the installed @runno/wasi.
type ImportedFunction = { module: string; name: string; parameters: number; result?: boolean }
type ExportedFunction = { name: string; body: number[]; result?: boolean }
const binaries = new Map<string, Uint8Array>()
const unsigned = (value: number): number[] => {
  const bytes: number[] = []
  do {
    const next = value & 127
    value >>>= 7
    bytes.push(next | (value ? 128 : 0))
  } while (value)
  return bytes
}
const integer = (value: number): number[] => {
  const bytes: number[] = []
  let more = true
  while (more) {
    const next = value & 127
    value >>= 7
    more = !((value === 0 && !(next & 64)) || (value === -1 && !!(next & 64)))
    bytes.push(next | (more ? 128 : 0))
  }
  return [0x41, ...bytes]
}
const vector = (values: number[][]) => [...unsigned(values.length), ...values.flat()]
const text = (value: string) => {
  const bytes = Array.from(new TextEncoder().encode(value))
  return [...unsigned(bytes.length), ...bytes]
}
const section = (id: number, body: number[]) => [id, ...unsigned(body.length), ...body]
const call = (index: number, ...arguments_: number[]) => [...arguments_.flatMap(integer), 0x10, ...unsigned(index)]
const store = (pointer: number, value: number) => [...integer(pointer), ...integer(value), 0x36, 2, 0]
const load = (pointer: number) => [...integer(pointer), 0x28, 2, 0]

function moduleBytes(imports: ImportedFunction[], functions: ExportedFunction[], importedMemory = false) {
  const types = [
    ...imports.map(({ parameters, result }) => [0x60, ...vector(Array.from({ length: parameters }, () => [0x7f])), ...vector(result ? [[0x7f]] : [])]),
    ...functions.map(({ result }) => [0x60, 0, ...vector(result ? [[0x7f]] : [])]),
  ]
  const entries = imports.map((entry, index) => [...text(entry.module), ...text(entry.name), 0, ...unsigned(index)])
  if (importedMemory) entries.push([...text('env'), ...text('memory'), 2, 1, ...unsigned(256), ...unsigned(512)])
  const exports = functions.map((entry, index) => [...text(entry.name), 0, ...unsigned(imports.length + index)])
  // Imported-memory fixtures deliberately omit the export to test the fallback.
  if (!importedMemory) exports.push([...text('memory'), 2, 0])
  return new Uint8Array([
    0, 97, 115, 109, 1, 0, 0, 0,
    ...section(1, vector(types)),
    ...(entries.length ? section(2, vector(entries)) : []),
    ...section(3, vector(functions.map((_, index) => unsigned(imports.length + index)))),
    ...(!importedMemory ? section(5, [1, 0, 1]) : []),
    ...section(7, vector(exports)),
    ...section(10, vector(functions.map(({ body }) => {
      const code = [0, ...body, 0x0b]
      return [...unsigned(code.length), ...code]
    }))),
  ])
}

function assertValue(body: number[], expected: number, exit: number) {
  return [...body, ...integer(expected), 0x47, 0x04, 0x40, ...call(exit, 91), 0x0b]
}

function commandBytes(namespace: string) {
  const other = namespace === 'wasi_snapshot_preview1' ? 'wasi_unstable' : 'wasi_snapshot_preview1'
  const imports: ImportedFunction[] = [
    { module: namespace, name: 'fd_read', parameters: 4, result: true },
    { module: other, name: 'fd_read', parameters: 4, result: true },
    { module: namespace, name: 'fd_write', parameters: 4, result: true },
    { module: namespace, name: 'proc_exit', parameters: 1 },
  ]
  const body: number[] = [...store(4, 1)]
  for (let index = 0; index < 7; index += 1) {
    body.push(...store(0, 128 + index), ...assertValue(call(index % 2, 0, 0, 1, 24), 0, 3), ...assertValue(load(24), 1, 3))
  }
  body.push(...assertValue(call(0, 0, 0, 1, 24), 0, 3), ...assertValue(load(24), 0, 3))
  // Native fd_read behavior for output and invalid descriptors must be retained.
  body.push(...assertValue(call(0, 1, 0, 1, 24), 58, 3), ...assertValue(call(0, 99, 0, 1, 24), 8, 3))
  body.push(...store(0, 128), ...store(4, 7), ...assertValue(call(2, 1, 0, 1, 24), 0, 3))
  body.push(...integer(512), ...integer(33), 0x3a, 0, 0, ...store(0, 512), ...store(4, 1))
  body.push(...assertValue(call(2, 2, 0, 1, 24), 0, 3), ...call(3, 7))
  return moduleBytes(imports, [{ name: '_start', body }])
}

describe('WAT JavaScript host bindings', () => {
  it('preserves array mutation, element identity and empty pop results', () => {
    const { arr } = createJSBindings()
    const reference = { label: 'preserved reference' }
    const values: (string | typeof reference)[] = ['first']

    expect(arr.length(values)).toBe(1)
    expect(arr.push(values, reference)).toBe(2)
    expect(values[1]).toBe(reference)
    expect(arr.pop(values)).toBe(reference)
    expect(arr.pop(values)).toBe('first')
    expect(arr.pop(values)).toBeUndefined()
    expect(arr.length(values)).toBe(0)
  })

  it('enumerates own string keys and retains native value references', () => {
    const symbol = Symbol('not a string key')
    const reference = { label: 'enumerated value' }
    const target = { visible: reference, [symbol]: 'symbol value' }
    Object.setPrototypeOf(target, { inherited: 'prototype value' })
    Object.defineProperty(target, 'hidden', { value: 'non-enumerable value' })

    for (const binding of [createJSBindings().obj, createObjectBindings()]) {
      expect(binding.keys(target)).toEqual(['visible'])
      expect(binding.values(target)).toEqual([reference])
      expect(binding.values(target)[0]).toBe(reference)
      expect(binding.entries(target)).toEqual([['visible', reference]])
      expect(binding.entries(target)[0]?.[1]).toBe(reference)
    }
  })

  it('retains native primitive coercion and nullish input errors', () => {
    for (const binding of [createJSBindings().obj, createObjectBindings()]) {
      expect(binding.keys('ab')).toEqual(['0', '1'])
      expect(binding.values('ab')).toEqual(['a', 'b'])
      expect(binding.entries('ab')).toEqual([['0', 'a'], ['1', 'b']])
      expect(binding.keys(17)).toEqual([])
      expect(binding.values(false)).toEqual([])
      expect(binding.entries(Symbol('primitive'))).toEqual([])
      for (const operation of [binding.keys, binding.values, binding.entries]) {
        expect(() => Reflect.apply(operation, undefined, [null])).toThrow(TypeError)
        expect(() => Reflect.apply(operation, undefined, [undefined])).toThrow(TypeError)
      }
    }
  })

  it('preserves Object.is semantics for NaN, signed zero and references', () => {
    const binding = createObjectBindings()
    const reference = { value: 1 }

    expect(binding.is(NaN, NaN)).toBe(true)
    expect(binding.is(0, -0)).toBe(false)
    expect(binding.is(reference, reference)).toBe(true)
    expect(binding.is(reference, { value: 1 })).toBe(false)
  })

  it('preserves reflective symbols, inherited properties and failed writes', () => {
    const binding = createReflectBindings()
    const symbol = Symbol('reflective key')
    const original = { label: 'original' }
    const replacement = { label: 'replacement' }
    const target = { [symbol]: original }
    Object.setPrototypeOf(target, { inherited: 'prototype value' })
    Object.defineProperty(target, 'fixed', { value: 7, writable: false })

    expect(binding.has(target, 'inherited')).toBe(true)
    expect(binding.get(target, 'inherited')).toBe('prototype value')
    expect(binding.get(target, symbol)).toBe(original)
    expect(binding.set(target, symbol, replacement)).toBe(true)
    expect(target[symbol]).toBe(replacement)
    expect(binding.set(target, 'fixed', 8)).toBe(false)
    expect(binding.get(target, 'fixed')).toBe(7)
    expect(binding.has(target, 'absent')).toBe(false)
    expect(binding.get(target, 'absent')).toBeUndefined()
    expect(() => Reflect.apply(binding.get, undefined, [17, 'value'])).toThrow(TypeError)
  })

  it('propagates getter errors from object and reflection operations', () => {
    const failure = new Error('getter failed')
    const target = { get value(): never { throw failure } }

    for (const binding of [createJSBindings().obj, createObjectBindings()]) {
      expect(binding.keys(target)).toEqual(['value'])
      expect(() => binding.values(target)).toThrow(failure)
      expect(() => binding.entries(target)).toThrow(failure)
    }
    expect(() => createReflectBindings().get(target, 'value')).toThrow(failure)
  })
})

describe('WAT runner with real WebAssembly and installed WASI', () => {
  const host = window as unknown as { WabtModule?: unknown }
  const previous = host.WabtModule
  beforeAll(() => {
    host.WabtModule = async () => ({
      parseWat: (_filename: string, source: string) => {
        const binary = binaries.get(source)
        if (!binary) throw new Error('Unregistered compiled WASM fixture')
        return { validate() {}, destroy() {}, toBinary: () => ({ buffer: binary }) }
      },
    })
  })
  afterAll(() => {
    if (previous === undefined) delete host.WabtModule
    else host.WabtModule = previous
  })

  it.each(['wasi_snapshot_preview1', 'wasi_unstable'])('preserves stdin bytes, both namespaces, native reads and exit status (%s)', async (namespace) => {
    const source = `;; compiled ${namespace} command fixture`
    binaries.set(source, commandBytes(namespace))
    expect(await new WatRunner().execute(source, 'Aé🦊')).toMatchObject({ stdout: 'Aé🦊', stderr: '!', exitCode: 7 })
  })

  it('initializes reactor memory before _initialize and preserves its main result', async () => {
    const source = ';; compiled reactor fixture'
    const body = [...store(0, 128), ...store(4, 4)]
    for (const [index, byte] of [105, 110, 105, 116].entries()) body.push(...integer(128 + index), ...integer(byte), 0x3a, 0, 0)
    body.push(...call(0, 1, 0, 1, 24), 0x1a)
    binaries.set(source, moduleBytes([
      { module: 'wasi_snapshot_preview1', name: 'fd_write', parameters: 4, result: true },
    ], [{ name: '_initialize', body }, { name: 'main', result: true, body: integer(9) }]))
    expect(await new WatRunner().execute(source)).toMatchObject({ stdout: 'init', stderr: '', exitCode: 9 })
  })

  it.each(['main', 'answer'])('retains a pure numeric %s export', async (name) => {
    const source = `;; compiled pure ${name} fixture`
    binaries.set(source, moduleBytes([], [{ name, result: true, body: integer(42) }]))
    expect(await new WatRunner().execute(source)).toMatchObject({ stdout: '42', stderr: '', exitCode: 0 })
  })

  it('retains the custom environment and imported shared memory', async () => {
    const source = ';; compiled environment fixture'
    binaries.set(source, moduleBytes([
      { module: 'env', name: 'print_i32', parameters: 1 },
    ], [{ name: 'main', body: call(0, 42) }], true))
    expect(await new WatRunner().execute(source)).toMatchObject({ stdout: '42', stderr: '', exitCode: 0 })
  })

  it('retains numeric AssemblyScript trace arguments through real WASM imports', async () => {
    const source = ';; compiled AssemblyScript trace fixture'
    binaries.set(source, moduleBytes([
      { module: 'assembly/index', name: 'trace', parameters: 4 },
    ], [{ name: 'main', body: call(0, 120, 2, 3, 4) }]))

    expect(await new WatRunner().execute(source)).toMatchObject({
      stdout: '[TRACE] 120 n=2 args=3, 4', stderr: '', exitCode: 0,
    })
  })

  it('retains AssemblyScript abort failures through real WASM imports', async () => {
    const source = ';; compiled AssemblyScript abort fixture'
    binaries.set(source, moduleBytes([
      { module: 'assembly/index', name: 'abort', parameters: 4 },
    ], [{ name: 'main', body: call(0, 120, 240, 3, 4) }]))

    expect(await new WatRunner().execute(source)).toMatchObject({
      stdout: '', stderr: 'WASM execution failed: Abort: 120', exitCode: 1,
    })
  })

  it('retains compiled dependency aliases and executeWithFiles', async () => {
    const main = ';; compiled importing fixture'
    const dependency = ';; compiled dependency fixture'
    binaries.set(main, moduleBytes([{ module: 'math', name: 'answer', parameters: 0, result: true }], [
      { name: 'main', result: true, body: call(0) },
    ]))
    binaries.set(dependency, moduleBytes([], [{ name: 'answer', result: true, body: integer(43) }]))
    expect(await new WatRunner().executeWithFiles('/main.wat', {
      '/main.wat': main, '/math.wat': dependency, '/note.txt': 'preserved source file',
    })).toMatchObject({ stdout: '43', stderr: '', exitCode: 0 })
  })
})
