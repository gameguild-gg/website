import React, { useState } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/learn/ui/dialog";
import { Button } from "@/components/learn/ui/button";
import { Input } from "@/components/learn/ui/input";
import { QuestionBasev1_0_0, CodeQuestionv1_0_0, AnswerQuestionv1_0_0, MultipleChoiceQuestionv1_0_0, EssayQuestionv1_0_0 } from '@/lib/interface-base/question.base.v1.0.0';
import ReactMarkdown from 'react-markdown';
import RichTextEditor from '@/components/learn/RichTextEditor';
import Link from 'next/link';

interface SubmissionDetailsProps {
  submission: CodeQuestionv1_0_0 | AnswerQuestionv1_0_0 | MultipleChoiceQuestionv1_0_0 | EssayQuestionv1_0_0;
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
}) => {
  const [score, setScore] = useState(initialScore);

  const handleScoreChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newScore = Math.min(Math.max(0, parseInt(e.target.value) || 0), maxScore);
    setScore(newScore);
  };

  const handleSaveScore = () => {
    onScoreChange(score);
    onClose();
  };

  const submissionId = userId ? `${submission.id}user${userId}` : undefined;

  const renderSubmissionContent = () => {
    switch (submission.type) {
      case 'code':
        return (
          <div>
            <h3 className="text-lg font-semibold mb-2">Submitted Code:</h3>
            <pre className="bg-gray-100 p-4 rounded-md overflow-x-auto">
              <code>{(submission as CodeQuestionv1_0_0).submittedCode.join('\n')}</code>
            </pre>
            {(submission as CodeQuestionv1_0_0).output && (
              <div className="mt-4">
                <h3 className="text-lg font-semibold mb-2">Output:</h3>
                <pre className="bg-gray-100 p-4 rounded-md overflow-x-auto">
                  <code>{(submission as CodeQuestionv1_0_0).output}</code>
                </pre>
              </div>
            )}
            <Link href={`/learn/coding-environment?id=${submission.id}&type=submission&userId=${userId}&role=${role}&courseId=${courseId}&moduleId=${moduleId}&assessmentId=${assessmentId}&submissionId=${submissionId}`}>
              <Button>View Student Code</Button>
            </Link>
          </div>
        );
      case 'answer':
        return (
          <div>
            <h3 className="text-lg font-semibold mb-2">Submitted Answer:</h3>
            <ReactMarkdown>{(submission as AnswerQuestionv1_0_0).submittedAnswer}</ReactMarkdown>
          </div>
        );
      case 'multiple-choice':
        return (
          <div>
            <h3 className="text-lg font-semibold mb-2">Selected Answers:</h3>
            <ul className="list-disc list-inside">
              {(submission as MultipleChoiceQuestionv1_0_0).submittedAnswers.map((answerId, index) => (
                <li key={index}>{answerId}</li>
              ))}
            </ul>
          </div>
        );
      case 'essay':
        return (
          <div>
            <h3 className="text-lg font-semibold mb-2">Submitted Essay:</h3>
            <ReactMarkdown>{(submission as EssayQuestionv1_0_0).submittedEssay}</ReactMarkdown>
          </div>
        );
      default:
        return <p>Unsupported submission type</p>;
    }
  };

  return (
    <Dialog open={true} onOpenChange={onClose}>
      <DialogContent className={`${
        mode === 'light' ? 'bg-white text-gray-900' :
        mode === 'dark' ? 'bg-gray-800 text-gray-100' :
        'bg-black text-yellow-300'
      } max-w-3xl`}>
        <DialogHeader>
          <DialogTitle>Submission Details</DialogTitle>
        </DialogHeader>
        <div className="mt-4">
          <h2 className="text-xl font-semibold mb-4">{submission.title}</h2>
          <div className="prose max-w-none mb-6">
            <ReactMarkdown>{submission.description}</ReactMarkdown>
          </div>
          {renderSubmissionContent()}
          <div className="mt-6">
            <label htmlFor="score" className="block text-sm font-medium mb-2">
              Score (out of {maxScore}):
            </label>
            <Input
              type="number"
              id="score"
              value={score}
              onChange={handleScoreChange}
              min={0}
              max={maxScore}
              className="w-24"
            />
          </div>
        </div>
        <DialogFooter>
          <Button onClick={onClose} variant="outline">Cancel</Button>
          <Button onClick={handleSaveScore}>Save Score</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

export default SubmissionDetails;

