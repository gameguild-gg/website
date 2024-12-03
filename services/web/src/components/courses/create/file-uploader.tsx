import { useState } from 'react'
import { Button } from "@/components/ui/button"

interface FileUploaderProps {
  onFileSelect: (file: File) => void
  accept: string
  label: string
  multiple?: boolean
}

export function FileUploader({ onFileSelect, accept, label, multiple = false }: FileUploaderProps) {
  const [selectedFiles, setSelectedFiles] = useState<File[]>([])

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files || [])
    setSelectedFiles(files)
    files.forEach(file => onFileSelect(file))
  }

  return (
    <div>
      <input
        type="file"
        accept={accept}
        onChange={handleFileChange}
        multiple={multiple}
        className="hidden"
        id={`file-upload-${label}`}
      />
      <Button asChild>
        <label htmlFor={`file-upload-${label}`}>{label}</label>
      </Button>
      {selectedFiles.length > 0 && (
        <ul className="mt-2">
          {selectedFiles.map((file, index) => (
            <li key={index}>{file.name}</li>
          ))}
        </ul>
      )}
    </div>
  )
}

