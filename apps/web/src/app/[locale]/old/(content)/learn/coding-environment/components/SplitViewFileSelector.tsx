import React from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from "@/components/learn/ui/dialog"
import { Button } from "@/components/learn/ui/button"

interface SplitViewFileSelectorProps {
  isOpen: boolean;
  onClose: () => void;
  onSelectFile: (fileName: string) => void;
  files: { name: string; language: string }[];
  mode: 'light' | 'dark' | 'high-contrast';
}

export function SplitViewFileSelector({ isOpen, onClose, onSelectFile, files, mode }: SplitViewFileSelectorProps) {
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

  const getButtonStyles = (isActive: boolean) => {
    const baseStyles = 'w-full px-4 py-2 mb-2 rounded transition-colors duration-200 text-left'
    switch (mode) {
      case 'light':
        return isActive
          ? `${baseStyles} bg-blue-500 text-white hover:bg-blue-600`
          : `${baseStyles} bg-gray-200 text-gray-800 hover:bg-gray-300`
      case 'dark':
        return isActive
          ? `${baseStyles} bg-blue-600 text-white hover:bg-blue-700`
          : `${baseStyles} bg-gray-700 text-gray-200 hover:bg-gray-600`
      case 'high-contrast':
        return isActive
          ? `${baseStyles} bg-yellow-300 text-black hover:bg-yellow-400`
          : `${baseStyles} bg-gray-800 text-yellow-300 hover:bg-gray-700 border border-yellow-300`
      default:
        return baseStyles
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className={`${getDialogStyles()} p-6 rounded-lg`}>
        <DialogHeader>
          <DialogTitle className={`text-xl font-bold ${
            mode === 'high-contrast' ? 'text-yellow-300' : ''
          }`}>Select File for Split View</DialogTitle>
          <DialogDescription className={`${
            mode === 'light' ? 'text-gray-600' :
            mode === 'dark' ? 'text-gray-400' :
            'text-yellow-200'
          }`}>
            Choose a file to display in the split view alongside the main editor.
          </DialogDescription>
        </DialogHeader>
        <div className="mt-4">
          {files.map((file) => (
            <Button
              key={file.name}
              onClick={() => {
                onSelectFile(file.name);
                onClose();
              }}
              className={getButtonStyles(false)}
            >
              {file.name}
            </Button>
          ))}
        </div>
      </DialogContent>
    </Dialog>
  );
}

