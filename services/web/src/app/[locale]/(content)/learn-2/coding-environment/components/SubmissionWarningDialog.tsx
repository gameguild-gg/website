import React from 'react'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import { AlertTriangle } from 'lucide-react'

interface SubmissionWarningDialogProps {
  isOpen: boolean
  onClose: () => void
  onSubmit: () => void
  conflicts: { fileName: string; reason: string }[]
  failedTests: number
  totalTests: number
  mode: 'light' | 'dark' | 'high-contrast'
}

export function SubmissionWarningDialog({ isOpen, onClose, onSubmit, conflicts, failedTests, totalTests, mode }: SubmissionWarningDialogProps) {
  const getBackgroundColor = () => {
    switch (mode) {
      case 'light': return 'bg-yellow-50'
      case 'dark': return 'bg-yellow-900'
      case 'high-contrast': return 'bg-black'
    }
  }

  const getTextColor = () => {
    switch (mode) {
      case 'light': return 'text-yellow-800'
      case 'dark': return 'text-yellow-200'
      case 'high-contrast': return 'text-yellow-300'
    }
  }

  const getBorderColor = () => {
    switch (mode) {
      case 'light': return 'border-yellow-400'
      case 'dark': return 'border-yellow-700'
      case 'high-contrast': return 'border-yellow-300'
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className={`${getBackgroundColor()} p-6 rounded-lg border-2 ${getBorderColor()}`} aria-describedby="submission-warning-description">
        <DialogHeader>
          <DialogTitle className={`text-xl font-bold flex items-center ${getTextColor()}`}>
            <AlertTriangle className="w-6 h-6 mr-2" />
            Submission Warning
          </DialogTitle>
          <DialogDescription id="submission-warning-description" className={getTextColor()}>
            The following issues need to be resolved before submission:
          </DialogDescription>
        </DialogHeader>
        <div className="py-4">
          <ul className={`list-disc pl-5 space-y-2 ${getTextColor()}`}>
            {conflicts.map((conflict, index) => (
              <li key={index} className="font-semibold">
                <span className="font-bold">{conflict.fileName}:</span> {conflict.reason}
              </li>
            ))}
          </ul>
        </div>
        <DialogFooter>
          <Button 
            onClick={onClose}
            className={`${
              mode === 'light' ? 'bg-gray-200 text-gray-800 hover:bg-gray-300' :
              mode === 'dark' ? 'bg-gray-700 text-gray-200 hover:bg-gray-600' :
              'bg-yellow-300 text-black hover:bg-yellow-400'
            }`}
          >
            Return
          </Button>
          <Button 
            onClick={onSubmit}
            className={`${
              mode === 'light' ? 'bg-yellow-500 text-white hover:bg-yellow-600' :
              mode === 'dark' ? 'bg-yellow-600 text-white hover:bg-yellow-700' :
              'bg-yellow-300 text-black hover:bg-yellow-400'
            }`}
          >
            Submit Anyway
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

