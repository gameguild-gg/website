import React from 'react'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/learn/ui/dialog"
import { Button } from "@/components/learn/ui/button"

interface ResetConfirmationDialogProps {
  isOpen: boolean
  onClose: () => void
  onConfirm: () => void
  mode: 'light' | 'dark' | 'high-contrast'
}

export function ResetConfirmationDialog({ isOpen, onClose, onConfirm, mode }: ResetConfirmationDialogProps) {
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
      <DialogContent className={getDialogStyles()} aria-describedby="reset-description">
        <DialogHeader>
          <DialogTitle className={mode === 'high-contrast' ? 'text-yellow-300' : ''}>Confirm Reset</DialogTitle>
          <DialogDescription id="reset-description" className={mode === 'high-contrast' ? 'text-yellow-200' : ''}>
            Are you sure you want to reset to the default? You will lose all your current work.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button className={getButtonStyles('secondary')} onClick={onClose}>No</Button>
          <Button className={getButtonStyles('primary')} onClick={onConfirm}>Yes</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

