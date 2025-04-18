import React from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/learn/ui/dialog"
import { Button } from "@/components/learn/ui/button"

interface ConfirmSubmissionDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: () => void;
  mode: 'light' | 'dark' | 'high-contrast';
}

export function ConfirmSubmissionDialog({ isOpen, onClose, onConfirm, mode }: ConfirmSubmissionDialogProps) {
  const getDialogStyles = () => {
    switch (mode) {
      case 'light':
        return 'bg-white text-gray-800'
      case 'dark':
        return 'bg-gray-800 text-gray-200'
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

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className={`${getDialogStyles()} p-6 rounded-lg`} aria-describedby="confirm-submission-description">
        <DialogHeader>
          <DialogTitle className={`text-xl font-bold ${
            mode === 'high-contrast' ? 'text-yellow-300' : ''
          }`}>Confirm Submission</DialogTitle>
          <DialogDescription id="confirm-submission-description" className={`${
            mode === 'light' ? 'text-gray-600' :
            mode === 'dark' ? 'text-gray-400' :
            'text-yellow-200'
          }`}>
            Are you sure you want to submit your assignment?
          </DialogDescription>
        </DialogHeader>
        <DialogFooter className="mt-6 flex justify-end space-x-2">
          <Button 
            variant="outline" 
            onClick={onClose}
            className={getButtonStyles('secondary')}
          >
            No
          </Button>
          <Button 
            onClick={onConfirm}
            className={getButtonStyles('primary')}
          >
            Yes
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

