import React, { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import {
  QuestionTypev1_0_0,
  CodeQuestionv1_0_0,
  AnswerQuestionv1_0_0,
  QuestionStatus,
  MultipleChoiceQuestionv1_0_0,
  EssayQuestionv1_0_0,
} from '@/lib/interface-base/question.base.v1.0.0';
import Link from 'next/link';
import ReactMarkdown from 'react-markdown';
import { Checkbox } from '@/components/learn/ui/checkbox';
import { toast } from '@/components/learn/ui/use-toast';
import TeacherQuestionDetail from './TeacherQuestionDetail';
import RichTextEditor from '@/components/learn/RichTextEditor';

interface QuestionDetailProps {
  question: QuestionTypev1_0_0;
  assessmentId: number;
  userId: string | null;
  role: string | null;
  courseId: string | null;
  moduleId: string | null;
  mode: 'light' | 'dark' | 'high-contrast';
}

const QuestionDetail: React.FC<QuestionDetailProps> = ({
  question,
  assessmentId,
  userId,
  role,
  courseId,
  moduleId,
  mode,
}) => {
  const router = useRouter();
  const [selectedAnswers, setSelectedAnswers] = useState<string[]>([]);

  // Construct the local storage key
  const localStorageKey = `assessment_${assessmentId}_question_${question.id}_user_${userId}_role_${role}_course_${courseId}_module_${moduleId}_answer`;
  const localStorageKeyMultipleChoice = `assessment_${assessmentId}_question_${question.id}_user_${userId}_role_${role}_course_${courseId}_module_${moduleId}_selectedAnswers`;

  // Load saved answer from local storage
  const [answer, setAnswer] = useState(() => {
    if (typeof window !== 'undefined') {
      const savedAnswer = localStorage.getItem(localStorageKey);
      if (question.type === 'multiple-choice') {
        const savedSelectedAnswers = localStorage.getItem(
          localStorageKeyMultipleChoice,
        );
        if (savedSelectedAnswers) {
          try {
            setSelectedAnswers(JSON.parse(savedSelectedAnswers));
          } catch (error) {
            console.error('Error parsing saved selected answers:', error);
          }
        }
      }
      return savedAnswer || '';
    }
    return '';
  });

  useEffect(() => {
    if (typeof window !== 'undefined') {
      localStorage.setItem(localStorageKey, answer);
      if (question.type === 'multiple-choice') {
        localStorage.setItem(
          localStorageKeyMultipleChoice,
          JSON.stringify(selectedAnswers),
        );
      }
    }
  }, [answer, selectedAnswers, localStorageKey, localStorageKeyMultipleChoice]);

  const handleSubmit = async () => {
    try {
      let submission: any;

      if (question.type === 'code') {
        submission = {
          ...question,
          submittedCode: [answer],
          output: '',
          testResults: [],
          status: QuestionStatus.Submitted,
        };
      } else if (question.type === 'multiple-choice') {
        submission = {
          ...question,
          submittedAnswers: selectedAnswers,
          status: QuestionStatus.Submitted,
        };
      } else if (question.type === 'essay') {
        submission = {
          ...question,
          submittedEssay: answer,
          status: QuestionStatus.Submitted,
        };
      } else {
        submission = {
          ...question,
          submittedAnswer: answer,
          status: QuestionStatus.Submitted,
        };
      }

      console.log('Submitting answer:', submission);

      const response = await fetch('../../../../api/learn/submit-assignment', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(submission),
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(
          `Failed to submit answer: ${errorData.error || response.statusText}`,
        );
      }

      const result = await response.json();
      console.log('Submission result:', result);

      toast({
        title: 'Success',
        description: 'Your answer has been submitted successfully.',
      });

      router.push(
        `/learn/assessment/${assessmentId}?userId=${userId}&role=${role}&courseId=${courseId}&moduleId=${moduleId}`,
      );
    } catch (error: any) {
      console.error('Error submitting answer:', error);
      toast({
        title: 'Error',
        description:
          error.message || 'An error occurred while submitting your answer.',
        variant: 'destructive',
      });
    }
  };

  const renderCodeQuestion = (question: CodeQuestionv1_0_0) => (
    <>
      <h2 className="text-xl font-semibold mb-4">{question.title}</h2>
      <div className="prose mb-4">
        <ReactMarkdown>{question.description}</ReactMarkdown>
      </div>
      <div className="mb-4">
        <Link
          href={`/learn/coding-environment?id=${question.id}&type=question&userId=${userId}&role=${role}&courseId=${courseId}&moduleId=${moduleId}&assessmentId=${assessmentId}`}
        >
          <button className="bg-green-500 hover:bg-green-700 text-white font-bold py-2 px-4 rounded">
            Access Code Environment
          </button>
        </Link>
      </div>
    </>
  );

  const renderAnswerQuestion = (question: AnswerQuestionv1_0_0) => (
    <>
      <h2 className="text-xl font-semibold mb-4">{question.title}</h2>
      <div className="prose mb-4">
        <ReactMarkdown>{question.question}</ReactMarkdown>
      </div>
      <div className="mb-4">
        <h3 className="text-lg font-semibold mb-2">Your Response</h3>
        <textarea
          value={answer}
          onChange={(e) => setAnswer(e.target.value)}
          className="w-full px-3 py-2 text-gray-700 border rounded-lg focus:outline-none"
          rows={10}
          maxLength={question.maxLetters}
        />
        <p className="text-sm text-gray-500 mt-1">
          {answer.length}/{question.maxLetters} characters
        </p>
      </div>
      <button
        onClick={handleSubmit}
        className="bg-blue-500 hover:bg-blue-700 text-white font-bold py-2 px-4 rounded"
      >
        Submit
      </button>
    </>
  );

  const renderEssayQuestion = (question: EssayQuestionv1_0_0) => (
    <>
      <h2 className="text-xl font-semibold mb-4">{question.title}</h2>
      <div className="prose mb-4">
        <ReactMarkdown>{question.question}</ReactMarkdown>
      </div>
      <div className="mb-4">
        <h3 className="text-lg font-semibold mb-2">Your Response</h3>
        <RichTextEditor value={answer} onChange={setAnswer} mode={mode} />
        <p className="text-sm text-gray-500 mt-1">
          Word count:{' '}
          {answer.split(/\s+/).filter((word) => word.length > 0).length} /{' '}
          {question.maxWords} words
        </p>
      </div>
      <button
        onClick={handleSubmit}
        className="bg-blue-500 hover:bg-blue-700 text-white font-bold py-2 px-4 rounded"
      >
        Submit
      </button>
    </>
  );

  const renderMultipleChoiceQuestion = (
    question: MultipleChoiceQuestionv1_0_0,
  ) => {
    const maxSelections = (question as MultipleChoiceQuestionv1_0_0)
      .correctAnswers.length;

    const handleCheckboxChange = (answerId: string) => {
      setSelectedAnswers((prevSelected) => {
        if (prevSelected.includes(answerId)) {
          return prevSelected.filter((id) => id !== answerId);
        } else if (prevSelected.length < maxSelections) {
          return [...prevSelected, answerId];
        } else {
          toast({
            title: 'Selection limit reached',
            description: `You can only select up to ${maxSelections} answer${maxSelections > 1 ? 's' : ''}.`,
            variant: 'default',
          });
          return prevSelected;
        }
      });
    };

    return (
      <>
        <h2 className="text-xl font-semibold mb-4">{question.title}</h2>
        <div className="prose mb-4">
          <ReactMarkdown>{question.question}</ReactMarkdown>
        </div>
        <div className="mb-2 text-sm text-gray-500">
          Select up to {maxSelections} answer{maxSelections > 1 ? 's' : ''}.
        </div>
        <div className="mb-4">
          {question.answers.map((answer) => (
            <div key={answer.id} className="flex items-center mb-2">
              <Checkbox
                id={`answer-${answer.id}`}
                checked={selectedAnswers.includes(answer.id)}
                onCheckedChange={() => handleCheckboxChange(answer.id)}
              />
              <label htmlFor={`answer-${answer.id}`} className="ml-2">
                {answer.text}
              </label>
            </div>
          ))}
        </div>
        <button
          onClick={handleSubmit}
          className="bg-blue-500 hover:bg-blue-700 text-white font-bold py-2 px-4 rounded"
        >
          Submit
        </button>
      </>
    );
  };

  if (role === 'teacher') {
    return (
      <TeacherQuestionDetail
        question={question}
        mode={mode}
        courseId={courseId!}
      />
    );
  }

  return (
    <div
      className={`bg-white p-6 rounded-lg shadow-md ${
        mode === 'dark'
          ? 'bg-gray-800 text-gray-100'
          : mode === 'high-contrast'
            ? 'bg-black text-yellow-300'
            : ''
      }`}
    >
      {question.type === 'code'
        ? renderCodeQuestion(question as CodeQuestionv1_0_0)
        : question.type === 'multiple-choice'
          ? renderMultipleChoiceQuestion(
              question as MultipleChoiceQuestionv1_0_0,
            )
          : question.type === 'essay'
            ? renderEssayQuestion(question as EssayQuestionv1_0_0)
            : renderAnswerQuestion(question as AnswerQuestionv1_0_0)}
    </div>
  );
};

export default QuestionDetail;
