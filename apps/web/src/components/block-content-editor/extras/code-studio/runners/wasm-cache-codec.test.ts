import { describe, expect, it } from 'vitest'
import type { BinaryWASIFS } from '@runno/wasi'
import { deserializeCachedFilesystem, serializeCachedFilesystem } from './wasm-cache-codec'

function cachedFile() {
  return {
    path: '/module.wasm',
    timestamps: { access: 0, modification: -1, change: 1700000000123 },
    mode: 'binary',
    contentBase64: 'AP+A',
  }
}

function filesystem(content = Uint8Array.from([0, 255, 128])): BinaryWASIFS {
  return {
    '/module.wasm': {
      path: '/module.wasm',
      timestamps: {
        access: new Date('1970-01-01T00:00:00.000Z'),
        modification: new Date('1969-12-31T23:59:59.999Z'),
        change: new Date('2023-11-14T22:13:20.123Z'),
      },
      mode: 'binary',
      content,
    },
  }
}

describe('WASM filesystem cache format', () => {
  it('reads numeric timestamps without replacing epoch or negative dates', () => {
    const result = deserializeCachedFilesystem({ version: 2, files: { '/module.wasm': cachedFile() } })
    expect(result).toEqual(filesystem())
  })

  it('retains ISO timestamps written by the previous version-two serializer', () => {
    const legacy: unknown = JSON.parse(JSON.stringify({ version: 2, files: {
      '/module.wasm': { ...filesystem()['/module.wasm'], content: undefined, contentBase64: 'AP+A' },
    } }))
    expect(deserializeCachedFilesystem(legacy)).toEqual(filesystem())
  })

  it('writes version-two milliseconds and exact base64 bytes', () => {
    expect(serializeCachedFilesystem(filesystem())).toEqual({
      version: 2,
      files: { '/module.wasm': cachedFile() },
    })
  })

  it('retains empty files and all distinct timestamps through JSON storage', () => {
    const fs = filesystem(new Uint8Array())
    const serialized = serializeCachedFilesystem(fs)
    const stored: unknown = JSON.parse(JSON.stringify(serialized))
    expect(serialized.files['/module.wasm']?.contentBase64).toBe('')
    expect(deserializeCachedFilesystem(stored)).toEqual(fs)
  })

  it('preserves bytes across multiple base64 conversion chunks', () => {
    const bytes = Uint8Array.from({ length: 20001 }, (_, index) => index % 256)
    const stored = serializeCachedFilesystem(filesystem(bytes))
    expect(stored.files['/module.wasm']?.contentBase64.startsWith('AAECAwQFBgcICQ')).toBe(true)
    expect(deserializeCachedFilesystem(JSON.parse(JSON.stringify(stored)))['/module.wasm']?.content).toEqual(bytes)
  })

  it('keeps the filesystem key and the stored file path unchanged', () => {
    const file = { ...cachedFile(), path: '/stored-name.wasm' }
    const result = deserializeCachedFilesystem({ version: 2, files: { '/lookup-name.wasm': file } })
    expect(Object.keys(result)).toEqual(['/lookup-name.wasm'])
    expect(result['/lookup-name.wasm']?.path).toBe('/stored-name.wasm')
  })

  it.each([
    { label: 'null', value: null },
    { label: 'array', value: [] },
    { label: 'old version', value: { version: 1, files: {} } },
    { label: 'missing files', value: { version: 2 } },
    { label: 'array of files', value: { version: 2, files: [] } },
  ])('rejects an invalid $label envelope so the loader can re-download', ({ value }) => {
    expect(() => deserializeCachedFilesystem(value)).toThrow('Invalid cached filesystem format')
  })

  it.each([
    { label: 'null', value: null },
    { label: 'missing path', value: { ...cachedFile(), path: undefined } },
    { label: 'non-binary mode', value: { ...cachedFile(), mode: 'string' } },
    { label: 'non-string base64', value: { ...cachedFile(), contentBase64: 12 } },
    { label: 'missing timestamps', value: { ...cachedFile(), timestamps: undefined } },
  ])('rejects an invalid $label file entry', ({ value }) => {
    expect(() => deserializeCachedFilesystem({ version: 2, files: { '/module.wasm': value } }))
      .toThrow('Invalid cached filesystem file')
  })

  it.each([
    { label: 'missing', value: undefined },
    { label: 'null', value: null },
    { label: 'boolean', value: false },
    { label: 'invalid date text', value: 'not-a-date' },
    { label: 'infinite', value: Infinity },
  ])('rejects a $label timestamp instead of inventing the current time', ({ value }) => {
    const file = { ...cachedFile(), timestamps: { ...cachedFile().timestamps, access: value } }
    expect(() => deserializeCachedFilesystem({ version: 2, files: { '/module.wasm': file } }))
      .toThrow('Invalid cached filesystem timestamp')
  })

  it('rejects malformed base64 so corrupt data is not returned as a filesystem', () => {
    const file = { ...cachedFile(), contentBase64: '!' }
    expect(() => deserializeCachedFilesystem({ version: 2, files: { '/module.wasm': file } })).toThrow()
  })
})
