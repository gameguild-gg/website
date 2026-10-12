"use client"

import { Button } from "@game-guild/ui/components/button"

export interface ConfirmDialogProps {
  showConfirmDialog: boolean
  setShowConfirmDialog: (show: boolean) => void
  toggleFileStates: (fileId: string) => void
  activeFileId: string
  isPreview?: boolean
}

export function ConfirmDialog({
  showConfirmDialog,
  setShowConfirmDialog,
  toggleFileStates,
  activeFileId,
  isPreview = false,
}: ConfirmDialogProps) {
  // Don't render anything in preview mode or when dialog is not shown
  if (isPreview || !showConfirmDialog) return null

  return (
    <div className="absolute inset-0 bg-background/80 backdrop-blur-sm z-50 flex items-center justify-center">
      <div className="bg-background border rounded-lg shadow-lg p-4 w-80">
        <h3 className="text-lg font-medium mb-4">Confirm Action</h3>
        <p className="text-sm text-muted-foreground mb-4">
          Are you sure you want to make this file language-specific? This will create separate versions of the file for
          each programming language.
        </p>
        <div className="flex justify-end space-x-2">
          <Button variant="outline" onClick={() => { setShowConfirmDialog(false); }}>
            Cancel
          </Button>
          <Button
            onClick={() => {
              toggleFileStates(activeFileId)
              setShowConfirmDialog(false)
            }}
          >
            Confirm
          </Button>
        </div>
      </div>
    </div>
  )
}
