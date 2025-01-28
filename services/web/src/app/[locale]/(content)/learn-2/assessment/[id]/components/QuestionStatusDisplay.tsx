import React from 'react'
import { QuestionStatus } from '@/interface-base/question.base.v1.0.0'

interface QuestionStatusDisplayProps {
  status?: QuestionStatus;
  mode: 'light' | 'dark' | 'high-contrast';
}

export const QuestionStatusDisplay: React.FC<QuestionStatusDisplayProps> = ({ status, mode }) => {
  let statusText = 'Available'
  let statusClass = ''

  switch (status) {
    case QuestionStatus.Corrected:
      statusText = 'Corrected'
      statusClass = mode === 'light' ? 'bg-blue-200 text-blue-800' : mode === 'dark' ? 'bg-blue-800 text-blue-100' : 'bg-blue-500 text-white'
      break
    case QuestionStatus.Submitted:
      statusText = 'Submitted'
      statusClass = mode === 'light' ? 'bg-green-200 text-green-800' : mode === 'dark' ? 'bg-green-800 text-green-100' : 'bg-yellow-500 text-black'
      break
    default:
      statusClass = mode === 'light' ? 'bg-yellow-200 text-yellow-800' : mode === 'dark' ? 'bg-yellow-800 text-yellow-100' : 'bg-yellow-600 text-black'
  }

  return <span className={`px-2 py-1 rounded ${statusClass}`}>{statusText}</span>
}

