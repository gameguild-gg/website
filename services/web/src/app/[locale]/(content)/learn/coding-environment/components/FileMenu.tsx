import { useState, useRef, useEffect } from 'react'
import { ChevronDown, File, FolderOpen, Save, Edit, Trash2, FileUp, Download, RefreshCw } from 'lucide-react'
import NewFileModal from './NewFileModal'
import RenameFileModal from './RenameFileModal'
import DeleteFileModal from './DeleteFileModal'
import ImportFileButton from './ImportFileButton'
import ExportFilesModal from './ExportFilesModal'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"

interface FileMenuProps {
  mode: 'light' | 'dark' | 'high-contrast'
  onNewFile: (name: string, language: string) => void
  onRenameFile: (oldName: string, newName: string) => void
  onDeleteFile: (name: string) => void
  onImportFile: (file: File) => void
  onExportFiles: (exportAll: boolean, singleFileName?: string) => void
  activeFileName: string
  currentFileCount: number
  maxFiles: number
  onReset: () => void
}

export default function FileMenu({
  mode,
  onNewFile,
  onRenameFile,
  onDeleteFile,
  onImportFile,
  onExportFiles,
  activeFileName,
  currentFileCount,
  maxFiles,
  onReset
}: FileMenuProps) {
  const [isOpen, setIsOpen] = useState(false)
  const [isNewFileModalOpen, setIsNewFileModalOpen] = useState(false)
  const [isRenameFileModalOpen, setIsRenameFileModalOpen] = useState(false)
  const [isDeleteFileModalOpen, setIsDeleteFileModalOpen] = useState(false)
  const [isExportModalOpen, setIsExportModalOpen] = useState(false)
  const [isResetConfirmationOpen, setIsResetConfirmationOpen] = useState(false)
  const menuRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
        setIsOpen(false)
      }
    }

    document.addEventListener('mousedown', handleClickOutside)
    return () => {
      document.removeEventListener('mousedown', handleClickOutside)
    }
  }, [])

  const toggleMenu = () => setIsOpen(!isOpen)

  const getMenuItemClass = (mode: string) => `
    flex items-center px-4 py-2 text-sm w-full text-left transition-colors duration-200
    ${mode === 'light' 
      ? 'text-gray-700 hover:bg-gray-100' 
      : mode === 'dark' 
      ? 'text-gray-300 hover:bg-gray-700' 
      : 'text-yellow-300 hover:bg-yellow-900'}
  `

  const getMenuStyles = (mode: string) => {
    switch (mode) {
      case 'light':
        return 'bg-white border border-gray-200 shadow-lg'
      case 'dark':
        return 'bg-gray-800 border border-gray-700 shadow-lg'
      case 'high-contrast':
        return 'bg-black border border-yellow-300 shadow-lg'
      default:
        return ''
    }
  }

  const getDialogStyles = (mode: string) => {
    switch (mode) {
      case 'light':
        return 'bg-white border border-gray-200 shadow-lg'
      case 'dark':
        return 'bg-gray-800 border border-gray-700 shadow-lg'
      case 'high-contrast':
        return 'bg-black border border-yellow-300 shadow-lg'
      default:
        return ''
    }
  }

  const handleResetConfirm = () => {
    onReset()
    setIsResetConfirmationOpen(false)
  }

  return (
    <div className="relative" ref={menuRef}>
      <button
        onClick={toggleMenu}
        className={`flex items-center space-x-1 px-3 py-1 rounded transition-colors duration-200 ${
          mode === 'light'
            ? 'bg-gray-100 text-gray-700 hover:bg-gray-200'
            : mode === 'dark'
            ? 'bg-gray-700 text-gray-200 hover:bg-gray-600'
            : 'bg-yellow-300 text-black hover:bg-yellow-400'
        }`}
      >
        <span>File</span>
        <ChevronDown className="w-4 h-4" />
      </button>
      {isOpen && (
        <div className={`absolute left-0 mt-2 w-56 rounded-md ${getMenuStyles(mode)} ring-1 ring-black ring-opacity-5 z-10`}>
          <div className="py-1" role="menu" aria-orientation="vertical" aria-labelledby="options-menu">
            <button
              onClick={() => {
                setIsNewFileModalOpen(true)
                setIsOpen(false)
              }}
              className={`${getMenuItemClass(mode)} ${currentFileCount >= maxFiles ? 'opacity-50 cursor-not-allowed' : ''}`}
              disabled={currentFileCount >= maxFiles}
            >
              <File className="mr-2 h-4 w-4" />
              New File
            </button>
            <button
              onClick={() => {
                setIsRenameFileModalOpen(true)
                setIsOpen(false)
              }}
              className={getMenuItemClass(mode)}
            >
              <Edit className="mr-2 h-4 w-4" />
              Rename File
            </button>
            <button
              onClick={() => {
                setIsDeleteFileModalOpen(true)
                setIsOpen(false)
              }}
              className={getMenuItemClass(mode)}
            >
              <Trash2 className="mr-2 h-4 w-4" />
              Delete File
            </button>
            <ImportFileButton 
              onImportFile={onImportFile} 
              mode={mode} 
              disabled={currentFileCount >= maxFiles}
            />
            <button
              onClick={() => {
                setIsExportModalOpen(true)
                setIsOpen(false)
              }}
              className={getMenuItemClass(mode)}
            >
              <Download className="mr-2 h-4 w-4" />
              Export
            </button>
            <button
              onClick={() => setIsResetConfirmationOpen(true)}
              className={getMenuItemClass(mode)}
            >
              <RefreshCw className="mr-2 h-4 w-4" />
              Reset
            </button>
          </div>
        </div>
      )}
      <NewFileModal
        isOpen={isNewFileModalOpen}
        onClose={() => setIsNewFileModalOpen(false)}
        onNewFile={onNewFile}
        mode={mode}
        currentFileCount={currentFileCount}
        maxFiles={maxFiles}
      />
      <RenameFileModal
        isOpen={isRenameFileModalOpen}
        onClose={() => setIsRenameFileModalOpen(false)}
        onRenameFile={onRenameFile}
        activeFileName={activeFileName}
        mode={mode}
      />
      <DeleteFileModal
        isOpen={isDeleteFileModalOpen}
        onClose={() => setIsDeleteFileModalOpen(false)}
        onDeleteFile={onDeleteFile}
        activeFileName={activeFileName}
        mode={mode}
      />
      <ExportFilesModal
        isOpen={isExportModalOpen}
        onClose={() => setIsExportModalOpen(false)}
        onExportFiles={onExportFiles}
        activeFileName={activeFileName}
        mode={mode}
      />
      <Dialog open={isResetConfirmationOpen} onOpenChange={setIsResetConfirmationOpen}>
        <DialogContent className={getDialogStyles(mode)}>
          <DialogHeader>
            <DialogTitle>Confirm Reset</DialogTitle>
            <DialogDescription>
              Are you sure you want to reset? This action cannot be undone.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsResetConfirmationOpen(false)}>Cancel</Button>
            <Button onClick={handleResetConfirm}>Reset</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

