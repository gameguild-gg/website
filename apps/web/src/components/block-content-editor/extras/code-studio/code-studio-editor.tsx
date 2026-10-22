"use client"

import { useEffect, useRef, useState } from "react"
import { useImmer } from "use-immer"
import { Button } from "@game-guild/ui/components/button"
import { Input } from "@game-guild/ui/components/input"
import { Label } from "@game-guild/ui/components/label"
import { Save, Code2, Menu, Lock, Layout } from "lucide-react"
import type { CodeStudioData, CodeFile, FileTreeFolder, LeafPanel, DisplayConfig, PanelType } from "./types"
import { MonacoCodeEditor } from "./monaco-code-editor"
import { ResultPanel } from "./result-panel"
import type { XTermTerminalHandle } from "./xterm-terminal"
import { MODE_CONFIGS, LANGUAGE_CONFIGS, getLanguageFromExtension, hasValidExtension } from "./types"
import { useTheme } from "next-themes"
import { FileExplorer } from "./file-system/file-explorer"
import { FileTabs } from "./file-tabs"
import { LanguageSelector } from "./language-selector"
import { SplitterCanvas } from "./splitter-canvas"
import { getAllLeaves, displayHasPanelType, findUniqueEditorLeaf } from "./tree-operations"
import { DisplayManager } from "./display-manager"
import { BaseAuthorSidebar } from "./base-author-sidebar"
import { EditorInstanceSwitch } from "./editor-instance-switch"
import { EmptyEditorState } from "./empty-editor-state"
import { cn } from "@/lib/utils"
import { createDefaultLayout } from "./default-layouts"
import * as FileOps from "./file-operations"
import * as LayoutOps from "./layout-operations"
import * as TabOps from "./tab-operations"
import * as PanelOps from "./panel-operations"
import { UnifiedCodeRunner, setDownloadNotificationCallback } from "./runners"
import { initializeMonacoFileSystem, syncFilesToMonacoFS, updateMonacoFile, disposeMonacoFileSystem } from "./monaco-file-system"
import { saveProjectAsCollection, countAssetReferences } from "./file-system/collection-utils"
import { collectionRepository } from "./file-system/collection-repository"
import { findAssetUris, inferMimeType, isAssetUri, toAssetUri, type AssetUri } from "@game-guild/assets"
import { getDefaultBrowserAssetRepository } from "@game-guild/assets/browser"
import { 
  ModalSize, 
  getEditorPreferences, 
  getModalSizeClasses 
} from "@/components/block-content-editor/lib/storage/editor/editor-preferences"
import { useEditorSettings } from "@/components/block-content-editor/extras/settings-menu"
import { BlockEditorShell } from "@/components/block-content-editor/extras/block-editor-shell"
import { toast } from "sonner"

const assetRepository = getDefaultBrowserAssetRepository()

interface CodeStudioEditorProps {
  data: CodeStudioData
  isPreview?: boolean
  onUpdate?: (data: Partial<CodeStudioData>) => void
  onSave?: (data: CodeStudioData) => void
  onCancel?: () => void
  onEdit?: () => void
  projectId?: string
}

