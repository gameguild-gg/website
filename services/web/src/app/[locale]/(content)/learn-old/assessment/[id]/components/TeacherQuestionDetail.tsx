import React, { useState, useEffect, useMemo } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import {
  QuestionTypev1_0_0,
  CodeQuestionv1_0_0,
  AnswerQuestionv1_0_0,
  MultipleChoiceQuestionv1_0_0,
  QuestionStatus,
  EssayQuestionv1_0_0,
} from '@/lib/interface-base/question.base.v1.0.0';
import { UserListQuestionv1_0_0 } from '@/lib/interface-base/user.list.question.v1.0.0';
import { toast } from '@/components/learn/ui/use-toast';
import { UserBasev1_0_0 } from '@/lib/interface-base/user.base.v1.0.0';
import Link from 'next/link';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
  DialogDescription,
} from '@/components/learn/ui/dialog';
import Image from 'next/image';
import ReactMarkdown from 'react-markdown';
import { Button as UIButton } from '@/components/learn/ui/button';
import { Input } from '@/components/learn/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/learn/ui/select';
import { Label } from '@/components/learn/ui/label';
import { useState as useState2 } from 'react';
import {
  Dialog as Dialog2,
  DialogContent as DialogContent2,
  DialogHeader as DialogHeader2,
  DialogTitle as DialogTitle2,
  DialogFooter as DialogFooter2,
} from '@/components/learn/ui/dialog';
import { Button } from '@/components/learn/ui/button';
//import SubmissionDetails from './SubmissionDetails';
/*
enum QuestionStatus {
  Corrected = 'corrected'
}*/

interface TeacherQuestionDetailProps {
  question: QuestionTypev1_0_0;
  mode: 'light' | 'dark' | 'high-contrast';
  courseId: string;
}

interface StudentDetails {
  user: UserBasev1_0_0;
  email: string;
  profilePicture?: string;
}

interface SubmissionDetailsProps {
  submission:
    | CodeQuestionv1_0_0
    | AnswerQuestionv1_0_0
    | MultipleChoiceQuestionv1_0_0
    | EssayQuestionv1_0_0;
  mode: 'light' | 'dark' | 'high-contrast';
  onClose: () => void;
  courseId: string;
  assessmentId: number;
  userId: string | null;
  role: string | null;
  moduleId: string | null;
  initialScore: number;
  maxScore: number;
  onScoreChange: (newScore: number) => void;
  question: QuestionTypev1_0_0;
}

