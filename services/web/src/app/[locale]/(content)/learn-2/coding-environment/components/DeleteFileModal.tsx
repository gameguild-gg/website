import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"

interface DeleteFileModalProps {
  isOpen: boolean
  onClose: () => void
  onDeleteFile: (name: string) => void
  activeFileName: string
  mode: 'light' | 'dark' | 'high-contrast'
}

export default function DeleteFileModal({ isOpen, onClose, onDeleteFile, activeFileName, mode }: DeleteFileModalProps) {
  const handleConfirmDelete = () => {
    onDeleteFile(activeFileName)
    onClose()
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

  const getButtonStyles = (variant: 'primary' | 'secondary') => {
    const baseStyles = 'px-4 py-2 rounded transition-colors duration-200'
    switch (mode) {
      case 'light':
        return variant === 'primary'
          ? `${baseStyles} bg-red-500 text-white hover:bg-red-600`
          : `${baseStyles} bg-gray-200 text-gray-800 hover:bg-gray-300`
      case 'dark':
        return variant === 'primary'
          ? `${baseStyles} bg-red-600 text-white hover:bg-red-700`
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
      <DialogContent className={getDialogStyles()} aria-describedby="delete-file-description">
        <DialogHeader>
          <DialogTitle className={mode === 'high-contrast' ? 'text-yellow-300' : ''}>Delete File</DialogTitle>
          <DialogDescription id="delete-file-description" className={mode === 'high-contrast' ? 'text-yellow-200' : ''}>
            Are you sure you want to delete the file "{activeFileName}"? This action cannot be undone.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button className={getButtonStyles('secondary')} onClick={onClose}>Cancel</Button>
          <Button className={getButtonStyles('primary')} onClick={handleConfirmDelete}>Delete</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

