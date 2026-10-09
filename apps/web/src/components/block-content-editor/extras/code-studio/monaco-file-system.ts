"use client"

import type { CodeFile } from "./types"
import type { RegisteredFileSystemProvider as MonacoFileSystemProvider } from '@codingame/monaco-vscode-files-service-override'
import { URI } from 'vscode-uri'
import { getTypeScriptSupport, type MonacoRuntime as Monaco } from './monaco-runtime'
import type { editor, languages, Position } from "monaco-editor"

let fileSystemProvider: MonacoFileSystemProvider | null = null
let disposable: { dispose: () => void } | null = null
let isInitialized = false
let currentFiles: CodeFile[] = []
let completionDisposables: Array<{ dispose: () => void }> = []
let monacoInstance: Monaco | null = null
let fsConsumerCount = 0

export function setMonacoInstance(monaco: Monaco) {
  monacoInstance = monaco
}

export async function initializeMonacoFileSystem() {
  if (typeof window === "undefined") return null

  fsConsumerCount++
  if (isInitialized) return fileSystemProvider

  try {
    const { registerFileSystemOverlay, RegisteredFileSystemProvider } = await import(
      "@codingame/monaco-vscode-files-service-override"
    )

    // Criar o provider em memória
    fileSystemProvider = new RegisteredFileSystemProvider(false) // false = read-write
    
    // Registrar o overlay (retorna IDisposable para cleanup)
    disposable = registerFileSystemOverlay(1, fileSystemProvider)
    
    isInitialized = true
    console.log('[Monaco FS] File system initialized')
    return fileSystemProvider
  } catch (error) {
    console.error('[Monaco FS] Failed to initialize:', error)
    return null
  }
}

export async function syncFilesToMonacoFS(files: CodeFile[], instanceId?: string) {
  if (!fileSystemProvider) {
    await initializeMonacoFileSystem()
  }

  if (!fileSystemProvider) {
    return
  }

  // Guardar referência dos arquivos para o completion provider
  currentFiles = files

  try {
    // Coletar todos os diretórios únicos
    const directories = new Set<string>()
    files.forEach(file => {
      const parts = file.path.split('/')
      if (parts.length > 1) {
        let currentPath = ''
        for (let i = 0; i < parts.length - 1; i++) {
          currentPath += (i > 0 ? '/' : '') + parts[i]
          if (currentPath) {
            directories.add(currentPath)
          }
        }
      }
    })

    // Criar diretórios
    for (const dir of Array.from(directories).sort()) {
      try {
        const fullPath = instanceId ? `/${instanceId}/${dir}` : `/${dir}`
        const dirUri = URI.file(fullPath)
        fileSystemProvider.mkdirSync(dirUri)
      } catch {
        // Diretório já existe
      }
    }

    // Criar/atualizar cada arquivo no sistema virtual
    for (const file of files) {
      try {
        const fullPath = instanceId ? `/${instanceId}/${file.path}` : `/${file.path}`
        const uri = URI.file(fullPath)
        
        await fileSystemProvider.writeFile(
          uri,
          new TextEncoder().encode(file.content),
          { create: true, overwrite: true, unlock: false, atomic: false }
        )
      } catch {
        // Ignorar erros individuais de arquivos
      }
    }
    
    // Se Monaco está disponível, adicionar arquivos como extra libs para TypeScript
    const ts = getTypeScriptSupport(monacoInstance)
    if (ts) {
      files.forEach(file => {
        if (file.language === 'typescript' || file.language === 'javascript') {
          const fullPath = instanceId ? `file:///${instanceId}/${file.path}` : `file:///${file.path}`
          
          // Adicionar como lib extra para o TypeScript worker reconhecer
          ts.typescriptDefaults.addExtraLib(
            file.content,
            fullPath
          )
          ts.javascriptDefaults.addExtraLib(
            file.content,
            fullPath
          )
        }
      })
    }
  } catch {
    // Silenciar erros - não são críticos para a funcionalidade
  }
}

