import React from 'react';
import { Button } from '@/components/ui/button';
import { Upload } from 'lucide-react';

interface FileUploaderProps {
  accept?: string;
  multiple?: boolean;
  id?: string;
  onFileSelect: (file: File) => void;
}

export function FileUploader({
                               accept,
                               multiple,
                               id,
                               onFileSelect,
                             }: FileUploaderProps) {
  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      onFileSelect(e.target.files[0]);
    }
  };

  return (
    <div>
      <input
        type="file"
        id={id}
        accept={accept}
        multiple={multiple}
        onChange={handleFileChange}
        className="hidden"
      />
      <label htmlFor={id}>
        <Button variant="outline" className="cursor-pointer" asChild>
          <span>
            <Upload className="mr-2 h-4 w-4" />
            Choose File{multiple ? 's' : ''}
          </span>
        </Button>
      </label>
    </div>
  );
}
