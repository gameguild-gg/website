import React from 'react'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/learn/ui/dialog"
import { Button } from "@/components/learn/ui/button"

interface RoleSelectionModalProps {
  isOpen: boolean
  onClose: () => void
  onSelectRole: (role: 'student' | 'teacher') => void
}

const RoleSelectionModal: React.FC<RoleSelectionModalProps> = ({ isOpen, onClose, onSelectRole }) => {
  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[425px]">
        <DialogHeader>
          <DialogTitle>Select Environment</DialogTitle>
          <DialogDescription>
            Choose which environment you want to access for this course.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-4 py-4">
          <Button onClick={() => onSelectRole('student')}>
            Student Environment
          </Button>
          <Button onClick={() => onSelectRole('teacher')}>
            Teacher Environment
          </Button>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export default RoleSelectionModal

