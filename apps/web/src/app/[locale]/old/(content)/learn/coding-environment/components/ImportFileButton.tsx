import { useRef } from 'react'
import { FileUp } from 'lucide-react'

interface ImportFileButtonProps {
  onImportFile: (file: File) => void
  mode: 'light' | 'dark' | 'high-contrast'
  disabled: boolean
}

const getMenuItemClass = (mode: string, disabled: boolean) => `
  flex items-center px-4 py-2 text-sm w-full text-left transition-colors duration-200
  ${mode === 'light' 
    ? 'text-gray-700 hover:bg-gray-100' 
    : mode === 'dark' 
    ? 'text-gray-300 hover:bg-gray-700' 
    : 'text-yellow-300 hover:bg-yellow-900'}
  ${disabled ? 'opacity-50 cursor-not-allowed' : ''}
`

export default function ImportFileButton({ onImportFile, mode, disabled }: ImportFileButtonProps) {
  const fileInputRef = useRef<HTMLInputElement>(null)

  const handleImportClick = () => {
    if (!disabled) {
      fileInputRef.current?.click()
    }
  }

  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (file) {
      onImportFile(file)
    }
  }

  return (
    <>
      <button
        onClick={handleImportClick}
        className={getMenuItemClass(mode, disabled)}
        disabled={disabled}
      >
        <FileUp className="mr-2 h-4 w-4" />
        Import File
      </button>
      <input
        type="file"
        ref={fileInputRef}
        onChange={handleFileChange}
        style={{ display: 'none' }}
        accept=".js,.jsx,.ts,.tsx,.py,.java,.cpp,.c,.go,.html,.css,.md,.json,.yaml,.xml,.rb,.rs,.cs"
      />
    </>
  )
}

