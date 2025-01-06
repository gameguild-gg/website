import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"

interface ExportFilesModalProps {
  isOpen: boolean
  onClose: () => void
  onExportFiles: (exportAll: boolean, singleFileName?: string) => void
  activeFileName: string
  mode: 'light' | 'dark' | 'high-contrast'
}

export default function ExportFilesModal({ isOpen, onClose, onExportFiles, activeFileName, mode }: ExportFilesModalProps) {
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
    const baseStyles = 'w-full mb-2 px-4 py-2 rounded transition-colors duration-200'
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
      <DialogContent className={getDialogStyles()} aria-describedby="export-files-description">
        <DialogHeader>
          <DialogTitle className={mode === 'high-contrast' ? 'text-yellow-300' : ''}>Export Files</DialogTitle>
          <DialogDescription id="export-files-description" className={mode === 'high-contrast' ? 'text-yellow-200' : ''}>
            Choose whether to export the current file or all files in the project.
          </DialogDescription>
        </DialogHeader>
        <div className="py-4">
          <Button
            onClick={() => {
              onExportFiles(false, activeFileName)
              onClose()
            }}
            className={getButtonStyles('primary')}
          >
            Export Current File
          </Button>
          <Button
            onClick={() => {
              onExportFiles(true)
              onClose()
            }}
            className={getButtonStyles('secondary')}
          >
            Export All Files
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  )
}

