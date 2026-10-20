import type { BinaryWASIFS } from '@runno/wasi'

export const CACHE_FORMAT_VERSION = 2

interface SerializedFilesystem {
  version: number
  files: Record<string, {
    path: string
    timestamps: { access: number; modification: number; change: number }
    mode: 'binary'
    contentBase64: string
  }>
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function readTimestamp(value: unknown): Date {
  if (typeof value !== 'number' && typeof value !== 'string') {
    throw new Error('Invalid cached filesystem timestamp')
  }
  const date = new Date(value)
  if (!Number.isFinite(date.getTime())) {
    throw new Error('Invalid cached filesystem timestamp')
  }
  return date
}

export function deserializeCachedFilesystem(value: unknown): BinaryWASIFS {
  if (!isRecord(value) || value.version !== CACHE_FORMAT_VERSION || !isRecord(value.files)) {
    throw new Error('Invalid cached filesystem format')
  }
  const fs: BinaryWASIFS = {}
  for (const [path, file] of Object.entries(value.files)) {
    if (!isRecord(file) || typeof file.path !== 'string' || file.mode !== 'binary'
      || typeof file.contentBase64 !== 'string' || !isRecord(file.timestamps)) {
      throw new Error('Invalid cached filesystem file')
    }
    fs[path] = {
      path: file.path,
      timestamps: {
        access: readTimestamp(file.timestamps.access),
        modification: readTimestamp(file.timestamps.modification),
        change: readTimestamp(file.timestamps.change),
      },
      mode: file.mode,
      content: Uint8Array.from(atob(file.contentBase64), c => c.charCodeAt(0)),
    }
  }
  return fs
}

export function serializeCachedFilesystem(fs: BinaryWASIFS): SerializedFilesystem {
  const serialized: SerializedFilesystem = { version: CACHE_FORMAT_VERSION, files: {} }
  for (const [path, file] of Object.entries(fs)) {
    if (!file.content) continue
    let binary = ''
    const chunkSize = 8192
    for (let offset = 0; offset < file.content.length; offset += chunkSize) {
      binary += String.fromCharCode(...file.content.subarray(offset, offset + chunkSize))
    }
    serialized.files[path] = {
      path: file.path,
      timestamps: {
        access: file.timestamps.access.getTime(),
        modification: file.timestamps.modification.getTime(),
        change: file.timestamps.change.getTime(),
      },
      mode: file.mode,
      contentBase64: btoa(binary),
    }
  }
  return serialized
}
