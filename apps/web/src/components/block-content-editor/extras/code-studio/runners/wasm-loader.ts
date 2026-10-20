import { ungzip } from 'pako'
import { Archive } from '@obsidize/tar-browserify'
import type { BinaryWASIFS } from '@runno/wasi'
import { CACHE_FORMAT_VERSION, deserializeCachedFilesystem, serializeCachedFilesystem } from './wasm-cache-codec'

const wasmCache: Map<string, WebAssembly.Module> = new Map()

// IndexedDB para cache persistente de arquivos descomprimidos
const DB_NAME = 'wasm-cache'
const DB_VERSION = 2 // Incrementado para invalidar cache antigo com timestamps Date
const STORE_NAME = 'decompressed-files'

interface CachedFile {
  url: string
  data: ArrayBuffer
  timestamp: number
  version: string // Para invalidar cache quando necessário
}

let dbPromise: Promise<IDBDatabase> | null = null

async function getDB(): Promise<IDBDatabase> {
  if (dbPromise) return dbPromise

  dbPromise = new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION)

    request.onerror = () => reject(request.error)
    request.onsuccess = () => resolve(request.result)

    request.onupgradeneeded = (event) => {
      const db = (event.target as IDBOpenDBRequest).result
      if (!db.objectStoreNames.contains(STORE_NAME)) {
        db.createObjectStore(STORE_NAME, { keyPath: 'url' })
      }
    }
  })

  return dbPromise
}

async function getCachedFile(url: string): Promise<ArrayBuffer | null> {
  try {
    const db = await getDB()
    return new Promise((resolve, reject) => {
      const transaction = db.transaction(STORE_NAME, 'readonly')
      const store = transaction.objectStore(STORE_NAME)
      const request = store.get(url)

      request.onsuccess = () => {
        const cached: CachedFile | undefined = request.result
        if (cached) {
          console.log(`[WASM Cache] Loading from cache: ${url}`)
          resolve(cached.data)
        } else {
          resolve(null)
        }
      }
      request.onerror = () => reject(request.error)
    })
  } catch (error) {
    console.warn('[WASM Cache] Error reading cache:', error)
    return null
  }
}

async function setCachedFile(url: string, data: ArrayBuffer): Promise<void> {
  try {
    const db = await getDB()
    return new Promise((resolve, reject) => {
      const transaction = db.transaction(STORE_NAME, 'readwrite')
      const store = transaction.objectStore(STORE_NAME)
      
      const cached: CachedFile = {
        url,
        data,
        timestamp: Date.now(),
        version: '1.0', // Incrementar para invalidar cache
      }

      const request = store.put(cached)
      request.onsuccess = () => {
        console.log(`[WASM Cache] Saved to cache: ${url}`)
        resolve()
      }
      request.onerror = () => reject(request.error)
    })
  } catch (error) {
    console.warn('[WASM Cache] Error writing cache:', error)
  }
}

async function deleteCachedFile(url: string): Promise<void> {
  try {
    const db = await getDB()
    return new Promise((resolve, reject) => {
      const transaction = db.transaction(STORE_NAME, 'readwrite')
      const store = transaction.objectStore(STORE_NAME)
      const request = store.delete(url)
      
      request.onsuccess = () => {
        console.log(`[WASM Cache] Deleted from cache: ${url}`)
        resolve()
      }
      request.onerror = () => reject(request.error)
    })
  } catch (error) {
    console.warn('[WASM Cache] Error deleting cache:', error)
  }
}

// Callback para notificar sobre downloads
type DownloadNotificationCallback = (message: string, isDownloading: boolean) => void
let downloadNotificationCallback: DownloadNotificationCallback | null = null

export function setDownloadNotificationCallback(callback: DownloadNotificationCallback | null): void {
  downloadNotificationCallback = callback
}

// Cache para filesystem do .NET managed extraído
let managedFilesystem: Record<string, ArrayBuffer> | null = null

/**
 * Carrega e extrai o filesystem do .NET managed (managed.tar.gz)
 * Retorna um mapa de caminhos para ArrayBuffers
 */
