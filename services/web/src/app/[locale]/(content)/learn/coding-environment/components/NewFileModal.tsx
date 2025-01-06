import { useState, useEffect } from 'react'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Button } from "@/components/ui/button"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { toast } from "@/components/ui/use-toast"

interface NewFileModalProps {
  isOpen: boolean;
  onClose: () => void;
  onNewFile: (name: string, language: string) => void;
  mode: 'light' | 'dark' | 'high-contrast';
  currentFileCount: number;
  maxFiles: number;
}

const languageExtensions: { [key: string]: string } = {
  javascript: 'js',
  typescript: 'ts',
  jsx: 'jsx',
  tsx: 'tsx',
  python: 'py',
  text: 'txt',
  lua: 'lua',
  php: 'php',
  ruby: 'rb',
  rust: 'rs',
  cpp: 'cpp',
  c: 'c',
  csharp: 'cs',
  java: 'java',
  html: 'html',
  css: 'css',
  markdown: 'md',
  json: 'json',
  yaml: 'yaml',
  xml: 'xml'
}

export default function NewFileModal({ isOpen, onClose, onNewFile, mode, currentFileCount, maxFiles }: NewFileModalProps) {
  const [newFileName, setNewFileName] = useState('')
  const [newFileLanguage, setNewFileLanguage] = useState('javascript')
  const [showLimitWarning, setShowLimitWarning] = useState(false)

  useEffect(() => {
    if (isOpen && currentFileCount >= maxFiles) {
      setShowLimitWarning(true)
    } else {
      setShowLimitWarning(false)
    }
  }, [isOpen, currentFileCount, maxFiles])

  const handleConfirmNewFile = () => {
    if (currentFileCount >= maxFiles) {
      setShowLimitWarning(true)
      return
    }

    if (newFileName) {
      const extension = languageExtensions[newFileLanguage] || ''
      const fullFileName = `${newFileName}${extension ? `.${extension}` : ''}`
      onNewFile(fullFileName, newFileLanguage)
      onClose()
      setNewFileName('')
      setNewFileLanguage('javascript')
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

  const getSelectStyles = () => {
    switch (mode) {
      case 'light':
        return 'bg-white text-gray-900 border-gray-300'
      case 'dark':
        return 'bg-gray-800 text-gray-100 border-gray-600' 
      case 'high-contrast':
        return 'bg-black text-yellow-300 border-yellow-300'
      default:
        return ''
    }
  }

  const getSelectContentStyles = () => {
    switch (mode) {
      case 'light':
        return 'bg-white text-gray-900'
      case 'dark':
        return 'bg-gray-900 text-gray-100' 
      case 'high-contrast':
        return 'bg-black text-yellow-300 border border-yellow-300'
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

  if (showLimitWarning) {
    return (
      <Dialog open={isOpen} onOpenChange={onClose}>
        <DialogContent className={getDialogStyles()}>
          <DialogHeader>
            <DialogTitle className={mode === 'high-contrast' ? 'text-yellow-300' : ''}>File Limit Reached</DialogTitle>
          </DialogHeader>
          <div className="py-6">
            <p className={`text-center ${mode === 'high-contrast' ? 'text-yellow-300' : 'text-red-500'}`}>
              You have reached the maximum number of files ({maxFiles}) allowed for this exercise.
            </p>
            <p className={`text-center mt-2 ${mode === 'high-contrast' ? 'text-yellow-200' : ''}`}>
              Please delete an existing file before creating a new one.
            </p>
          </div>
          <DialogFooter>
            <Button className={getButtonStyles('primary')} onClick={onClose}>Close</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    )
  }

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className={getDialogStyles()} aria-describedby="new-file-description">
        <DialogHeader>
          <DialogTitle className={mode === 'high-contrast' ? 'text-yellow-300' : ''}>Create New File</DialogTitle>
          <DialogDescription id="new-file-description" className={mode === 'high-contrast' ? 'text-yellow-200' : ''}>
            Enter a name for your new file and select its language.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-4 py-4">
          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="name" className={`text-right ${mode === 'high-contrast' ? 'text-yellow-300' : ''}`}>
              Name
            </Label>
            <Input
              id="name"
              value={newFileName}
              onChange={(e) => setNewFileName(e.target.value)}
              className={`col-span-3 ${getInputStyles()}`}
              placeholder={`Enter file name (e.g., myfile.${languageExtensions[newFileLanguage] || ''})`}
            />
          </div>
          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="language" className={`text-right ${mode === 'high-contrast' ? 'text-yellow-300' : ''}`}>
              Language
            </Label>
            <Select
              value={newFileLanguage}
              onValueChange={setNewFileLanguage}
            >
              <SelectTrigger className={`col-span-3 ${getSelectStyles()}`}>
                <SelectValue placeholder="Select a language" />
              </SelectTrigger>
              <SelectContent className={getSelectContentStyles()}>
                {Object.entries(languageExtensions).map(([lang, ext]) => (
                  <SelectItem 
                    key={lang} 
                    value={lang}
                    className={`
                      ${mode === 'high-contrast' ? 'text-yellow-300 hover:bg-gray-800' : ''}
                      ${mode === 'dark' ? 'text-gray-100 hover:bg-gray-700' : ''}
                    `}
                  >
                    {lang.charAt(0).toUpperCase() + lang.slice(1)} (.{ext})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>
        <DialogFooter>
          <Button className={getButtonStyles('primary')} onClick={handleConfirmNewFile}>Confirm</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

