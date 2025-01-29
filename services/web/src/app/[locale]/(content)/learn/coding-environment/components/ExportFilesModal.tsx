import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from "@/components/learn/ui/dialog"
import { Button } from "@/components/learn/ui/button"
import { Download, FolderOpen, File } from "lucide-react"

interface ExportFilesModalProps {
  isOpen: boolean
  onClose: () => void
  onExportFiles: (exportAll: boolean, singleFileName?: string) => void
  onExportOpenFiles: () => void
  activeFileName: string
  mode: "light" | "dark" | "high-contrast"
}

export default function ExportFilesModal({
  isOpen,
  onClose,
  onExportFiles,
  onExportOpenFiles,
  activeFileName,
  mode,
}: ExportFilesModalProps) {
  const getDialogStyles = () => {
    switch (mode) {
      case "light":
        return "bg-white text-gray-900"
      case "dark":
        return "bg-gray-800 text-gray-100"
      case "high-contrast":
        return "bg-black text-yellow-300 border-2 border-yellow-300"
      default:
        return ""
    }
  }

  const getButtonStyles = (variant: "primary" | "secondary") => {
    const baseStyles = "w-full mb-2 px-4 py-2 rounded transition-colors duration-200"
    switch (mode) {
      case "light":
        return variant === "primary"
          ? `${baseStyles} bg-blue-500 text-white hover:bg-blue-600`
          : `${baseStyles} bg-gray-200 text-gray-800 hover:bg-gray-300`
      case "dark":
        return variant === "primary"
          ? `${baseStyles} bg-blue-600 text-white hover:bg-blue-700`
          : `${baseStyles} bg-gray-700 text-gray-200 hover:bg-gray-600`
      case "high-contrast":
        return variant === "primary"
          ? `${baseStyles} bg-yellow-300 text-black hover:bg-yellow-400`
          : `${baseStyles} bg-gray-800 text-yellow-300 hover:bg-gray-700 border border-yellow-300`
      default:
        return ""
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className={getDialogStyles()} aria-describedby="export-files-description">
        <DialogHeader>
          <DialogTitle className={mode === "high-contrast" ? "text-yellow-300" : ""}>Export Files</DialogTitle>
          <DialogDescription
            id="export-files-description"
            className={mode === "high-contrast" ? "text-yellow-200" : ""}
          >
            Choose which files you want to export.
          </DialogDescription>
        </DialogHeader>
        <div className="flex justify-center gap-4 py-4">
          <Button
            onClick={() => {
              onExportFiles(false, activeFileName)
              onClose()
            }}
            className="flex flex-col items-center justify-center w-36 h-24 rounded transition-colors duration-200 bg-blue-500 text-white hover:bg-blue-600"
          >
            <File className="w-8 h-8 mb-2" />
            <span className="text-sm">Export Current File</span>
          </Button>
          <Button
            onClick={() => {
              onExportOpenFiles()
              onClose()
            }}
            className="flex flex-col items-center justify-center w-36 h-24 rounded transition-colors duration-200 bg-gray-200 text-gray-800 hover:bg-gray-300"
          >
            <FolderOpen className="w-8 h-8 mb-2" />
            <span className="text-sm">Export Open Files</span>
          </Button>
          <Button
            onClick={() => {
              onExportFiles(true)
              onClose()
            }}
            className="flex flex-col items-center justify-center w-36 h-24 rounded transition-colors duration-200 bg-gray-200 text-gray-800 hover:bg-gray-300"
          >
            <Download className="w-8 h-8 mb-2" />
            <span className="text-sm">Export All Files</span>
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  )
}

