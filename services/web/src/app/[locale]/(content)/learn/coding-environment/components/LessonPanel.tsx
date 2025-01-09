import React, { useState } from 'react'
import ReactMarkdown from 'react-markdown'
import { CodeQuestionv1_0_0 } from '@/lib/interface-base/question.base.v1.0.0'
import { Button } from '@/components/learn/ui/button'
import CustomScrollbar from './CustomScrollbar'
import { HierarchyBasev1_0_0 } from '@/lib/interface-base/structure.base.v1.0.0'; // Import HierarchyBasev1_0_0

interface LessonPanelProps {
  codeQuestion: CodeQuestionv1_0_0
  onReturn: () => void
  onSubmit: () => void
  onReset: () => void
  mode: 'light' | 'dark' | 'high-contrast'
  isSubmitDisabled: boolean
  hierarchy: HierarchyBasev1_0_0 // Added hierarchy
}

export default function LessonPanel({ codeQuestion, onReturn, onSubmit, onReset, mode, isSubmitDisabled, hierarchy }: LessonPanelProps) { // Updated destructuring
  const shouldHideSubmit = codeQuestion.rules.includes('submit: n');

  // const [isResetDialogOpen, setIsResetDialogOpen] = useState(false)

  // const handleResetClick = () => {
  //   setIsResetDialogOpen(true)
  // }

  // const handleResetConfirm = () => {
  //   setIsResetDialogOpen(false)
  //   onReset()
  // }

  return (
    <div className={`flex flex-col h-full ${
      mode === 'light' ? 'bg-white text-gray-800 border-gray-300' :
      mode === 'dark' ? 'bg-gray-900 text-gray-100 border-gray-700' :
      'bg-black text-yellow-300 border-yellow-300'
    }`}>
      <div className="p-4 border-b flex justify-between">
        <Button onClick={onReturn} variant="outline" size="sm" className={`
          ${mode === 'light' ? 'bg-gray-200 text-gray-800 hover:bg-gray-300' :
            mode === 'dark' ? 'bg-gray-700 text-gray-100 hover:bg-gray-600' :
            'bg-yellow-300 text-black hover:bg-yellow-400 border-2 border-yellow-300'}
        `}>
          Return
        </Button>
        {/* <Button onClick={handleResetClick} variant="destructive" size="sm" className={`
          ${mode === 'light' ? 'bg-red-500 text-white hover:bg-red-600' :
            mode === 'dark' ? 'bg-red-600 text-white hover:bg-red-700' :
            'bg-yellow-300 text-black hover:bg-yellow-400 border-2 border-yellow-300'}
        `}>
          Reset
        </Button> */}
      </div>
      <CustomScrollbar mode={mode} className="flex-grow">
        <div className="p-4">
          <h1 className="text-2xl font-bold mb-4">{codeQuestion.title}</h1>
          <ReactMarkdown className={`prose max-w-none ${
            mode === 'light' ? 'prose-gray' :
            mode === 'dark' ? 'prose-invert' :
            'prose-yellow prose-invert'
          }`}>{codeQuestion.description}</ReactMarkdown>
        </div>
      </CustomScrollbar>
      <div className="p-4 border-t">
        {!shouldHideSubmit && (
          <Button 
            onClick={onSubmit} 
            className={`w-full mb-20 ${
              mode === 'light' ? 'bg-blue-500 hover:bg-blue-600 text-white' :
              mode === 'dark' ? 'bg-blue-600 hover:bg-blue-700 text-white' :
              'bg-yellow-300 hover:bg-yellow-400 text-black'
            }`}
            disabled={isSubmitDisabled}
          >
            Submit
          </Button>
        )}
      </div>
      {/* <ResetConfirmationDialog
        isOpen={isResetDialogOpen}
        onClose={() => setIsResetDialogOpen(false)}
        onConfirm={handleResetConfirm}
        mode={mode}
      /> */}
    </div>
  )
}