export async function updateMonacoFile(filePath: string, content: string, instanceId?: string) {
  if (!fileSystemProvider) {
    return
  }

  try {
    const fullPath = instanceId ? `/${instanceId}/${filePath}` : `/${filePath}`
    const uri = URI.file(fullPath)
    
    // Criar diretórios se necessário
    const parts = filePath.split('/')
    if (parts.length > 1) {
      let currentPath = ''
      for (let i = 0; i < parts.length - 1; i++) {
        currentPath += (i > 0 ? '/' : '') + parts[i]
        if (currentPath) {
          try {
            const dirFullPath = instanceId ? `/${instanceId}/${currentPath}` : `/${currentPath}`
            const dirUri = URI.file(dirFullPath)
            fileSystemProvider.mkdirSync(dirUri)
          } catch {
            // Diretório já existe ou erro, continuar
          }
        }
      }
    }
    
    await fileSystemProvider.writeFile(
      uri,
      new TextEncoder().encode(content),
      { create: true, overwrite: true, unlock: false, atomic: false }
    )
    
    // Atualizar extraLib se Monaco estiver disponível
    const tsUpdate = getTypeScriptSupport(monacoInstance)
    if (tsUpdate) {
      const fileExt = filePath.split('.').pop()
      if (fileExt === 'ts' || fileExt === 'tsx' || fileExt === 'js' || fileExt === 'jsx') {
        const libPath = instanceId ? `file:///${instanceId}/${filePath}` : `file:///${filePath}`
        tsUpdate.typescriptDefaults.addExtraLib(content, libPath)
        tsUpdate.javascriptDefaults.addExtraLib(content, libPath)
      }
    }
  } catch {
    // Silenciar erros de file system - não são críticos
  }
}

export async function deleteMonacoFile(filePath: string) {
  if (!fileSystemProvider) return

  try {
    const uri = URI.file(`/${filePath}`)
    await fileSystemProvider.delete(uri)
  } catch {
    // Arquivo pode não existir, ignorar
  }
}

export function createMonacoDirectory(dirPath: string) {
  if (!fileSystemProvider) return

  try {
    const uri = URI.file(`/${dirPath}`)
    // RegisteredFileSystemProvider usa mkdirSync ao invés de mkdir com parâmetro
    fileSystemProvider.mkdirSync(uri)
  } catch {
    // Diretório pode já existir, ignorar
  }
}

export function getFileSystemProvider() {
  return fileSystemProvider
}

export function disposeMonacoFileSystem() {
  fsConsumerCount--
  if (fsConsumerCount > 0) {
    console.log('[Monaco FS] Skipping dispose, still', fsConsumerCount, 'consumer(s)')
    return
  }
  if (disposable) {
    disposable.dispose()
    disposable = null
  }
  
  // Dispose completion providers
  completionDisposables.forEach(d => d.dispose())
  completionDisposables = []
  
  fileSystemProvider = null
  isInitialized = false
  currentFiles = []
  console.log('[Monaco FS] File system disposed')
}