export function CodeStudioEditor({ 
  data, 
  isPreview = false, 
  onUpdate, 
  onSave, 
  onCancel,
  onEdit,
  projectId,
}: CodeStudioEditorProps) {
  const { resolvedTheme } = useTheme()
  const isDarkMode = resolvedTheme === "dark"
  
  const [localData, setLocalData] = useImmer<CodeStudioData>(() => {
    // Criar layout padrão se não existir
    if (!data.layout) {
      return {
        ...data,
        mode: data.mode || "execution",
        layout: createDefaultLayout(data.mode || "execution"),
      }
    }
    return data
  })
  const [isExecuting, setIsExecuting] = useImmer(false)
  const [isSaving, setIsSaving] = useState(false)
  const [output, setOutput] = useImmer<string>("")
  const [resolvedContents, setResolvedContents] = useImmer<Record<string, string>>({})
  const settings = useEditorSettings("code-studio")
  const modalSize = settings.modalSize
  const setModalSize = settings.setModalSize
  // Two global Monaco-option groups drive every Monaco surface in the
  // code-studio:
  //   - `editorOptions` (settings.editor) is for the IDE-style
  //     secondary displays (Mirror, Test, custom) where authors actively
  //     edit. Theme, font size, line numbers, word wrap, minimap, tab
  //     size and whitespace rendering all flow from this group.
  //   - `previewOptions` (settings.preview) is for the "base" display
  //     (display-1) — the embed-sized frame that mirrors exactly what
  //     students will see — and for the published `isPreview` render
  //     mounted by `PreviewCodeStudio`. This keeps the WYSIWYG promise:
  //     the base display in the editor matches the document preview
  //     byte-for-byte.
  //   While preferences are still hydrating (`null`) we leave the bag
  //   unset; `MonacoCodeEditor` substitutes its own sane defaults so the
  //   editor can mount before IndexedDB resolves.
  const editorOptions = settings.editor
  const previewOptions = settings.preview ?? settings.editor
  const baseDisplayId = localData.layout?.displays[0]?.id
  // Canvas size in the editor follows the active display's scope:
  //   - Base (first display) renders inside a fixed embed-sized frame so authors
  //     see exactly what students will see in the document.
  //   - Secondary displays (Mirror, Test, custom) render full-bleed (IDE-style).
  const codeRunnerRef = useRef<UnifiedCodeRunner | null>(null)
  const terminalRef = useRef<XTermTerminalHandle | null>(null)
  const initializedRef = useRef(false)
  const originalDataRef = useRef<CodeStudioData>(JSON.parse(JSON.stringify(data)))
  const lastProcessedContentsRef = useRef<Record<string, string>>({})

  // Body-scroll lock is centralised in `BlockEditorShell` (html-level
  // refcounted CSS class). This editor used to set
  // `document.body.style.overflow = 'hidden'` + `pointerEvents = 'none'`
  // here as well, but that duplicated lock conflicted with the shell's
  // lock and with Radix UI's own body-lock (used by inline Select /
  // Popover / Dialog children), producing two visible bugs:
  //   1. Permanent `pointer-events: none` on body after closing.
  //   2. Scroll position snapping to top after closing (the body briefly
  //      loses then regains its overflow, which can reset scroll under
  //      some browser/layout conditions).
  // The shell already handles both scroll and click isolation correctly.

  // Initialize runner and Monaco file system
  useEffect(() => {
    // Setup download notification callback
    setDownloadNotificationCallback((message: string, isDownloading: boolean) => {
      // Mostrar no terminal
      if (terminalRef.current) {
        if (isDownloading) {
          terminalRef.current.write(`\r\n\x1b[33m📥 ${message}\x1b[0m\r\n`)
        } else {
          terminalRef.current.write(`\x1b[32m✓ ${message}\x1b[0m\r\n\r\n`)
          // Limpar o terminal após 2 segundos
          setTimeout(() => {
            if (terminalRef.current) {
              terminalRef.current.write('\x1b[2J\x1b[H') // Clear screen
            }
          }, 2000)
        }
      }
    })

    codeRunnerRef.current = new UnifiedCodeRunner({ 
      timeout: 30000,
      onRequestInput: async (prompt?: string, currentOutput?: string) => {
        if (terminalRef.current) {
          // Write current output to terminal before requesting input
          if (currentOutput) {
            terminalRef.current.write(currentOutput.replace(/\n/g, '\r\n') + '\r\n')
          }
          // Write prompt if provided
          if (prompt) {
            terminalRef.current.write(prompt)
          }
          return await terminalRef.current.requestInput()
        }
        return ""
      },
      onProgress: (message: string) => {
        // Atualizar terminal com mensagens de progresso
        console.log('[Progress]', message, 'terminal:', !!terminalRef.current)
        if (terminalRef.current) {
          try {
            terminalRef.current.write(`\x1b[36m${message}\x1b[0m\r\n`)
          } catch (e) {
            console.error('[Progress] Failed to write:', e)
          }
        }
      }
    })
    
    // Inicializar sistema de arquivos virtual do Monaco
    initializeMonacoFileSystem().then(() => {
      // Sincronizar arquivos iniciais com instanceId único
      if (localData.files.length > 0) {
        syncFilesToMonacoFS(localData.files, localData.id)
      }
    })
    
    return () => {
      codeRunnerRef.current?.dispose()
      codeRunnerRef.current = null
      disposeMonacoFileSystem()
    }
  }, [])
  
  // Sincronizar arquivos quando mudarem ou quando conteúdos forem resolvidos
  // Note: Não incluir resolvedContents nas dependências para evitar re-sync constante
  useEffect(() => {
    if (localData.files.length > 0) {
      // Criar versão dos arquivos com conteúdos resolvidos
      const filesWithResolvedContent = localData.files.map(file => ({
        ...file,
        content: resolvedContents[file.id] || file.content
      }))
      syncFilesToMonacoFS(filesWithResolvedContent, localData.id)
    }
  }, [localData.files, localData.id])

  useEffect(() => {
    if (!projectId || localData.files.length === 0) return

    const usages = localData.files
      .filter((file) => isAssetUri(file.content))
      .map((file) => ({
        uri: toAssetUri(file.content),
        consumerId: file.id,
        role: "code-file",
      }))
    void assetRepository.reconcileUsage(
      { type: "code-studio", id: `${projectId}:${localData.id}` },
      usages,
    )
  }, [localData.files, projectId, localData.id])

  // Sincronizar com mudanças externas apenas na primeira montagem
  useEffect(() => {
    if (!initializedRef.current) {
      initializedRef.current = true
      return // Skip na primeira vez, pois useState já inicializou
    }
    
    // Só sincronizar quando data mudar externamente (edição fora deste componente)
    setLocalData(draft => {
      if (!data.layout) {
        Object.assign(draft, data)
        draft.mode = data.mode || "execution"
        draft.layout = createDefaultLayout(data.mode || "execution")
      } else {
        // Usar dados do JSON exatamente como salvos
        Object.assign(draft, data)
      }
    })
  }, [data, setLocalData])

  // Resolver conteúdos de assets quando necessário
  useEffect(() => {
    const resolveContents = async () => {
      const updates: Record<string, string> = {}
      let hasChanges = false
      
      for (const file of localData.files) {
        let newContent: string
        
        // Se é referência a asset, resolver
        if (FileOps.isAssetReference(file.content)) {
          newContent = await FileOps.resolveFileContent(file)
        } else {
          // Conteúdo direto
          newContent = file.content
        }
        
        // Só atualizar se realmente mudou comparado com o último processado
        if (lastProcessedContentsRef.current[file.id] !== newContent) {
          updates[file.id] = newContent
          lastProcessedContentsRef.current[file.id] = newContent
          hasChanges = true
        }
      }
      
      // Aplicar todas as mudanças de uma vez
      if (hasChanges) {
        setResolvedContents(draft => {
          Object.assign(draft, updates)
        })
      }
    }
    
    resolveContents()
  }, [localData.files])

  // Se não há modo definido, não renderizar nada
  if (!localData.mode) {
    return null
  }

  const currentMode = MODE_CONFIGS[localData.mode]
  const activeFile = localData.files.find(f => f.id === localData.activeFileId)

  const handleDataChange = (newData: Partial<CodeStudioData>) => {
    setLocalData(draft => {
      Object.assign(draft, newData)
    })
    
    // Propagar mudanças apenas se for preview
    // No modo editor (não preview), só salva quando clicar em Save
    
  }

  const handleCodeChange = async (content: string, fileId: string) => {
    setLocalData(draft => {
      const file = draft.files.find(f => f.id === fileId)
      if (file) {
        // Se o arquivo veio de assets e ainda não foi modificado
        if (file.assetId && !file.isModified) {
          // Marcar como modificado (isso fará com que seja convertido para local ao salvar)
          FileOps.markFileAsModified(draft, fileId)
        }
        
        // Armazenar o novo conteúdo (já não é mais referência)
        file.content = content
        
        // Não chamar updateMonacoFile aqui - o Monaco já está gerenciando o arquivo
        // e chamar isso causaria uma atualização circular
      }
    })
  }

  const handleResetFile = async (fileId: string) => {
    const originalFile = originalDataRef.current.files.find(f => f.id === fileId)
    if (!originalFile) return

    // Resolve asset reference if needed
    let contentToRestore = originalFile.content
    if (FileOps.isAssetReference(originalFile.content)) {
      contentToRestore = await FileOps.resolveFileContent(originalFile)
    }

    setLocalData(draft => {
      const file = draft.files.find(f => f.id === fileId)
      
      if (file) {
        // Reset content to original
        file.content = originalFile.content
        file.isModified = false
        
        // Update Monaco file system with resolved content
        updateMonacoFile(file.path, contentToRestore, draft.id)
      }
    })

    // Force update resolved content to trigger Monaco re-render
    setResolvedContents(draft => {
      draft[fileId] = contentToRestore
      lastProcessedContentsRef.current[fileId] = contentToRestore
    })
  }

  const handleResetAllFiles = async () => {
    // Resolve all asset references first
    const resolvedContentsMap: Record<string, string> = {}
    
    for (const originalFile of originalDataRef.current.files) {
      let contentToRestore = originalFile.content
      if (FileOps.isAssetReference(originalFile.content)) {
        contentToRestore = await FileOps.resolveFileContent(originalFile)
      }
      resolvedContentsMap[originalFile.id] = contentToRestore
    }

    setLocalData(draft => {
      // Reset all files to original content
      draft.files.forEach(file => {
        const originalFile = originalDataRef.current.files.find(f => f.id === file.id)
        if (originalFile) {
          file.content = originalFile.content
          file.isModified = false
          
          // Update Monaco file system with resolved content
          const resolvedContent = resolvedContentsMap[originalFile.id] || originalFile.content
          updateMonacoFile(file.path, resolvedContent, draft.id)
        }
      })
    })

    // Force update all resolved contents to trigger Monaco re-render
    setResolvedContents(draft => {
      Object.assign(draft, resolvedContentsMap)
      // Update last processed contents
      Object.entries(resolvedContentsMap).forEach(([fileId, content]) => {
        lastProcessedContentsRef.current[fileId] = content
      })
    })
  }

  // File Management
  const handleFileSelect = (fileId: string, panelId?: string) => {
    const activeDisplay = getActiveDisplay()
    if (!activeDisplay) return

    const hasFocusEditor = displayHasPanelType(activeDisplay, "focus-editor")
    const hasFullEditor = displayHasPanelType(activeDisplay, "full-editor")
    const focusFolder = localData.folders?.find(f => f.isFocusFolder)
    const file = localData.files.find(f => f.id === fileId)
    const fileFolderPath = file ? file.path.substring(0, file.path.lastIndexOf('/')) : ''
    const isFocusFile = !!(focusFolder && file && fileFolderPath === focusFolder.path)

    // Focus editor is the only editor and the clicked file is outside the
    // focus folder — it would have nowhere to render, so ignore the click.
    if (hasFocusEditor && !hasFullEditor && focusFolder && file && !isFocusFile) {
      return
    }

    setLocalData(draft => {
      // When a focus-editor is present, files that live in the focus folder
      // should be routed ONLY to the focus-editor — they must not appear as
      // tabs in the sibling full-editor. We achieve that by just updating the
      // global active file (which the focus-editor reads) without adding the
      // file to the shared openTabs list that drives full-editor tabs.
      if (hasFocusEditor && isFocusFile) {
        draft.activeFileId = fileId
        return
      }
      TabOps.selectFile(draft, fileId, panelId, activeDisplay)
    })
  }

  const handleCloseTab = (fileId: string, panelId?: string) => {
    const activeDisplay = getActiveDisplay()
    if (!activeDisplay) return

    setLocalData(draft => {
      TabOps.closeTab(draft, fileId, panelId, activeDisplay)
    })
  }

  const handleReorderTabs = (newOrder: string[], panelId?: string) => {
    const activeDisplay = getActiveDisplay()
    setLocalData(draft => {
      TabOps.reorderTabs(draft, newOrder, panelId, activeDisplay)
    })
  }

  const handleCreateFile = (path: string, name: string) => {
    if (!localData.layout) return
    const activeDisplay = getActiveDisplay()
    if (!activeDisplay) return

    const hasFocusEditor = displayHasPanelType(activeDisplay, "focus-editor")
    const focusFolder = localData.folders?.find(f => f.isFocusFolder)
    const routeToFocusOnly = !!(hasFocusEditor && focusFolder && path === focusFolder.path)

    setLocalData(draft => {
      FileOps.createFile(draft, path, name, activeDisplay.id)
      if (routeToFocusOnly) {
        // The shared createFile helper adds the new file to the global
        // openTabs list (which feeds the full-editor). For focus-folder files
        // we strip it back out so it only surfaces inside the focus-editor.
        const fullPath = path ? `${path}/${name}` : name
        const newFile = draft.files.find(f => f.path === fullPath)
        if (newFile && draft.openTabs) {
          draft.openTabs = draft.openTabs.filter(id => id !== newFile.id)
        }
      }
    })
  }

  const handleAddFileFromAsset = (path: string, assetId: string, fileName: string, content: string) => {
    if (!localData.layout) return
    const activeDisplay = getActiveDisplay()
    if (!activeDisplay) return

    const hasFocusEditor = displayHasPanelType(activeDisplay, "focus-editor")
    const focusFolder = localData.folders?.find(f => f.isFocusFolder)
    const routeToFocusOnly = !!(hasFocusEditor && focusFolder && path === focusFolder.path)

    setLocalData(draft => {
      FileOps.addFileFromAsset(draft, path, assetId, fileName, content, activeDisplay.id)
      if (routeToFocusOnly) {
        const fullPath = path ? `${path}/${fileName}` : fileName
        const newFile = draft.files.find(f => f.path === fullPath)
        if (newFile && draft.openTabs) {
          draft.openTabs = draft.openTabs.filter(id => id !== newFile.id)
        }
      }
    })
  }

  const handleCreateFolder = (path: string, name: string) => {
    setLocalData(draft => {
      FileOps.createFolder(draft, path, name)
    })
  }

  const handleDeleteFile = (fileId: string) => {
    setLocalData(draft => {
      FileOps.deleteFile(draft, fileId)
    })
  }

  const handleDeleteFolder = (folderId: string) => {
    setLocalData(draft => {
      FileOps.deleteFolder(draft, folderId)
    })
  }

  const handleRenameFile = (fileId: string, newName: string) => {
    setLocalData(draft => {
      FileOps.renameFile(draft, fileId, newName)
    })
  }

  const handleRenameFolder = (folderId: string, newName: string) => {
    setLocalData(draft => {
      FileOps.renameFolder(draft, folderId, newName)
    })
  }

  const handleToggleFolder = (folderId: string) => {
    setLocalData(draft => {
      FileOps.toggleFolder(draft, folderId)
    })
  }

  const handleMoveFile = (fileId: string, newPath: string) => {
    setLocalData(draft => {
      FileOps.moveFile(draft, fileId, newPath)
    })
  }

  const handleMoveFolder = (folderId: string, newPath: string) => {
    setLocalData(draft => {
      FileOps.moveFolder(draft, folderId, newPath)
    })
  }

  const handleReorderFiles = (newOrder: CodeFile[]) => {
    setLocalData(draft => {
      FileOps.reorderFiles(draft, newOrder)
    })
  }

  const handleChangeFileType = (fileId: string, fileType: 'f' | 'm' | 't') => {
    setLocalData(draft => {
      const file = draft.files.find(f => f.id === fileId)
      if (file) {
        // Se está marcando como 'm' ou 't', remover a marca de outros arquivos
        if (fileType === 'm') {
          draft.files.forEach(f => {
            if (f.id !== fileId && f.isFile === 'm') {
              f.isFile = 'f'
            }
          })
        } else if (fileType === 't') {
          draft.files.forEach(f => {
            if (f.id !== fileId && f.isFile === 't') {
              f.isFile = 'f'
            }
          })
        }
        file.isFile = fileType
      }
    })
  }

  const handleToggleFileVisibility = (fileId: string) => {
    setLocalData(draft => {
      FileOps.toggleFileVisibility(draft, fileId)
    })
  }

  const handleToggleFolderVisibility = (folderId: string) => {
    setLocalData(draft => {
      FileOps.toggleFolderVisibility(draft, folderId)
    })
  }

  const handleToggleFileReadonly = (fileId: string) => {
    setLocalData(draft => {
      FileOps.toggleFileReadonly(draft, fileId)
    })
  }

  const handleToggleFolderReadonly = (folderId: string) => {
    setLocalData(draft => {
      FileOps.toggleFolderReadonly(draft, folderId)
    })
  }

  const handleToggleFocusFolder = (folderId: string) => {
    setLocalData(draft => {
      FileOps.toggleFocusFolder(draft, folderId)
    })
  }

  const handleSetAllReadonly = (readonly: boolean) => {
    setLocalData(draft => {
      FileOps.setAllFilesReadonly(draft, readonly)
    })
  }

  const handleSetAllHidden = (hidden: boolean) => {
    setLocalData(draft => {
      FileOps.setAllFilesHidden(draft, hidden)
    })
  }

  // Collection handlers
  const handleImportCollection = async (
    path: string, 
    files: Array<{ name: string; path: string; assetId: string; isFile?: 'f' | 'm' | 't'; readonly?: boolean; isVisible?: boolean }>,
    folderMetadata?: Map<string, { readonly?: boolean; isVisible?: boolean }>
  ) => {
    const invalidFile = files.find((file) => file.assetId && !isAssetUri(file.assetId))
    if (invalidFile) throw new Error(`Collection contains an invalid asset reference: ${invalidFile.name}`)

    // Group files by their directory paths to create folders first
    const foldersNeeded = new Set<string>()
    
    for (const file of files) {
      // Extract all folder paths needed for this file
      const pathParts = file.path.split('/')
      pathParts.pop() // Remove filename
      
      // Build cumulative paths (e.g., "src", "src/components", "src/components/ui")
      let currentPath = ''
      for (const part of pathParts) {
        currentPath = currentPath ? `${currentPath}/${part}` : part
        const fullFolderPath = path ? `${path}/${currentPath}` : currentPath
        foldersNeeded.add(fullFolderPath)
      }
    }

    // Sort folders by depth (create parent folders first)
    const sortedFolders = Array.from(foldersNeeded).sort((a, b) => {
      const depthA = (a.match(/\//g) || []).length
      const depthB = (b.match(/\//g) || []).length
      return depthA - depthB
    })

    // Create folders
    setLocalData(draft => {
      for (const folderPath of sortedFolders) {
        // Check if folder already exists
        const exists = draft.folders?.some(f => f.path === folderPath)
        if (exists) {
          continue
        }

        // Extract parent path and folder name
        const lastSlash = folderPath.lastIndexOf('/')
        const folderName = lastSlash >= 0 ? folderPath.substring(lastSlash + 1) : folderPath

        // Get metadata for this folder from collection (if available)
        // Need to remove destination path prefix to get original collection path
        const relativeFolderPath = path && folderPath.startsWith(`${path}/`) 
          ? folderPath.substring(path.length + 1) 
          : folderPath
        const metadata = folderMetadata?.get(relativeFolderPath)
        
        const newFolder: FileTreeFolder = {
          id: `${Date.now()}-${Math.random()}`,
          name: folderName,
          path: folderPath,
          isExpanded: true,
          isVisible: metadata?.isVisible ?? true,
          children: [],
          type: "folder",
          readonly: metadata?.readonly,
        }

        if (!draft.folders) {
          draft.folders = []
        }
        draft.folders.push(newFolder)
      }
    })

    // Import files
    for (const file of files) {
      // Use the stable URI or empty content for empty collection files.
      const content = file.assetId || ''

      // Extract folder path and filename from file.path
      const pathParts = file.path.split('/')
      const fileName = pathParts.pop() || file.name
      const relativeFolderPath = pathParts.join('/')
      const fullPath = path
        ? (relativeFolderPath ? `${path}/${relativeFolderPath}/${fileName}` : `${path}/${fileName}`)
        : (relativeFolderPath ? `${relativeFolderPath}/${fileName}` : fileName)

      // Add file directly to draft
      setLocalData(draft => {
        const existingFile = draft.files.find(f => f.path === fullPath)
        if (existingFile) {
          existingFile.content = content
          existingFile.assetId = file.assetId
          existingFile.isModified = false
          return
        }

        const language = getLanguageFromExtension(fileName)
        const newFile: CodeFile = {
          id: `${Date.now()}-${Math.random()}`,
          name: fileName,
          content,
          language,
          isFile: file.isFile || 'f',
          isVisible: file.isVisible ?? true,
          readonly: file.readonly,
          path: fullPath,
          assetId: file.assetId || undefined,
          isModified: false,
        }

        draft.files.push(newFile)
      })
    }
  }

  const handleSaveAsCollection = async (path: string, folderName?: string): Promise<{ success: boolean; error?: string }> => {
    const createdAssetUris: AssetUri[] = []
    let createdCollectionId: string | undefined
    try {
      // Get files and folders for the specified path
      let targetFiles: CodeFile[] = []
      let targetFolders: FileTreeFolder[] = []

      if (path === "") {
        // Root: get files and folders that are at root level
        // Root files: path doesn't contain '/' (path = filename)
        // Root folders: path doesn't contain '/' (path = foldername)
        targetFiles = localData.files.filter(f => !f.path.includes('/'))
        targetFolders = localData.folders.filter(f => !f.path.includes('/'))
      } else {
        // Specific folder: files and subfolders inside this path
        // Files inside "new": path starts with "new/" (e.g., "new/README.md")
        // Subfolders inside "new": path starts with "new/" (e.g., "new/subfolder")
        const pathPrefix = `${path}/`
        targetFiles = localData.files.filter(f => f.path.startsWith(pathPrefix))
        targetFolders = localData.folders.filter(f => f.path.startsWith(pathPrefix))
        
      }

      // Convert local files to assets before saving
      const convertedFiles: CodeFile[] = []
      for (const file of targetFiles) {
        // Check if file is empty
        const isEmpty = !file.content || file.content.trim() === ''
        
        if (isEmpty) {
          // Empty file - keep without creating asset
          convertedFiles.push({
            ...file,
            content: '',
          })
          continue
        }
        
        // Check if file needs to be converted to a new asset
        if (isAssetUri(file.content) && !file.isModified) {
          // Asset that hasn't been modified - keep reference
          convertedFiles.push(file)
        } else {
          // Get actual content
          let contentToSave = file.content
          if (isAssetUri(file.content) && file.isModified) {
            // Modified asset - need to get resolved content from resolvedContents
            const resolvedContent = resolvedContents[file.id]
            if (resolvedContent === undefined) {
              throw new Error(`Cannot save ${file.name}: its asset content is unavailable`)
            }
            contentToSave = resolvedContent
          }

          if (!contentToSave) {
            convertedFiles.push({ ...file, content: "", assetId: undefined, isModified: false })
            continue
          }

          // Create asset from content
          const result = await assetRepository.importBlob(
            new Blob([contentToSave], { type: inferMimeType(file.name) }),
            {
              name: file.name,
              source: { type: "generated", value: "code-studio" },
              scope: { type: "code-studio", id: `${projectId ?? "local"}:${localData.id}` },
            },
          )
          createdAssetUris.push(result.uri)
          convertedFiles.push({
            ...file,
            content: result.uri,
            assetId: result.uri,
            isModified: false,
          })
        }
      }

      // Recursively build folder structure with files and subfolders
      const buildFolderStructure = async (
        folderPath: string, 
        folderName: string,
        originalFolder: FileTreeFolder
      ): Promise<FileTreeFolder> => {
        // folderPath is the full path of this folder (e.g., "new" or "new/subfolder")
        // Get all files that are directly inside this folder
        // Files in "new": path = "new/filename.ext" (starts with folderPath/ and no additional slashes)
        const pathPrefix = `${folderPath}/`
        const folderFiles = localData.files.filter(f => {
          if (!f.path.startsWith(pathPrefix)) return false
          // Check if file is directly in this folder (no sub-paths)
          const relativePath = f.path.substring(pathPrefix.length)
          return !relativePath.includes('/')
        })
        
        // Get all subfolders that are directly inside this folder
        // Subfolders in "new": path = "new/subfoldername" (starts with folderPath/ and no additional slashes)
        const subfolders = localData.folders.filter(f => {
          if (!f.path.startsWith(pathPrefix)) return false
          const relativePath = f.path.substring(pathPrefix.length)
          return !relativePath.includes('/')
        })
        
        const children: (CodeFile | FileTreeFolder)[] = []

        // Convert and add files
        for (const file of folderFiles) {
          // Check if file is empty
          const isEmpty = !file.content || file.content.trim() === ''
          
          if (isEmpty) {
            // Empty file - keep without creating asset
            children.push({
              ...file,
              content: '',
            })
            continue
          }
          
          if (isAssetUri(file.content) && !file.isModified) {
            // Asset that hasn't been modified - keep reference
            children.push(file)
          } else {
            // File needs new asset (either local content or modified asset)
            let contentToSave = file.content
            if (isAssetUri(file.content) && file.isModified) {
              // Modified asset - need to get resolved content
              const resolvedContent = resolvedContents[file.id]
              if (resolvedContent === undefined) {
                throw new Error(`Cannot save ${file.name}: its asset content is unavailable`)
              }
              contentToSave = resolvedContent
            }

            if (!contentToSave) {
              children.push({ ...file, content: "", assetId: undefined, isModified: false })
              continue
            }

            const result = await assetRepository.importBlob(
              new Blob([contentToSave], { type: inferMimeType(file.name) }),
              {
                name: file.name,
                source: { type: "generated", value: "code-studio" },
                scope: { type: "code-studio", id: `${projectId ?? "local"}:${localData.id}` },
              },
            )
            createdAssetUris.push(result.uri)
            children.push({
              ...file,
              content: result.uri,
              assetId: result.uri,
              isModified: false,
            })
          }
        }

        // Recursively process subfolders
        for (const subfolder of subfolders) {
          const builtSubfolder = await buildFolderStructure(subfolder.path, subfolder.name, subfolder)
          children.push(builtSubfolder)
        }

        return {
          id: `folder-${folderPath}-${folderName}`,
          name: folderName,
          path: folderPath,
          isExpanded: false,
          isVisible: originalFolder.isVisible,
          readonly: originalFolder.readonly,
          children,
          type: "folder"
        }
      }

      // Build converted folders with proper hierarchy
      const convertedFolders: FileTreeFolder[] = []
      for (const folder of targetFolders) {
        const builtFolder = await buildFolderStructure(folder.path, folder.name, folder)
        convertedFolders.push(builtFolder)
      }

      // Now save as collection using converted files
      const result = await saveProjectAsCollection({
        name: folderName || "Untitled Collection",
        folders: convertedFolders,
        files: convertedFiles,
      })

      if (!result.success || !result.collectionId) {
        await Promise.all(
          createdAssetUris.map((uri) =>
            assetRepository.remove(uri, { force: true }).catch(() => undefined),
          ),
        )
        return result
      }
      createdCollectionId = result.collectionId

      const referencedUris = findAssetUris({ convertedFiles, convertedFolders })
      await assetRepository.reconcileUsage(
        { type: "code-studio-collection", id: result.collectionId },
        referencedUris.map((uri, index) => ({
          uri,
          consumerId: `file:${index}`,
          role: "file-body",
        })),
      )

      return result
    } catch (error) {
      if (createdCollectionId) {
        await collectionRepository.remove(createdCollectionId).catch(() => undefined)
        await assetRepository.reconcileUsage(
          { type: "code-studio-collection", id: createdCollectionId },
          [],
        ).catch(() => undefined)
      }
      await Promise.all(
        createdAssetUris.map((uri) =>
          assetRepository.remove(uri, { force: true }).catch(() => undefined),
        ),
      )
      console.error('Failed to save collection:', error)
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Unknown error',
      }
    }
  }

  // Layout handlers
  const getActiveDisplay = (): DisplayConfig | undefined => {
    if (!localData.layout) return undefined
    return localData.layout.displays.find(d => d.id === localData.layout!.activeDisplayId)
  }

  const handleToggleLayoutEdit = () => {
    setLocalData(draft => {
      LayoutOps.toggleLayoutEdit(draft)
    })
  }

  const handleSelectDisplay = (displayId: string) => {
    setLocalData(draft => {
      LayoutOps.selectDisplay(draft, displayId)
      
      // Ao trocar de display, atualizar activeFileId para refletir o estado do novo display
      const newDisplay = draft.layout?.displays.find(d => d.id === displayId)
      if (newDisplay) {
        const uniqueEditor = findUniqueEditorLeaf(newDisplay)
        if (uniqueEditor) {
          // Display com editor único: usar uniqueActiveFileId
          draft.activeFileId = newDisplay.uniqueActiveFileId
        } else {
          // Display com editor múltiplo: manter activeFileId global se estiver nas tabs abertas
          if (draft.openTabs && draft.openTabs.length > 0) {
            if (!draft.activeFileId || !draft.openTabs.includes(draft.activeFileId)) {
              draft.activeFileId = draft.openTabs[draft.openTabs.length - 1]
            }
          } else {
            draft.activeFileId = undefined
          }
        }
      }
    })
  }

  const handleCreateDisplay = (name: string, templateId: string) => {
    setLocalData(draft => {
      LayoutOps.createDisplay(draft, name, templateId)
    })
  }

  const handleDeleteDisplay = (displayId: string) => {
    setLocalData(draft => {
      LayoutOps.deleteDisplay(draft, displayId)
    })
  }

  const handleRenameDisplay = (displayId: string, newName: string) => {
    setLocalData(draft => {
      LayoutOps.renameDisplay(draft, displayId, newName)
    })
  }

  const handleApplyTemplate = (displayId: string, templateId: string) => {
    setLocalData(draft => {
      LayoutOps.applyTemplateToDisplay(draft, displayId, templateId)
    })
  }

  const handleUpdateCurrentDisplay = (updatedDisplay: DisplayConfig) => {
    setLocalData(draft => {
      LayoutOps.updateCurrentDisplay(draft, updatedDisplay)
    })
  }

  const handleAddPanel = (type: PanelType) => {
    const activeDisplay = getActiveDisplay()
    if (!activeDisplay) return

    // Hard cap: at most one Full Editor and at most one Focus Editor per
    // display. Other panel types (explorer, output) may repeat.
    if (
      (type === "full-editor" || type === "focus-editor") &&
      displayHasPanelType(activeDisplay, type)
    ) {
      return
    }

    setLocalData(draft => {
      PanelOps.addPanel(draft, activeDisplay, type)
    })
  }

  const handleSplitResize = (splitId: string, sizes: number[]) => {
    const activeDisplay = getActiveDisplay()
    if (!activeDisplay) return

    setLocalData(draft => {
      PanelOps.resizeSplit(draft, activeDisplay, splitId, sizes)
    })
  }

  const handleRemovePanel = (panelId: string) => {
    const activeDisplay = getActiveDisplay()
    if (!activeDisplay) return

    setLocalData(draft => {
      PanelOps.removePanel(draft, activeDisplay, panelId)
    })
  }

  const handleMovePanel = (
    sourcePanelId: string,
    targetPanelId: string,
    position: "top" | "right" | "bottom" | "left",
  ) => {
    const activeDisplay = getActiveDisplay()
    if (!activeDisplay) return

    setLocalData(draft => {
      PanelOps.movePanel(draft, activeDisplay, sourcePanelId, targetPanelId, position)
    })
  }

  const handleToggleEditorInstance = (panelId: string) => {
    const activeDisplay = getActiveDisplay()
    if (!activeDisplay) return

    setLocalData(draft => {
      PanelOps.toggleEditorInstance(draft, activeDisplay, panelId)
    })
  }

  // Renderizar conteúdo de cada painel
  const renderPanelContent = (panel: LeafPanel, displayConfig?: DisplayConfig) => {
    // Pick the Monaco-options group based on which display this leaf
    // belongs to. The base display (display-1) — and the `isPreview`
    // re-mount, which also targets the base display — uses the global
    // `preview` group because that's what students see. Every other
    // display is the IDE-style authoring surface and uses the global
    // `editor` group.
    const isBaseLeaf = isPreview || (displayConfig?.id !== undefined && displayConfig.id === baseDisplayId)
    const optionsForLeaf = isBaseLeaf ? previewOptions : editorOptions
    switch (panel.type) {
      case "explorer":
        return (
          <FileExplorer
            files={localData.files}
            folders={localData.folders || []}
            activeFileId={localData.activeFileId}
            onFileSelect={handleFileSelect}
            onCreateFile={handleCreateFile}
            onCreateFolder={handleCreateFolder}
            onDeleteFile={handleDeleteFile}
            onDeleteFolder={handleDeleteFolder}
            onRenameFile={handleRenameFile}
            onRenameFolder={handleRenameFolder}
            onToggleFolder={handleToggleFolder}
            onMoveFile={handleMoveFile}
            onMoveFolder={handleMoveFolder}
            onReorderFiles={handleReorderFiles}
            onAddFileFromAsset={handleAddFileFromAsset}
            onChangeFileType={handleChangeFileType}
            onToggleFileVisibility={handleToggleFileVisibility}
            onToggleFolderVisibility={handleToggleFolderVisibility}
            onToggleFileReadonly={handleToggleFileReadonly}
            onToggleFolderReadonly={handleToggleFolderReadonly}
            onToggleFocusFolder={handleToggleFocusFolder}
            onSetAllReadonly={handleSetAllReadonly}
            onSetAllHidden={handleSetAllHidden}
            onImportCollection={handleImportCollection}
            onSaveAsCollection={handleSaveAsCollection}
            isPreview={isPreview}
          />
        )
      
      case "full-editor": {
        // No preview, usar displayConfig passado; no editor, usar activeDisplay
        const displayToUse = isPreview && displayConfig ? displayConfig : getActiveDisplay()
        const isUniqueInstance = panel.editorInstance === "unique"
        const rawCurrentOpenTabs = isUniqueInstance 
          ? (displayToUse?.uniqueOpenTabs || [])
          : (localData.openTabs || [])

        // When the same display also has a focus-editor, files that live in
        // the focus folder are owned exclusively by it — strip them out of the
        // full-editor's tab strip so they don't show up in both places. This
        // is defensive: handleFileSelect / handleCreateFile already avoid
        // adding them, but legacy data may carry stale entries.
        const fullDisplayHasFocus = displayToUse ? displayHasPanelType(displayToUse, "focus-editor") : false
        const fullFocusFolder = fullDisplayHasFocus
          ? localData.folders?.find(f => f.isFocusFolder)
          : undefined
        const currentOpenTabs = fullFocusFolder
          ? rawCurrentOpenTabs.filter(tabId => {
              const f = localData.files.find(file => file.id === tabId)
              if (!f) return true
              const folderPath = f.path.substring(0, f.path.lastIndexOf('/'))
              return folderPath !== fullFocusFolder.path
            })
          : rawCurrentOpenTabs

        const rawCurrentActiveFileId = isUniqueInstance
          ? displayToUse?.uniqueActiveFileId
          : localData.activeFileId
        // If the global active file points at a focus-folder file (now owned
        // by the focus-editor), the full-editor should fall back to the first
        // tab it actually has so its editor surface keeps showing something
        // meaningful instead of going blank.
        const currentActiveFileId = rawCurrentActiveFileId && currentOpenTabs.includes(rawCurrentActiveFileId)
          ? rawCurrentActiveFileId
          : currentOpenTabs[0]
        
        // No preview, verificar se há explorer no Display Base para permitir fechar tabs
        const hasExplorer = displayConfig ? displayHasPanelType(displayConfig, 'explorer') : true
        const canCloseTabs = isPreview ? hasExplorer : true
        
        return (
          <div className="flex flex-col h-full relative">
            {/* Editor Instance Switch */}
            {panel.editorInstance && localData.layout?.editMode && (
              <EditorInstanceSwitch
                editorInstance={panel.editorInstance}
                onToggle={() => handleToggleEditorInstance(panel.id)}
              />
            )}
            
            <FileTabs
              files={localData.files}
              openTabs={currentOpenTabs}
              activeFileId={currentActiveFileId}
              editorInstance={panel.editorInstance}
              panelId={panel.id}
              onSelectTab={(fileId) => handleFileSelect(fileId, panel.id)}
              onCloseTab={canCloseTabs ? (fileId) => handleCloseTab(fileId, panel.id) : undefined}
              onReorderTabs={handleReorderTabs}
            />
            <div className="flex-1 min-h-0 relative">
              {currentOpenTabs.length === 0 ? (
                <EmptyEditorState />
              ) : (
                <>
                  {currentOpenTabs.map((fileId, index) => {
                    const file = localData.files.find(f => f.id === fileId)
                    if (!file) return null
                    const isActive = fileId === currentActiveFileId
                    
                    // Verificar se arquivo ou pasta pai está readonly
                    let isFileReadonly = file.readonly || false
                    if (!isFileReadonly && localData.folders) {
                      // Verificar se está dentro de uma pasta readonly
                      const fileFolder = localData.folders.find(folder => 
                        file.path.startsWith(folder.path + "/")
                      )
                      if (fileFolder?.readonly) {
                        isFileReadonly = true
                      }
                    }
                    
                    return (
                      <div
                        key={`${file.id}-${index}`}
                        className="absolute inset-0"
                        style={{ 
                          visibility: isActive ? 'visible' : 'hidden',
                          pointerEvents: isActive ? 'auto' : 'none',
                          zIndex: isActive ? 1 : 0
                        }}
                      >
                        <MonacoCodeEditor
                          fileId={file.id}
                          filePath={file.path}
                          instanceId={localData.id} // ID único da instância para isolamento completo
                          value={resolvedContents[file.id] || file.content}
                          onChange={(content) => handleCodeChange(content, file.id)}
                          language={file.language}
                          readonly={localData.readonly || isFileReadonly}
                          options={optionsForLeaf}
                        />
                      </div>
                    )
                  })}
                </>
              )}
            </div>
          </div>
        )
      
      }
      case "focus-editor": {
        // Focus editor: Language selector instead of file tabs.
        // Always rendered in "multiple" mode — it intentionally shares the
        // global active file with sibling editors so the toggle is hidden.
        const focusFolderForRender = localData.folders?.find(f => f.isFocusFolder)

        // No focus folder configured — the focus-editor has no scope to render.
        // Show a guidance message instead of an empty editor; the author can
        // mark any folder as the focus folder from the file explorer.
        if (!focusFolderForRender) {
          return (
            <div className="flex flex-col h-full items-center justify-center text-center px-6 py-8 gap-2 text-gray-600 dark:text-gray-300">
              <div className="text-sm font-medium">
                Nenhuma pasta marcada como foco
              </div>
              <div className="text-xs text-gray-500 dark:text-gray-400 max-w-xs">
                Marque uma pasta como “foco” no explorador de arquivos para
                que ela apareça aqui.
              </div>
            </div>
          )
        }

        const rawFocusActiveFileId = localData.activeFileId

        // Constrain the rendered file to the focus folder. When the global
        // active file lives outside (e.g. user opened it in a sibling Full
        // Editor), fall back to the first file inside the focus folder so the
        // focus-editor keeps showing the focus context instead of spilling.
        const focusFolderFiles = localData.files.filter(
          f => f.path.substring(0, f.path.lastIndexOf('/')) === focusFolderForRender.path,
        )
        const focusActiveFileId = rawFocusActiveFileId && focusFolderFiles.some(f => f.id === rawFocusActiveFileId)
          ? rawFocusActiveFileId
          : focusFolderFiles[0]?.id
        
        return (
          <div className="flex flex-col h-full relative">
            <LanguageSelector
              files={localData.files}
              folders={localData.folders || []}
              activeFileId={focusActiveFileId}
              onSelectLanguage={(fileId) => handleFileSelect(fileId, panel.id)}
              isPreview={isPreview}
              onResetFile={handleResetFile}
              onResetAllFiles={handleResetAllFiles}
            />
            
            <div className="flex-1 min-h-0 relative">
              {!focusActiveFileId ? (
                <EmptyEditorState />
              ) : (
                (() => {
                  const file = localData.files.find(f => f.id === focusActiveFileId)
                  if (!file) return <EmptyEditorState />
                  
                  // Check if file or parent folder is readonly
                  let isFileReadonly = file.readonly || false
                  if (!isFileReadonly && localData.folders) {
                    const fileFolder = localData.folders.find(folder => 
                      file.path.startsWith(folder.path + "/")
                    )
                    if (fileFolder?.readonly) {
                      isFileReadonly = true
                    }
                  }
                  
                  return (
                    <MonacoCodeEditor
                      fileId={file.id}
                      filePath={file.path}
                      instanceId={localData.id}
                      value={resolvedContents[file.id] || file.content}
                      onChange={(content) => handleCodeChange(content, file.id)}
                      language={file.language}
                      readonly={localData.readonly || isFileReadonly}
                      options={optionsForLeaf}
                    />
                  )
                })()
              )}
            </div>
          </div>
        )
      
      }
      case "output": {
        const mainFile = localData.files.find(f => f.isFile === 'm')
        const testFile = localData.files.find(f => f.isFile === 't')
        
        return (
          <ResultPanel
            ref={terminalRef}
            output={output}
            isExecuting={isExecuting}
            mode={localData.mode!}
            onExecuteFile={handleExecuteFile}
            onExecuteProject={handleExecuteProject}
            onExecuteTest={handleExecuteTest}
            onStop={handleStop}
            testCases={localData.testCases?.[localData.activeFileId || ""] || []}
            activeFile={activeFile}
            hasMainFile={!!mainFile}
            hasTestFile={!!testFile}
          />
        )
      }
    }
  }

  // Executa o arquivo atualmente selecionado
  const handleExecuteFile = async () => {
    if (!codeRunnerRef.current || !terminalRef.current) return
    
    // Buscar arquivo ativo: primeiro tentar painéis únicos, depois global
    const activeDisplay = getActiveDisplay()
    let fileToExecute = activeFile // Padrão: arquivo global ativo
    
    // Se houver painéis com instância única, usar o arquivo ativo deles
    if (activeDisplay) {
      const uniqueEditorPanel = findUniqueEditorLeaf(activeDisplay)
      if (uniqueEditorPanel && activeDisplay.uniqueActiveFileId) {
        fileToExecute = localData.files.find(f => f.id === activeDisplay.uniqueActiveFileId)
      }
    }
    
    if (!fileToExecute) return
    
    await executeFile(fileToExecute)
  }

  // Executa o arquivo marcado como 'm' (main)
  const handleExecuteProject = async () => {
    if (!codeRunnerRef.current || !terminalRef.current) return
    
    const mainFile = localData.files.find(f => f.isFile === 'm')
    if (!mainFile) return
    
    await executeFile(mainFile)
  }

  // Executa o arquivo marcado como 't' (test)
  const handleExecuteTest = async () => {
    if (!codeRunnerRef.current || !terminalRef.current) return
    
    const testFile = localData.files.find(f => f.isFile === 't')
    if (!testFile) return
    
    await executeFile(testFile)
  }

  // Função auxiliar para executar um arquivo
  const executeFile = async (fileToExecute: CodeFile) => {
    if (!codeRunnerRef.current || !terminalRef.current) return
    
    setIsExecuting(true)
    setOutput('') // Limpar output anterior - isso vai limpar o terminal via useEffect

    terminalRef.current.write('\x1b[33m⟳ Starting execution...\x1b[0m\r\n')

    try {
      // Criar mapa de arquivos para suportar imports - apenas arquivos da mesma linguagem
      // Resolver conteúdos de assets antes de executar
      const filesMap: Record<string, string> = {}
      for (const file of localData.files) {
        if (file.language === fileToExecute.language && 
            hasValidExtension(file.path, fileToExecute.language)) {
          // Resolver conteúdo se for referência a asset
          const content = resolvedContents[file.id] || file.content
          filesMap[`/${file.path}`] = content
        }
      }

      // Usar runWithFiles para suportar imports entre arquivos
      const result = await codeRunnerRef.current.runWithFiles(
        fileToExecute.language,
        `/${fileToExecute.path}`,
        filesMap
      )

      // Escrever resultado no terminal (já foi limpo automaticamente quando isExecuting=true)
      if (result.stdout) {
        terminalRef.current.write(result.stdout.replace(/\n/g, '\r\n'))
      }
      if (result.stderr) {
        if (result.stdout) terminalRef.current.write('\r\n')
        terminalRef.current.write('\x1b[31m' + result.stderr.replace(/\n/g, '\r\n') + '\x1b[0m')
      }
      if (result.exitCode !== 0) {
        terminalRef.current.write(`\r\n\x1b[33m[Process exited with code ${result.exitCode}]\x1b[0m`)
      }
      terminalRef.current.write(`\r\n\x1b[90m[Execution time: ${result.executionTime.toFixed(2)}ms]\x1b[0m\r\n`)
    } catch (error) {
      terminalRef.current.write(`\r\n\x1b[31mExecution error: ${error instanceof Error ? error.message : String(error)}\x1b[0m\r\n`)
    } finally {
      setIsExecuting(false)
    }
  }

  const handleExecute = handleExecuteFile // Manter retrocompatibilidade

  const handleStop = async () => {
    if (codeRunnerRef.current) {
      await codeRunnerRef.current.interrupt()
      setIsExecuting(false)
      setOutput(prev => prev + '\n\x1b[33m[Execution interrupted]\x1b[0m')
    }
  }

  const handleSaveClick = async () => {
    if (isSaving) return
    setIsSaving(true)
    const createdAssetUris: AssetUri[] = []
    const usageScope = { type: "code-studio", id: `${projectId ?? "local"}:${localData.id}` }
    const previousUris = findAssetUris(localData.files)
    try {
      const files = await Promise.all(localData.files.map(async (file): Promise<CodeFile> => {
        if (!file.content) {
          return { ...file, assetId: undefined, isModified: false }
        }
        if (isAssetUri(file.content) && !file.isModified) return file

        const content = isAssetUri(file.content)
          ? resolvedContents[file.id]
          : file.content
        if (content === undefined) {
          throw new Error(`Cannot save ${file.name}: its asset content is unavailable`)
        }
        if (!content) return { ...file, content: "", assetId: undefined, isModified: false }

        const record = await assetRepository.importBlob(
          new Blob([content], { type: inferMimeType(file.name) }),
          {
            name: file.name,
            source: { type: "generated", value: "code-studio" },
            scope: { type: "code-studio", id: `${projectId ?? "local"}:${localData.id}` },
          },
        )
        createdAssetUris.push(record.uri)
        return { ...file, content: record.uri, assetId: record.uri, isModified: false }
      }))

      const display1 = localData.layout?.displays.find(d => d.id === 'display-1')
      const isDisplay1Unique = display1 ? !!findUniqueEditorLeaf(display1) : false
      let syncedActiveFileId = localData.activeFileId

      if (isDisplay1Unique) {
        if (display1?.uniqueOpenTabs && display1.uniqueOpenTabs.length > 0) {
          syncedActiveFileId = display1.uniqueActiveFileId
        } else {
          syncedActiveFileId = undefined
        }
      } else {
        if (localData.openTabs && localData.openTabs.length > 0) {
          if (localData.activeFileId && localData.openTabs.includes(localData.activeFileId)) {
            syncedActiveFileId = localData.activeFileId
          } else {
            syncedActiveFileId = localData.openTabs[localData.openTabs.length - 1]
          }
        } else {
          syncedActiveFileId = undefined
        }
      }

      const dataToSave: CodeStudioData = {
        ...localData,
        files,
        layout: localData.layout ? {
          ...localData.layout,
          activeDisplayId: 'display-1'
        } : undefined,
        activeFileId: syncedActiveFileId
      }
      await assetRepository.reconcileUsage(
        usageScope,
        findAssetUris(files).map((uri, index) => ({
          uri,
          consumerId: `file:${index}`,
          role: "code-file",
        })),
      )
      await onSave?.(dataToSave)
      setLocalData(draft => {
        Object.assign(draft, dataToSave)
      })
    } catch (error) {
      await assetRepository.reconcileUsage(
        usageScope,
        previousUris.map((uri, index) => ({
          uri,
          consumerId: `file:${index}`,
          role: "code-file",
        })),
      ).catch(() => undefined)
      await Promise.all(
        createdAssetUris.map((uri) =>
          assetRepository.remove(uri, { force: true }).catch(() => undefined),
        ),
      )
      toast.error("Failed to save Code Studio", {
        description: error instanceof Error ? error.message : "Asset persistence failed",
      })
    } finally {
      setIsSaving(false)
    }
  }

  const handleCancelClick = () => {
    onCancel?.()
  }

  // Se for preview (renderizado no documento), não mostra o modal
  if (isPreview) {
    // Usar Display Base (display-1) como espelho do preview
    const baseDisplay = localData.layout?.displays.find(d => d.id === 'display-1')
    if (!baseDisplay) return null

    const leaves = getAllLeaves(baseDisplay.root)
    const isSingleLeaf = leaves.length === 1
    // Minimal embed: a single focus-editor or full-editor leaf renders without
    // any surrounding chrome (no rounded card, no border). Larger trees use
    // the full SplitterCanvas in read-only mode.
    if (isSingleLeaf && leaves[0]) {
      return (
        <div className="w-full h-[500px] min-h-0">
          {renderPanelContent(leaves[0], baseDisplay)}
        </div>
      )
    }

    return (
      <div className="w-full h-[500px] min-h-0">
        <SplitterCanvas
          root={baseDisplay.root}
          renderLeaf={(leaf) => renderPanelContent(leaf, baseDisplay)}
          editable={false}
          resizable
        />
      </div>
    )
  }

  // Modal de edição (fullscreen)
  const baseDisplayIdForSettings = localData.layout?.displays[0]?.id
  const activeDisplayIdForSettings = localData.layout?.activeDisplayId
  const isViewingBase = baseDisplayIdForSettings !== undefined && baseDisplayIdForSettings === activeDisplayIdForSettings

  return (
    <BlockEditorShell
      settings={settings}
      onClose={handleCancelClick}
      icon={<Code2 className="h-5 w-5 text-blue-600 dark:text-blue-400" />}
      title="Code Studio"
      defaultMonacoTab={isViewingBase ? 'preview' : 'editor'}
    >
        {/* Settings Bar */}
        <div className="flex items-center gap-3 p-4 border-b border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900 flex-wrap">
          <div className="flex items-center gap-2 shrink-0">
            <Label htmlFor="title" className="text-sm font-medium">
              Title:
            </Label>
            <Input
              id="title"
              value={localData.title || ""}
              onChange={(e) => handleDataChange({ title: e.target.value })}
              placeholder="Optional title"
              className="w-48"
            />
          </div>

          {/* Display Selector — read-only chips when NOT editing layout */}
          {!localData.layout?.editMode && localData.layout && (
            <div className="flex items-center gap-1 px-2 py-1 bg-gray-100 dark:bg-gray-800 rounded-md border border-gray-200 dark:border-gray-700">
              {localData.layout.displays.map((display) => (
                <button
                  key={display.id}
                  onClick={() => handleSelectDisplay(display.id)}
                  className={cn(
                    "px-2.5 py-1 rounded text-xs font-medium transition-all",
                    localData.layout?.activeDisplayId === display.id
                      ? "bg-blue-600 text-white shadow-sm"
                      : "bg-transparent text-gray-600 dark:text-gray-400 hover:bg-gray-200 dark:hover:bg-gray-700"
                  )}
                  title={display.name}
                >
                  {display.name}
                </button>
              ))}
            </div>
          )}

          {/* Inline layout-edit toolbar (display tabs + templates + add-panel) */}
          {localData.layout?.editMode && localData.layout && (() => {
            const editToolbarActive = localData.layout.displays.find(
              d => d.id === localData.layout?.activeDisplayId,
            )
            const existingPanelTypes = editToolbarActive
              ? new Set<PanelType>(getAllLeaves(editToolbarActive.root).map(l => l.type))
              : undefined
            return (
              <DisplayManager
                displays={localData.layout.displays}
                activeDisplayId={localData.layout.activeDisplayId}
                activeDisplayScope={
                  localData.layout.displays[0]?.id === localData.layout.activeDisplayId ? "compact" : "expanded"
                }
                existingPanelTypes={existingPanelTypes}
                onSelectDisplay={handleSelectDisplay}
                onCreateDisplay={handleCreateDisplay}
                onDeleteDisplay={handleDeleteDisplay}
                onRenameDisplay={handleRenameDisplay}
                onApplyTemplate={handleApplyTemplate}
                onAddPanel={handleAddPanel}
              />
            )
          })()}

          {/* Layout Edit Button */}
          <div className="ml-auto flex items-center gap-2 shrink-0">
            <Button
              variant={localData.layout?.editMode ? "default" : "outline"}
              size="sm"
              onClick={handleToggleLayoutEdit}
              className="h-8"
              title={localData.layout?.editMode ? "Exit Layout Edit" : "Edit Layout"}
            >
              <Layout className="h-4 w-4 mr-2" />
              {localData.layout?.editMode ? "Done" : "Layout"}
            </Button>
          </div>
        </div>

        {/* Main Content - Splitter-pane Layout */}
        <div className="flex-1 min-h-0 p-3 bg-gray-100 dark:bg-gray-950 overflow-hidden flex flex-col">

          {/* Canvas Container
              Base (first) display → embed-sized preview frame centered, plus
              an author-only sidebar on the left when editing the layout.
              Secondary displays → fill the available canvas area. */}
          {(() => {
            const activeDisplay = getActiveDisplay()
            if (!activeDisplay) return null

            const isBase = localData.layout?.displays[0]?.id === activeDisplay.id
            const isEditing = localData.layout?.editMode || false
            // Author-only Files sidebar appears on Base whenever the layout
            // itself doesn't already expose a student-facing Explorer panel.
            // Visible in both edit and view modes so authors can manage files
            // without entering layout edit mode.
            const showBaseSidebar = isBase && !displayHasPanelType(activeDisplay, "explorer")

            const outerClass = isBase
              ? "flex-1 min-h-0 overflow-hidden flex items-stretch justify-center gap-10 p-4"
              : "flex-1 min-h-0 overflow-hidden flex items-stretch justify-stretch"
            const containerClass = isBase
              ? "h-[600px] w-full max-w-[720px] self-center"
              : "w-full h-full"

            const canvas = (
              <div
                className={cn(
                  containerClass,
                  isEditing && "ring-2 ring-blue-500/40 ring-dashed rounded-lg",
                )}
              >
                <SplitterCanvas
                  root={activeDisplay.root}
                  renderLeaf={(leaf) => renderPanelContent(leaf, activeDisplay)}
                  editable={isEditing}
                  resizable
                  onSplitResize={handleSplitResize}
                  onMovePanel={handleMovePanel}
                  onRemovePanel={handleRemovePanel}
                />
              </div>
            )

            if (showBaseSidebar) {
              return (
                <div className={outerClass}>
                  <BaseAuthorSidebar
                    fileExplorerProps={{
                      files: localData.files,
                      folders: localData.folders || [],
                      activeFileId: localData.activeFileId,
                      onFileSelect: handleFileSelect,
                      onCreateFile: handleCreateFile,
                      onCreateFolder: handleCreateFolder,
                      onDeleteFile: handleDeleteFile,
                      onDeleteFolder: handleDeleteFolder,
                      onRenameFile: handleRenameFile,
                      onRenameFolder: handleRenameFolder,
                      onToggleFolder: handleToggleFolder,
                      onMoveFile: handleMoveFile,
                      onMoveFolder: handleMoveFolder,
                      onReorderFiles: handleReorderFiles,
                      onAddFileFromAsset: handleAddFileFromAsset,
                      onChangeFileType: handleChangeFileType,
                      onToggleFileVisibility: handleToggleFileVisibility,
                      onToggleFolderVisibility: handleToggleFolderVisibility,
                      onToggleFileReadonly: handleToggleFileReadonly,
                      onToggleFolderReadonly: handleToggleFolderReadonly,
                      onToggleFocusFolder: handleToggleFocusFolder,
                      onSetAllReadonly: handleSetAllReadonly,
                      onSetAllHidden: handleSetAllHidden,
                      onImportCollection: handleImportCollection,
                      onSaveAsCollection: handleSaveAsCollection,
                    }}
                  />
                  {canvas}
                </div>
              )
            }

            return <div className={outerClass}>{canvas}</div>
          })()}
        </div>

        {/* Footer */}
        <div className="p-4 border-t border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900">
          <div className="flex items-center gap-4">
            <div className="flex-1">
              <Label htmlFor="caption" className="text-sm font-medium">
                Caption:
              </Label>
              <Input
                id="caption"
                value={localData.caption || ""}
                onChange={(e) => handleDataChange({ caption: e.target.value })}
                placeholder="Optional caption"
                className="mt-1"
              />
            </div>

            <div className="flex gap-2">
              <Button 
                variant="outline" 
                onClick={handleCancelClick}
                disabled={localData.layout?.editMode || isSaving}
              >
                Cancel
              </Button>
              <Button 
                onClick={handleSaveClick} 
                className="flex items-center gap-2"
                disabled={localData.layout?.editMode || isSaving}
              >
                <Save className="h-4 w-4" />
                {isSaving ? "Saving..." : "Save"}
              </Button>
            </div>
          </div>
        </div>
    </BlockEditorShell>
  )
}
