import React from 'react'

interface QuestionScoreProps {
  score: number[];
  mode: 'light' | 'dark' | 'high-contrast';
}

export const QuestionScore: React.FC<QuestionScoreProps> = ({ score, mode }) => (
  <span className={`px-2 py-1 rounded ${
    mode === 'light'
      ? 'bg-gray-200 text-gray-800'
      : mode === 'dark'
      ? 'bg-gray-600 text-gray-200'
      : 'bg-gray-700 text-yellow-200'
  }`}>
    Score: {score[1]} / {score[0]}
  </span>
)

