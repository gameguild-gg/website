import React from 'react'
import { QuestionTypev1_0_0, QuestionStatus } from '@/interface-base/question.base.v1.0.0'
import { QuestionScore } from './QuestionScore'
import { QuestionStatusDisplay } from './QuestionStatusDisplay'

interface QuestionListProps {
  questions: QuestionTypev1_0_0[]
  onSelectQuestion: (question: QuestionTypev1_0_0) => void
  selectedQuestionId: number | undefined
  mode: 'light' | 'dark' | 'high-contrast'
  questionOrder: number[]
}

const QuestionList: React.FC<QuestionListProps> = ({ questions, onSelectQuestion, selectedQuestionId, mode, questionOrder }) => {
  return (
    <div className="space-y-1">
      {questions
        .filter(question => questionOrder.includes(question.id))
        .map((question) => {
          const questionNumber = questionOrder.indexOf(question.id) + 1;
          return (
            <div
              key={question.id}
              className={`cursor-pointer p-3 rounded-lg transition-colors duration-200 ${
                question.id === selectedQuestionId
                  ? mode === 'light'
                    ? 'bg-blue-100 text-blue-800'
                    : mode === 'dark'
                    ? 'bg-blue-900 text-blue-100'
                    : 'bg-yellow-300 text-black'
                  : mode === 'light'
                  ? 'bg-gray-50 hover:bg-gray-100'
                  : mode === 'dark'
                  ? 'bg-gray-700 hover:bg-gray-600'
                  : 'bg-gray-800 hover:bg-gray-700'
              }`}
              onClick={() => onSelectQuestion(question)}
            >
              <div className="flex items-center">
                <span className="font-bold mr-2">{questionNumber}.</span>
                <h3 className="font-semibold">
                  {question.title}
                </h3>
              </div>
              <div className="flex justify-between items-center text-sm mt-2">
                <QuestionScore score={question.score} mode={mode} />
                <QuestionStatusDisplay status={question.status} mode={mode} />
              </div>
            </div>
          );
        })}
    </div>
  )
}

export default QuestionList

