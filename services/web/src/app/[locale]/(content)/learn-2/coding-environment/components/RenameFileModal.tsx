import { useState, useEffect, useMemo } from 'react'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Button } from "@/components/ui/button"

interface RenameFileModalProps {
  isOpen: boolean
  onClose: () => void
  onRenameFile: (oldName: string, newName: string) => void
  activeFileName: string
  mode: 'light' | 'dark' | 'high-contrast'
}

export default function RenameFileModal({ isOpen, onClose, onRenameFile, activeFileName, mode }: RenameFileModalProps) {
  const [newFileName, setNewFileName] = useState(activeFileName)
  const [newExtension, setNewExtension] = useState('')

  const fileNameWithoutExtension = useMemo(() => {
    const parts = activeFileName.split('.')
    if (parts.length > 1) {
      setNewExtension('.' + parts.pop())
      return parts.join('.')
    }
    return activeFileName
  }, [activeFileName])

  useEffect(() => {
    setNewFileName(fileNameWithoutExtension)
  }, [activeFileName])

  const handleConfirmRename = () => {
    if (newFileName) {
      const newNameWithExtension = newFileName + newExtension
      if (newNameWithExtension !== activeFileName) {
        onRenameFile(activeFileName, newNameWithExtension)
      }
      onClose()
    }
  }

  const getDialogStyles = () => {
    switch (mode) {
      case 'light':
        return 'bg-white text-gray-900'
      case 'dark':
        return 'bg-gray-800 text-gray-100'
      case 'high-contrast':
        return 'bg-black text-yellow-300 border-2 border-yellow-300'
      default:
        return ''
    }
  }

  const getInputStyles = () => {
    switch (mode) {
      case 'light':
        return 'bg-white text-gray-900 border-gray-300'
      case 'dark':
        return 'bg-gray-700 text-gray-100 border-gray-600'
      case 'high-contrast':
        return 'bg-black text-yellow-300 border-yellow-300'
      default:
        return ''
    }
  }

  const getButtonStyles = (variant: 'primary' | 'secondary') => {
    const baseStyles = 'px-4 py-2 rounded transition-colors duration-200'
    switch (mode) {
      case 'light':
        return variant === 'primary'
          ? `${baseStyles} bg-blue-500 text-white hover:bg-blue-600`
          : `${baseStyles} bg-gray-200 text-gray-800 hover:bg-gray-300`
      case 'dark':
        return variant === 'primary'
          ? `${baseStyles} bg-blue-600 text-white hover:bg-blue-700`
          : `${baseStyles} bg-gray-700 text-gray-200 hover:bg-gray-600`
      case 'high-contrast':
        return variant === 'primary'
          ? `${baseStyles} bg-yellow-300 text-black hover:bg-yellow-400`
          : `${baseStyles} bg-gray-800 text-yellow-300 hover:bg-gray-700 border border-yellow-300`
      default:
        return ''
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className={getDialogStyles()} aria-describedby="rename-file-description">
        <DialogHeader>
          <DialogTitle className={mode === 'high-contrast' ? 'text-yellow-300' : ''}>Rename File</DialogTitle>
          <DialogDescription id="rename-file-description" className={mode === 'high-contrast' ? 'text-yellow-200' : ''}>
            Enter a new name for the file "{activeFileName}".
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-4 py-4">
          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="rename" className={`text-right ${mode === 'high-contrast' ? 'text-yellow-300' : ''}`}>
              New Name
            </Label>
            <Input
              id="rename"
              value={newFileName}
              onChange={(e) => setNewFileName(e.target.value)}
              className={`col-span-3 ${getInputStyles()}`}
            />
          </div>
        </div>
        <div className="grid gap-4 py-4">
          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="extension" className={`text-right ${mode === 'high-contrast' ? 'text-yellow-300' : ''}`}>
              Extension
            </Label>
            <Input
              id="extension"
              value={newExtension}
              onChange={(e) => setNewExtension(e.target.value)}
              className={`col-span-3 ${getInputStyles()}`}
              placeholder="Enter the new extension (e.g., .js)"
            />
          </div>
        </div>
        <DialogFooter>
          <Button className={getButtonStyles('primary')} onClick={handleConfirmRename}>Confirm</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