const SubmissionDetails: React.FC<SubmissionDetailsProps> = ({
  submission,
  mode,
  onClose,
  courseId,
  assessmentId,
  userId,
  role,
  moduleId,
  initialScore,
  maxScore,
  onScoreChange,
  question,
}) => {
  const [score, setScore] = useState<number>(() => {
    const initialScoreNumber = Number(initialScore);
    return isNaN(initialScoreNumber) ? 0 : initialScoreNumber;
  });
  const actualOutput =
    submission.type === 'code'
      ? (submission as CodeQuestionv1_0_0)?.testResults
          ?.map((result) => result.actualOutput)
          .join('\n')
      : '';
  const expectedOutput =
    submission.type === 'code'
      ? (submission as CodeQuestionv1_0_0)?.testResults
          ?.map((result) => result.expectedOutput)
          .join('\n')
      : '';
  const testResults =
    submission.type === 'code'
      ? (submission as CodeQuestionv1_0_0)?.testResults || []
      : [];
  const submissionId = `${submission.id}user${userId}`;

  const handleViewCode = () => {
    setShowStudentCode(true);
  };

  const handleCloseCode = () => {
    setShowStudentCode(false);
  };

  const [calculatedScore, setCalculatedScore] = useState(0);
  const [additionalScore, setAdditionalScore] = useState(0);

  useEffect(() => {
    const totalScore =
      submission.type === 'code'
        ? submission.testResults.reduce((acc, result, index) => {
            if (result.actualOutput === result.expectedOutput) {
              const score =
                (question as CodeQuestionv1_0_0).outputsScore?.[index] || 0;
              return acc + score;
            }
            return acc;
          }, 0)
        : 0;

    setCalculatedScore(Math.min(totalScore, maxScore));
    setAdditionalScore(Number(initialScore) - totalScore);
  }, [submission, question, initialScore, maxScore]);

  const handleScoreChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newAdditionalScore = Math.max(
      0,
      Math.min(parseInt(e.target.value) || 0, maxScore - calculatedScore),
    );
    setAdditionalScore(newAdditionalScore);
  };

  const handleSaveScore = () => {
    const finalScore = Math.min(calculatedScore + additionalScore, maxScore);
    onScoreChange(finalScore);
    onClose();
  };

  return (
    <Dialog open={true} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[625px]">
        <DialogHeader>
          <DialogTitle>Submission Details</DialogTitle>
        </DialogHeader>
        <div className="mt-4">
          <div className="mb-4 flex items-center space-x-4">
            <div>
              <label
                htmlFor="calculatedScore"
                className="block text-sm font-medium text-gray-700"
              >
                Score:
              </label>
              <span className="block text-sm">
                {calculatedScore} / {maxScore}
              </span>
            </div>
            <div>
              <label
                htmlFor="additionalScore"
                className="block text-sm font-medium text-gray-700"
              >
                Add:
              </label>
              <Input
                type="number"
                id="additionalScore"
                name="additionalScore"
                value={additionalScore}
                onChange={handleScoreChange}
                min={0}
                max={maxScore - calculatedScore}
                className="block w-16 rounded-md border border-gray-300 shadow-sm px-2 py-1 text-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
              />
            </div>
            <div>
              <label
                htmlFor="finalScore"
                className="block text-sm font-medium text-gray-700"
              >
                Final Score:
              </label>
              <span className="block text-sm">
                {Math.min(calculatedScore + additionalScore, maxScore)} /{' '}
                {maxScore}
              </span>
            </div>
          </div>

          {submission.type === 'code' && (
            <>
              <table className="min-w-full divide-y divide-gray-200">
                <thead className="bg-gray-50">
                  <tr>
                    <th
                      scope="col"
                      className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider"
                    >
                      Status
                    </th>
                    <th
                      scope="col"
                      className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider"
                    >
                      Actual Output
                    </th>
                    <th
                      scope="col"
                      className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider"
                    >
                      Expected Output
                    </th>
                    <th
                      scope="col"
                      className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider"
                    >
                      Score
                    </th>
                  </tr>
                </thead>
                <tbody className="bg-white divide-y divide-gray-200">
                  {testResults.map((result, index) => (
                    <tr key={index}>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                        {result.actualOutput === result.expectedOutput ? (
                          <span className="px-2 inline-flex text-xs leading-5 font-semibold rounded-full bg-green-100 text-green-800">
                            Pass
                          </span>
                        ) : (
                          <span className="px-2 inline-flex text-xs leading-5 font-semibold rounded-full bg-red-100 text-red-800">
                            Fail
                          </span>
                        )}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                        <pre className="whitespace-pre-wrap">
                          {result.actualOutput}
                        </pre>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                        <pre className="whitespace-pre-wrap">
                          {result.expectedOutput}
                        </pre>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                        {(question as CodeQuestionv1_0_0)?.outputsScore ? (
                          (question as CodeQuestionv1_0_0).outputsScore
                            .length === 1 ? (
                            (question as CodeQuestionv1_0_0).outputsScore[0]
                          ) : (
                            (question as CodeQuestionv1_0_0).outputsScore[index]
                          )
                        ) : (
                          <span className="text-red-500">Empty</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>

              <div className="mt-4">
                <Link
                  href={`learn/coding-environment?id=${submissionId}&type=submission&userId=${userId}&role=${role}&courseId=${courseId}&moduleId=${moduleId}&assessmentId=${assessmentId}&submissionId=${submission.id}`}
                >
                  <UIButton>View Student Code</UIButton>
                </Link>
              </div>
            </>
          )}

          {submission.type === 'answer' && (
            <>
              <div>
                <h3 className="text-lg font-semibold mb-2">
                  Submitted Answer:
                </h3>
                <p>{(submission as AnswerQuestionv1_0_0).submittedAnswer}</p>
              </div>
              <div className="mt-4">
                <h3 className="text-lg font-semibold mb-2">Expected Answer:</h3>
                <p>{(submission as AnswerQuestionv1_0_0).answer}</p>
              </div>
            </>
          )}
          {submission.type === 'multiple-choice' && (
            <>
              <div>
                <h3 className="text-lg font-semibold mb-2">
                  Submitted Answers:
                </h3>
                <ul>
                  {(
                    submission as MultipleChoiceQuestionv1_0_0
                  ).submittedAnswers.map((answer, index) => (
                    <li key={index}>{answer}</li>
                  ))}
                </ul>
              </div>
              <div className="mt-4">
                <h3 className="text-lg font-semibold mb-2">Correct Answers:</h3>
                <ul>
                  {(
                    submission as MultipleChoiceQuestionv1_0_0
                  ).correctAnswers.map((answer, index) => (
                    <li key={index}>{answer}</li>
                  ))}
                </ul>
              </div>
            </>
          )}
          {submission.type === 'essay' && (
            <>
              <div>
                <h3 className="text-lg font-semibold mb-2">Submitted Essay:</h3>
                <ReactMarkdown>
                  {(submission as any).submittedEssay}
                </ReactMarkdown>
              </div>
              <div className="mt-4">
                <h3 className="text-lg font-semibold mb-2">Expected Answer:</h3>
                <p>{(submission as any).answer}</p>
              </div>
            </>
          )}
        </div>
        <DialogFooter>
          <UIButton onClick={onClose}>Close</UIButton>
          <UIButton onClick={handleSaveScore}>Save Score</UIButton>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

const TeacherQuestionDetail = ({
  question,
  mode,
  courseId,
}: TeacherQuestionDetailProps) => {
  const [submissions, setSubmissions] = useState<{ [key: string]: any }>({});
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [viewingSubmission, setViewingSubmission] = useState<string | null>(
    null,
  );
  const [showStudentDetails, setShowStudentDetails] = useState<string | null>(
    null,
  );
  const [statusFilter, setStatusFilter] = useState<string>('all');

  const filteredSubmissions = useMemo(() => {
    if (statusFilter === 'all') {
      return submissions;
    }
    return Object.fromEntries(
      Object.entries(submissions).filter(
        ([_, submissionData]) =>
          submissionData.submission &&
          submissionData.submission.status.toString() === statusFilter,
      ),
    );
  }, [submissions, statusFilter]);

  useEffect(() => {
    const fetchSubmissions = async () => {
      try {
        setIsLoading(true);
        const response = await fetch(
          `../../../api/learn/teach/question/${question.id}?courseId=${courseId}`,
        );
        if (!response.ok) {
          throw new Error('Failed to fetch student submissions');
        }
        const data = await response.json();
        setSubmissions(data.submissions || {});
      } catch (error) {
        console.error('Error fetching student submissions:', error);
        setError('Failed to load student submissions');
      } finally {
        setIsLoading(false);
      }
    };

    fetchSubmissions();
  }, [question.id, courseId]);

  if (isLoading) {
    return <div>Loading submissions...</div>;
  }

  if (error) {
    return <div>Error: {error}</div>;
  }

  return (
    <div>
      <h2 className="text-xl font-semibold mb-4">Student Submissions</h2>
      <div className="mb-4 flex items-center">
        <Label htmlFor="status-filter" className="mr-2">
          Filter by status:
        </Label>
        <Select value={statusFilter} onValueChange={setStatusFilter}>
          <SelectTrigger id="status-filter" className="w-[180px]">
            <SelectValue placeholder="Select status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All</SelectItem>
            <SelectItem value="0">Unavailable</SelectItem>
            <SelectItem value="1">Available</SelectItem>
            <SelectItem value="2">Submitted</SelectItem>
            <SelectItem value="3">Corrected</SelectItem>
          </SelectContent>
        </Select>
      </div>
      {Object.keys(filteredSubmissions).length > 0 ? (
        <table className="min-w-full divide-y divide-gray-200">
          <thead className="bg-gray-50">
            <tr>
              <th
                scope="col"
                className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider"
              >
                Student
              </th>
              <th
                scope="col"
                className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider"
              >
                Status
              </th>
              <th
                scope="col"
                className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider"
              >
                Score
              </th>
              <th
                scope="col"
                className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider"
              >
                Actions
              </th>
            </tr>
          </thead>
          <tbody className="bg-white divide-y divide-gray-200">
            {Object.entries(filteredSubmissions).map(
              ([userId, submissionData]) => {
                const { user, submission, maxScore, score } = submissionData;
                return (
                  <tr key={user.idUser}>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <div className="flex items-center">
                        <div
                          className="flex-shrink-0 h-10 w-10 cursor-pointer"
                          onClick={() => setShowStudentDetails(userId)}
                        >
                          <img
                            className="h-10 w-10 rounded-full"
                            src={user.profilePicture || '/placeholder.svg'}
                            alt=""
                          />
                        </div>
                        <div
                          className="ml-4 cursor-pointer"
                          onClick={() => setShowStudentDetails(userId)}
                        >
                          <div className="text-sm font-medium text-gray-900">{`${user.firstName} ${user.lastName}`}</div>
                        </div>
                      </div>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <span className="px-2 inline-flex text-xs leading-5 font-semibold rounded-full bg-green-100 text-green-800">
                        {submission
                          ? QuestionStatus[submission.status]
                          : 'Not Submitted'}
                      </span>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                      {score} / {maxScore}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-right text-sm font-medium">
                      {submission ? (
                        <Button
                          variant="link"
                          onClick={() => setViewingSubmission(userId)}
                          className="text-indigo-600 hover:text-indigo-900"
                        >
                          View
                        </Button>
                      ) : (
                        <></>
                      )}
                    </td>
                  </tr>
                );
              },
            )}
          </tbody>
        </table>
      ) : (
        <p>No submissions matching the selected filter.</p>
      )}

      {viewingSubmission && submissions[viewingSubmission]?.submission && (
        <SubmissionDetails
          submission={submissions[viewingSubmission].submission}
          mode={mode}
          onClose={() => setViewingSubmission(null)}
          courseId={courseId}
          assessmentId={question.id}
          userId={viewingSubmission}
          role="teacher"
          moduleId={null}
          initialScore={submissions[viewingSubmission].score}
          maxScore={submissions[viewingSubmission].maxScore}
          onScoreChange={async (newScore) => {
            try {
              const response = await fetch(`../../../api/learn/update-score`, {
                method: 'POST',
                headers: {
                  'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                  courseId,
                  questionId: question.id,
                  userId: viewingSubmission,
                  newScore,
                }),
              });

              if (!response.ok) {
                throw new Error('Failed to update score');
              }

              setSubmissions((prev) => ({
                ...prev,
                [viewingSubmission]: {
                  ...prev[viewingSubmission],
                  score: newScore,
                },
              }));

              toast({
                title: 'Score updated',
                description:
                  "The student's score has been successfully updated.",
              });
            } catch (error) {
              console.error('Error updating score:', error);
              toast({
                title: 'Error',
                description: 'Failed to update the score. Please try again.',
                variant: 'destructive',
              });
            }
          }}
          question={question}
        />
      )}

      {showStudentDetails && (
        <Dialog open={true} onOpenChange={() => setShowStudentDetails(null)}>
          <DialogContent className="sm:max-w-[425px]">
            <DialogHeader>
              <DialogTitle>Student Details</DialogTitle>
            </DialogHeader>
            <div className="mt-4">
              <div className="flex items-center">
                <div className="flex-shrink-0 h-10 w-10">
                  <img
                    className="h-10 w-10 rounded-full"
                    src={
                      submissions[showStudentDetails].user.profilePicture ||
                      '/placeholder.svg'
                    }
                    alt=""
                  />
                </div>
                <div className="ml-4">
                  <div className="text-sm font-medium text-gray-900">{`${submissions[showStudentDetails].user.firstName} ${submissions[showStudentDetails].user.lastName}`}</div>
                  <div className="text-sm text-gray-500">
                    {submissions[showStudentDetails].user.email}
                  </div>
                </div>
              </div>
            </div>
            <DialogFooter>
              <UIButton onClick={() => setShowStudentDetails(null)}>
                Close
              </UIButton>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      )}
    </div>
  );
};

export default TeacherQuestionDetail;

function setShowStudentCode(arg0: boolean) {
  throw new Error('Function not implemented.');
}
