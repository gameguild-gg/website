'use client'

import { useState, useEffect, useCallback } from 'react'
import { useRouter, useSearchParams } from 'next/navigation'
import Link from 'next/link'
import { AssessmentBasev1_0_0 } from '@/lib/interface-base/assessment.base.v1.0.0'
import { QuestionBasev1_0_0, QuestionStatus } from '@/lib/interface-base/question.base.v1.0.0'
import QuestionList from './components/QuestionList'
import QuestionDetail from './components/QuestionDetail'
import { Sun, Moon, ZapOff, ChevronLeft } from 'lucide-react'
import { HierarchyBasev1_0_0 } from '@/lib/interface-base/structure.base.v1.0.0'
import PageHeader from '@/components/learn/PageHeader'

export default function AssessmentPage({ params }: { params: { id: string } }) {
  const [assessment, setAssessment] = useState<AssessmentBasev1_0_0 | null>(null)
  const [allQuestions, setAllQuestions] = useState<QuestionBasev1_0_0[]>([])
  const [currentTabQuestions, setCurrentTabQuestions] = useState<QuestionBasev1_0_0[]>([])
  const [selectedQuestion, setSelectedQuestion] = useState<QuestionBasev1_0_0 | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const router = useRouter()
  const searchParams = useSearchParams()
  const userId = searchParams.get('userId')
  const role = searchParams.get('role')
  const courseId = searchParams.get('courseId')
  const moduleId = searchParams.get('moduleId')
  const [mode, setMode] = useState<'light' | 'dark' | 'high-contrast'>('dark')
  const [activeTab, setActiveTab] = useState(0);

  const fetchData = useCallback(async () => {
    try {
      setIsLoading(true)

      const assessmentResponse = await fetch(`../../../api/learn/content/assessment/${params.id}`)
      if (!assessmentResponse.ok) {
        throw new Error(`Failed to fetch assessment: ${assessmentResponse.statusText}`)
      }
      const assessmentData: AssessmentBasev1_0_0 = await assessmentResponse.json()
      setAssessment(assessmentData)

      // Collect all unique question IDs from all tabs
      const uniqueQuestionIds = Array.from(new Set(assessmentData.idQuestions.flatMap(tab => tab.questions)));
      
      const questionPromises = uniqueQuestionIds.map((id: number) =>
        fetch(`../../../api/learn/content/question/${id}`).then(res => {
          if (!res.ok) {
            throw new Error(`Failed to fetch question ${id}: ${res.statusText}`)
          }
          return res.json()
        })
      );
      const allQuestionData: QuestionBasev1_0_0[] = await Promise.all(questionPromises)
      setAllQuestions(allQuestionData)

      updateCurrentTabQuestions(assessmentData, allQuestionData, activeTab)
    } catch (error) {
      console.error('Error fetching assessment and questions:', error)
      setError(error instanceof Error ? error.message : 'An unknown error occurred')
    } finally {
      setIsLoading(false)
    }
  }, [params.id])

  useEffect(() => {
    fetchData()
  }, [params.id])

  const updateCurrentTabQuestions = (assessmentData: AssessmentBasev1_0_0, allQuestions: QuestionBasev1_0_0[], tabIndex: number) => {
    const currentTabQuestionIds = assessmentData.idQuestions[tabIndex].questions;
    const currentTabQuestions = currentTabQuestionIds.map(id => 
      allQuestions.find(q => q.id === id)
    ).filter((q): q is QuestionBasev1_0_0 => q !== undefined);
    setCurrentTabQuestions(currentTabQuestions);
  }

  useEffect(() => {
    const storedMode = localStorage.getItem('colorMode') as 'light' | 'dark' | 'high-contrast' | null
    if (storedMode) {
      setMode(storedMode)
    }
  }, [])

  useEffect(() => {
    localStorage.setItem('colorMode', mode)
  }, [mode])

  const handleQuestionSelect = (question: QuestionBasev1_0_0) => {
    setSelectedQuestion(question)
  }

  const toggleMode = () => {
    const newMode = mode === 'light' ? 'dark' : mode === 'dark' ? 'high-contrast' : 'light'
    setMode(newMode)
  }

  const handleTabChange = useCallback((index: number) => {
    setActiveTab(index);
    setSelectedQuestion(null); // Deselect question when changing tabs
    if (assessment) {
      updateCurrentTabQuestions(assessment, allQuestions, index);
    }
  }, [assessment, allQuestions]);

  useEffect(() => {
    if (assessment && allQuestions.length > 0) {
      updateCurrentTabQuestions(assessment, allQuestions, activeTab);
    }
  }, [activeTab, assessment, allQuestions]);

  if (isLoading) {
    return <div className="min-h-screen bg-gray-100 p-8">Loading assessment and questions...</div>
  }

  if (error) {
    return (
      <div className="min-h-screen bg-gray-100 p-8">
        <h1 className="text-2xl font-bold mb-4 text-red-600">Error</h1>
        <p className="text-red-500">{error}</p>
        <div className="mt-4">
          <Link href={`/learn/course/${courseId}?userId=${userId}&role=${role}`}>
            <span className="text-blue-500 hover:underline">Return to Course</span>
          </Link>
        </div>
      </div>
    )
  }

  if (!assessment) {
    return <div className="min-h-screen bg-gray-100 p-8">Assessment not found.</div>
  }

  const currentTab = assessment.idQuestions[activeTab];

  // Calculate total score and question count across all tabs
  const totalScore = assessment.idQuestions.reduce((sum, tab) => 
    sum + tab.questions.reduce((tabSum, qId) => {
      const question = allQuestions.find(q => q.id === qId);
      return tabSum + (question ? question.score[0] : 0);
    }, 0), 0);

  const totalCurrentScore = assessment.idQuestions.reduce((sum, tab) => 
    sum + tab.questions.reduce((tabSum, qId) => {
      const question = allQuestions.find(q => q.id === qId);
      return tabSum + (question ? question.score[1] : 0);
    }, 0), 0);

  const totalQuestions = assessment.idQuestions.reduce((sum, tab) => sum + tab.questions.length, 0);

  return (
    <div className={`min-h-screen p-8 ${
      mode === 'light' ? 'bg-gray-100 text-gray-900' :
      mode === 'dark' ? 'bg-gray-800 text-gray-100' :
      'bg-black text-yellow-300'
    }`}>
      <div className="max-w-5xl mx-auto">
        <PageHeader
          title={assessment.title}
          backLink={`/learn/course/${courseId}?userId=${userId}&role=${role}`}
          userId={userId}
          mode={mode}
          onModeToggle={toggleMode}
        />

        <div className="bg-white dark:bg-gray-700 high-contrast:bg-black p-4 rounded-lg shadow-md mb-8">
          <p className="text-lg dark:text-gray-300 high-contrast:text-yellow-300 mb-4">{assessment.description}</p>
          <p className="text-gray-700 dark:text-gray-300 high-contrast:text-yellow-300 mb-2">
            Total Score: {totalScore} ({totalCurrentScore} currently)
          </p>
          <p className="text-gray-700 dark:text-gray-300 high-contrast:text-yellow-300 mb-2">
            Questions: {totalQuestions}
          </p>
          <p className="text-gray-700 dark:text-gray-300 high-contrast:text-yellow-300">
            Subjects: {Array.from(new Set(allQuestions.flatMap(q => q.subject))).join(', ')}
          </p>
          {assessment.scoreTotal && (
            <div className="mt-4">
              <h3 className="text-xl font-bold text-gray-700 dark:text-gray-300 high-contrast:text-yellow-300 mb-2">
                Score Deadlines:
              </h3>
              <ul className="list-disc list-inside">
                {assessment.scoreTotal.map((score, index) => {
                  const discountedScore = totalScore * (score.discount[0] / 100);
                  const scoreLoss = totalScore - discountedScore;
                  return (
                    <li key={index} className="text-gray-700 dark:text-gray-300 high-contrast:text-yellow-300 mb-2">
                      {new Date(score.maxDate).toLocaleDateString()}: 
                      <span className="font-semibold"> {discountedScore.toFixed(2)} </span>
                      (Discount: {score.discount[1]}%, Score loss: 
                      <span className="font-semibold text-red-500 dark:text-red-400 high-contrast:text-red-300"> {scoreLoss.toFixed(2)}</span>)
                    </li>
                  );
                })}
              </ul>
            </div>
          )}
        </div>

        <div className="mt-4 mb-0">
          <div className="flex space-x-4">
            {assessment?.idQuestions.map((tab, index) => (
              <button
                key={index}
                onClick={() => handleTabChange(index)}
                className={`px-4 py-2 rounded-t-md border-b-2 ${
                  activeTab === index
                    ? 'bg-white border-blue-500'
                    : 'bg-gray-200 dark:bg-gray-700 high-contrast:bg-gray-900 text-gray-800 dark:text-gray-300 high-contrast:text-yellow-300 border-transparent hover:bg-gray-300 dark:hover:bg-gray-600 high-contrast:hover:bg-gray-800'
                }`}
              >
                {tab.title}
              </button>
            ))}
          </div>
        </div>

        <div className={`flex flex-col md:flex-row gap-8 bg-white dark:bg-gray-800 high-contrast:bg-black rounded-b-lg shadow-lg overflow-hidden border-t border-gray-200 dark:border-gray-600 high-contrast:border-yellow-300`}>
          <div className="w-full md:w-1/3 p-6 border-r border-gray-200 dark:border-gray-600 high-contrast:border-yellow-300">
            <h2 className="text-md font-semibold mb-4">Questions</h2>
            <QuestionList
              questions={currentTabQuestions}
              onSelectQuestion={handleQuestionSelect}
              selectedQuestionId={selectedQuestion?.id}
              mode={mode}
              questionOrder={assessment.idQuestions[activeTab].questions}
            />
          </div>
          <div className="w-full md:w-2/3 p-8">
            {selectedQuestion ? (
              <QuestionDetail
                question={selectedQuestion}
                assessmentId={assessment.id}
                userId={userId}
                role={role}
                courseId={courseId}
                moduleId={moduleId}
                mode={mode}
              />
            ) : (
              <div className={`p-6 rounded-lg ${
                mode === 'light' ? 'bg-gray-50 text-gray-600' :
                mode === 'dark' ? 'bg-gray-600 text-gray-200' :
                'bg-gray-900 text-yellow-200'
              }`}>
                <p>Select a question to view its details and submit your answer.</p>
              </div>
            )}
          </div>
        </div>

        <div className="mt-8">
          <Link href={`/learn/course/${courseId}?userId=${userId}&role=${role}`}>
            <button className={`w-full md:w-auto font-bold py-2 px-4 rounded transition-colors duration-200 ${
              mode === 'light' ? 'bg-blue-500 hover:bg-blue-700 text-white' :
              mode === 'dark' ? 'bg-blue-700 hover:bg-blue-900 text-white' :
              'bg-yellow-500 hover:bg-yellow-700 text-black'
            }`}>
              Return to Course
            </button>
          </Link>
        </div>
      </div>
    </div>
  )
}