export function registerPathCompletionProvider(monaco: Monaco) {
  // Salvar instância do Monaco
  monacoInstance = monaco
  
  // Dispose previous providers
  completionDisposables.forEach(d => d.dispose())
  completionDisposables = []

  // Provider combinado para TypeScript e JavaScript
  const createProvider = () => ({
    triggerCharacters: ['"', "'", '/', '.'],
    provideCompletionItems: (model: editor.ITextModel, position: Position) => {
      const lineContent = model.getLineContent(position.lineNumber)
      const textBeforeCursor = lineContent.substring(0, position.column - 1)
      
      // Detectar se estamos dentro de um import/require/from
      const importMatch = textBeforeCursor.match(/(?:import.*?from\s*|require\s*\()\s*['"]([^'"]*?)$/)
      if (!importMatch || !importMatch[1]) return { suggestions: [] }
      
      const currentPath = importMatch[1] || ''
      const modelPath = model.uri.path || ''
      const currentDir = modelPath.split('/').slice(0, -1).join('/')
      
      const suggestions: languages.CompletionItem[] = []

      // Sugerir arquivos disponíveis
      currentFiles.forEach(file => {
        const filePath = `/${file.path}`
        const fileName = file.path.split('/').pop() || ''

        // Não sugerir o próprio arquivo
        if (filePath === modelPath) return

        // Calcular caminho relativo
        let relativePath = ''

        if (currentPath.startsWith('./') || currentPath.startsWith('../') || currentPath.length === 0) {
          // Path relativo ou vazio - sugerir arquivos do mesmo diretário
          const fileDir = filePath.split('/').slice(0, -1).join('/')

          if (fileDir === currentDir) {
            relativePath = './' + fileName
          } else {
            // Calcular caminho relativo entre diretórios
            const currentParts = currentDir.split('/').filter(Boolean)
            const fileParts = fileDir.split('/').filter(Boolean)

            let commonLength = 0
            while (commonLength < currentParts.length &&
                   commonLength < fileParts.length &&
                   currentParts[commonLength] === fileParts[commonLength]) {
              commonLength++
            }

            const upCount = currentParts.length - commonLength
            const downPath = fileParts.slice(commonLength)

            if (upCount === 0) {
              relativePath = './' + downPath.concat([fileName]).join('/')
            } else {
              const upPart = Array(upCount).fill('..').join('/')
              relativePath = upPart + (downPath.length > 0 ? '/' + downPath.join('/') : '') + '/' + fileName
            }
          }
        }

        if (relativePath) {
          suggestions.push({
            label: relativePath,
            kind: monaco.languages.CompletionItemKind.File,
            insertText: relativePath,
            range: new monaco.Range(
              position.lineNumber,
              position.column - currentPath.length,
              position.lineNumber,
              position.column,
            ),
            detail: `${file.language} file`,
            documentation: `Import from ${file.path}`,
            sortText: `0_${relativePath}`, // Priorizar na lista
          })
        }
      })

      return { suggestions }
    },
  })

  // Provider para Python
  const createPythonProvider = () => ({
    triggerCharacters: ['"', "'", '/', '.'],
    provideCompletionItems: (model: editor.ITextModel, position: Position) => {
      const lineContent = model.getLineContent(position.lineNumber)
      const textBeforeCursor = lineContent.substring(0, position.column - 1)
      
      // Detectar imports Python: from X import, import X
      const importMatch = textBeforeCursor.match(/(?:from\s+|import\s+)([^\s'"]*?)$/)
      if (!importMatch || !importMatch[1]) return { suggestions: [] }
      
      const currentPath = importMatch[1] || ''
      const modelPath = model.uri.path || ''
      const currentDir = modelPath.split('/').slice(0, -1).join('/')
      
      const suggestions: languages.CompletionItem[] = []
      
      // Sugerir arquivos Python disponíveis
      currentFiles.forEach(file => {
        if (file.language !== 'python') return
        
        const filePath = `/${file.path}`
        const fileName = file.path.split('/').pop() || ''
        const moduleName = fileName.replace(/\.py$/, '')
        
        // Não sugerir o próprio arquivo
        if (filePath === modelPath) return
        
        const fileDir = filePath.split('/').slice(0, -1).join('/')
        
        // Se está no mesmo diretório, sugerir como módulo direto
        if (fileDir === currentDir) {
          suggestions.push({
            label: moduleName,
            kind: monaco.languages.CompletionItemKind.Module,
            insertText: moduleName,
            range: {
              startLineNumber: position.lineNumber,
              startColumn: position.column - currentPath.length,
              endLineNumber: position.lineNumber,
              endColumn: position.column,
            },
            detail: 'Python module',
            documentation: `Import from ${file.path}`,
            sortText: `0_${moduleName}`,
          })
        }
      })
      
      return { suggestions }
    },
  })

  // Provider para Lua
  const createLuaProvider = () => ({
    triggerCharacters: ['"', "'", '/', '.', '('],
    provideCompletionItems: (model: editor.ITextModel, position: Position) => {
      const lineContent = model.getLineContent(position.lineNumber)
      const textBeforeCursor = lineContent.substring(0, position.column - 1)
      
      // Detectar require Lua: require("X") ou require 'X'
      const requireMatch = textBeforeCursor.match(/require\s*\(?['"]([^'"]*?)$/)
      if (!requireMatch || !requireMatch[1]) return { suggestions: [] }
      
      const currentPath = requireMatch[1] || ''
      const modelPath = model.uri.path || ''
      const currentDir = modelPath.split('/').slice(0, -1).join('/')
      
      const suggestions: languages.CompletionItem[] = []
      
      // Sugerir arquivos Lua disponíveis
      currentFiles.forEach(file => {
        if (file.language !== 'lua') return
        
        const filePath = `/${file.path}`
        const fileName = file.path.split('/').pop() || ''
        const moduleName = fileName.replace(/\.lua$/, '')
        
        // Não sugerir o próprio arquivo
        if (filePath === modelPath) return
        
        const fileDir = filePath.split('/').slice(0, -1).join('/')
        
        // Se está no mesmo diretório, sugerir como módulo direto
        if (fileDir === currentDir) {
          suggestions.push({
            label: moduleName,
            kind: monaco.languages.CompletionItemKind.Module,
            insertText: moduleName,
            range: {
              startLineNumber: position.lineNumber,
              startColumn: position.column - currentPath.length,
              endLineNumber: position.lineNumber,
              endColumn: position.column,
            },
            detail: 'Lua module',
            documentation: `Require from ${file.path}`,
            sortText: `0_${moduleName}`,
          })
        }
      })
      
      return { suggestions }
    },
  })

  // Registrar para TypeScript, JavaScript, Python e Lua
  const tsProvider = monaco.languages.registerCompletionItemProvider('typescript', createProvider())
  const jsProvider = monaco.languages.registerCompletionItemProvider('javascript', createProvider())
  const pyProvider = monaco.languages.registerCompletionItemProvider('python', createPythonProvider())
  const luaProvider = monaco.languages.registerCompletionItemProvider('lua', createLuaProvider())

  completionDisposables.push(tsProvider, jsProvider, pyProvider, luaProvider)
  console.log('[Monaco FS] Path completion providers registered')
}