export async function loadManagedFilesystem(): Promise<Record<string, ArrayBuffer>> {
  if (managedFilesystem) {
    return managedFilesystem
  }

  console.log('[WASM Loader] Loading .NET managed filesystem...')
  
  // Carregar e extrair managed.tar.gz
  const fs = await loadTarGz('/langs/managed.tar.gz')
  
  // Converter BinaryWASIFS para Record<string, ArrayBuffer>
  managedFilesystem = {}
  for (const [path, file] of Object.entries(fs)) {
    if (file.content) {
      managedFilesystem[path] = file.content.buffer as ArrayBuffer
    }
  }
  
  console.log(`[WASM Loader] ✓ Loaded ${Object.keys(managedFilesystem).length} .NET managed files`)
  return managedFilesystem
}

// Interceptor para fetch de arquivos .NET managed
const originalFetch = window.fetch
let managedFetchInterceptorInstalled = false
let rustFetchInterceptorInstalled = false

export function installManagedFetchInterceptor(): void {
  if (managedFetchInterceptorInstalled) {
    return
  }

  window.fetch = async function(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
    let url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url
    
    // Normalizar URL removendo origin se for localhost
    if (url.startsWith('http://') || url.startsWith('https://')) {
      try {
        const urlObj = new URL(url)
        url = urlObj.pathname + urlObj.search + urlObj.hash
      } catch (e) {
        // Keep original URL if parsing fails
      }
    }
    
    // Interceptar requisições para /managed/
    if (url.includes('/managed/')) {
      try {
        // Carregar filesystem do managed (se ainda não foi carregado)
        const filesystem = await loadManagedFilesystem()
        
        // Extrair o caminho relativo do arquivo
        const relativePath = url.replace(/^.*\/managed\//, '/')
        
        // Buscar arquivo no filesystem
        const fileBuffer = filesystem[relativePath]
        
        if (!fileBuffer) {
          console.warn(`[WASM Cache] File not found in managed filesystem: ${relativePath}`)
          // Fallback to original fetch
          return originalFetch(input, init)
        }
        
        // Determinar Content-Type
        let contentType = 'application/octet-stream'
        if (url.endsWith('.wasm')) contentType = 'application/wasm'
        else if (url.endsWith('.js')) contentType = 'application/javascript'
        else if (url.endsWith('.json')) contentType = 'application/json'
        
        return new Response(fileBuffer, {
          status: 200,
          headers: { 'Content-Type': contentType }
        })
      } catch (error) {
        console.error(`[WASM Cache] Failed to load ${url}:`, error)
        // Fallback to original fetch
        return originalFetch(input, init)
      }
    }
    
    // Para outras URLs, usar fetch original
    return originalFetch(input, init)
  }

  managedFetchInterceptorInstalled = true
  console.log('[WASM Cache] Fetch interceptor installed for /managed/ files')
}

// Cache para filesystem do Rust extraído
let rustFilesystem: Record<string, ArrayBuffer> | null = null

/**
 * Carrega e extrai o filesystem do Rust (rust.tar.gz)
 * Retorna um mapa de caminhos para ArrayBuffers
 */
export async function loadRustFilesystem(): Promise<Record<string, ArrayBuffer>> {
  if (rustFilesystem) {
    return rustFilesystem
  }

  console.log('[WASM Loader] Loading Rust filesystem...')
  
  // Carregar e extrair rust.tar.gz
  const fs = await loadTarGz('/langs/rust.tar.gz')
  
  // Converter BinaryWASIFS para Record<string, ArrayBuffer>
  rustFilesystem = {}
  for (const [path, file] of Object.entries(fs)) {
    if (file.content) {
      rustFilesystem[path] = file.content.buffer as ArrayBuffer
    }
  }
  
  console.log(`[WASM Loader] ✓ Loaded ${Object.keys(rustFilesystem).length} Rust files`)
  return rustFilesystem
}

export function installRustFetchInterceptor(): void {
  if (rustFetchInterceptorInstalled) {
    return
  }

  const currentFetch = window.fetch

  window.fetch = async function(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
    let url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url
    
    // Normalizar URL removendo origin se for localhost
    if (url.startsWith('http://') || url.startsWith('https://')) {
      try {
        const urlObj = new URL(url)
        url = urlObj.pathname + urlObj.search + urlObj.hash
      } catch (e) {
        // Keep original URL if parsing fails
      }
    }
    
    // Interceptar requisições para /rust/
    if (url.includes('/rust/')) {
      try {
        // Carregar filesystem do rust (se ainda não foi carregado)
        const filesystem = await loadRustFilesystem()
        
        // Extrair o caminho relativo do arquivo
        const relativePath = url.replace(/^.*\/rust\//, '/')
        
        // Buscar arquivo no filesystem
        const fileBuffer = filesystem[relativePath]
        
        if (!fileBuffer) {
          console.warn(`[WASM Cache] File not found in rust filesystem: ${relativePath}`)
          // Fallback to current fetch
          return currentFetch(input, init)
        }
        
        // Determinar Content-Type
        let contentType = 'application/octet-stream'
        if (url.endsWith('.wasm')) contentType = 'application/wasm'
        else if (url.endsWith('.js')) contentType = 'application/javascript'
        else if (url.endsWith('.json')) contentType = 'application/json'
        
        return new Response(fileBuffer, {
          status: 200,
          headers: { 'Content-Type': contentType }
        })
      } catch (error) {
        console.error(`[WASM Cache] Failed to load ${url}:`, error)
        // Fallback to current fetch
        return currentFetch(input, init)
      }
    }
    
    // Para outras URLs, usar fetch anterior
    return currentFetch(input, init)
  }

  rustFetchInterceptorInstalled = true
  console.log('[WASM Cache] Fetch interceptor installed for /rust/ files')
}

export async function loadCompressedWasm(path: string): Promise<ArrayBuffer> {
  // Tentar carregar do cache primeiro
  const cached = await getCachedFile(path)
  if (cached) {
    return cached
  }

  // Se não estiver em cache, notificar que vai baixar
  const fileName = path.split('/').pop() || path
  if (downloadNotificationCallback) {
    downloadNotificationCallback(`Downloading ${fileName}...`, true)
  }
  
  console.log(`[WASM Cache] Downloading and decompressing: ${path}`)
  
  try {
    const response = await fetch(path)
    if (!response.ok) {
      throw new Error(`Failed to fetch ${path}: ${response.statusText}`)
    }

    const compressed = await response.arrayBuffer()
    const decompressed = ungzip(new Uint8Array(compressed))
    const buffer = decompressed.buffer

    // Salvar no cache para uso futuro
    await setCachedFile(path, buffer)
    
    if (downloadNotificationCallback) {
      downloadNotificationCallback(`${fileName} ready`, false)
    }

    return buffer
  } catch (error) {
    if (downloadNotificationCallback) {
      downloadNotificationCallback(`Failed to download ${fileName}`, false)
    }
    throw error
  }
}

export async function loadCompressedScript(url: string): Promise<void> {
  // Tentar carregar do cache primeiro
  const cached = await getCachedFile(url)
  let decompressed: Uint8Array

  if (cached) {
    decompressed = new Uint8Array(cached)
  } else {
    // Se não estiver em cache, notificar que vai baixar
    const fileName = url.split('/').pop() || url
    if (downloadNotificationCallback) {
      downloadNotificationCallback(`Downloading ${fileName}...`, true)
    }
    
    console.log(`[WASM Cache] Downloading and decompressing script: ${url}`)
    
    try {
      const response = await fetch(url)
      if (!response.ok) {
        throw new Error(`Failed to fetch ${url}: ${response.statusText}`)
      }

      const compressed = await response.arrayBuffer()
      decompressed = ungzip(new Uint8Array(compressed))

      // Converter para ArrayBuffer antes de salvar no cache
      const buffer = new ArrayBuffer(decompressed.byteLength)
      new Uint8Array(buffer).set(decompressed)
      await setCachedFile(url, buffer)
      
      if (downloadNotificationCallback) {
        downloadNotificationCallback(`${fileName} ready`, false)
      }
    } catch (error) {
      if (downloadNotificationCallback) {
        downloadNotificationCallback(`Failed to download ${fileName}`, false)
      }
      throw error
    }
  }

  const code = new TextDecoder().decode(decompressed)

  // Execute the script in global scope
  const script = document.createElement('script')
  script.textContent = code
  document.head.appendChild(script)
}

export async function loadAndCacheWasm(
  name: string,
  path: string
): Promise<WebAssembly.Module> {
  if (wasmCache.has(name)) {
    return wasmCache.get(name)!
  }

  const buffer = await loadCompressedWasm(path)
  const module = await WebAssembly.compile(buffer)
  wasmCache.set(name, module)

  return module
}

// Função auxiliar para limpar cache antigo (opcional)
export async function clearWasmCache(): Promise<void> {
  try {
    const db = await getDB()
    return new Promise((resolve, reject) => {
      const transaction = db.transaction(STORE_NAME, 'readwrite')
      const store = transaction.objectStore(STORE_NAME)
      const request = store.clear()
      
      request.onsuccess = () => {
        console.log('[WASM Cache] Cache cleared')
        resolve()
      }
      request.onerror = () => reject(request.error)
    })
  } catch (error) {
    console.warn('[WASM Cache] Error clearing cache:', error)
  }
}

// Função para carregar e extrair tar.gz com cache
export async function loadTarGz(path: string): Promise<BinaryWASIFS> {
  // Tentar carregar do cache primeiro
  const cached = await getCachedFile(path)
  
  if (cached) {
    try {
      // Deserializar o filesystem do cache
      const text = new TextDecoder().decode(cached)
      const parsed: unknown = JSON.parse(text)
      
      // Verificar versão do cache
      if (typeof parsed !== 'object' || parsed === null || !('version' in parsed) || parsed.version !== CACHE_FORMAT_VERSION) {
        console.log(`[WASM Cache] Invalidating old cache format for: ${path}`)
        await deleteCachedFile(path)
        // Continuar para baixar novamente
      } else {
        const fs = deserializeCachedFilesystem(parsed)
        
        console.log(`[WASM Cache] Loaded tar.gz from cache: ${path}`)
        return fs
      }
    } catch (e) {
      console.warn(`[WASM Cache] Failed to load from cache, re-downloading: ${path}`, e)
      await deleteCachedFile(path)
      // Continuar para baixar novamente
    }
  }

  // Se não estiver em cache, baixar e processar
  const fileName = path.split('/').pop() || path
  if (downloadNotificationCallback) {
    downloadNotificationCallback(`Downloading ${fileName}...`, true)
  }
  
  console.log(`[WASM Cache] Downloading tar.gz: ${path}`)
  
  try {
    const response = await fetch(path)
    if (!response.ok) {
      throw new Error(`Failed to fetch ${path}: ${response.statusText}`)
    }

    const tarGzBuffer = await response.arrayBuffer()
    
    // Descomprimir gzip
    let inflatedBinary: Uint8Array
    try {
      inflatedBinary = ungzip(new Uint8Array(tarGzBuffer))
    } catch (e) {
      inflatedBinary = new Uint8Array(tarGzBuffer)
    }

    // Extrair tar (Archive.extract é assíncrono no tar-browserify v6)
    const archive = await Archive.extract(inflatedBinary)
    const entries = archive.entries

    const fs: BinaryWASIFS = {}
    for (const entry of entries) {
      if (!entry.isFile()) {
        continue
      }

      // Garantir que cada arquivo comece com /
      const name = entry.fileName.replace(/^([^/])/, '/$1')
      const timestamp = new Date(entry.lastModified)
      fs[name] = {
        path: name,
        timestamps: {
          change: timestamp,
          access: timestamp,
          modification: timestamp,
        },
        mode: 'binary',
        content: entry.content!,
      }
    }

    // Serializar para cache (converter Uint8Array para base64)
    const serializable = serializeCachedFilesystem(fs)
    
    const serialized = JSON.stringify(serializable)
    const buffer = new TextEncoder().encode(serialized).buffer
    
    // Salvar no cache
    await setCachedFile(path, buffer)
    
    if (downloadNotificationCallback) {
      downloadNotificationCallback(`${fileName} ready`, false)
    }

    console.log(`[WASM Cache] Extracted tar.gz: ${path}`)
    return fs
  } catch (error) {
    if (downloadNotificationCallback) {
      downloadNotificationCallback(`Failed to download ${fileName}`, false)
    }
    throw error
  }
}
