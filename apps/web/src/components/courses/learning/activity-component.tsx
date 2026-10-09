'use client';

import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@game-guild/ui/components/input';
import { Label } from '@game-guild/ui/components/label';
import { Textarea } from '@game-guild/ui/components/textarea';
import {
  createEmptyQuizAnswer,
  evaluateQuizAnswer,
  type QuizAnswer,
  type QuizLearnerEntry,
  type QuizPracticeEntry,
} from '@game-guild/quiz';
import type { AssessmentSubmissionRuntimeViewV1 } from '@game-guild/grading';
import {
  createQuizAnswerEnvelope,
  parseQuizAnswerEnvelope,
  QUIZ_ASSESSMENT_TYPE_ADAPTER,
  type QuizLearnerDeliveryItemV1,
} from '@game-guild/grading-adapter-quiz';
import { isQuizRuntimeContentDocument } from '@game-guild/quiz-content';
import {
  QuizPlayer,
  QuizPracticePlayer,
  type QuizSubmissionResult,
} from '@game-guild/quiz-surface/player';
import { submitActivity } from '@/lib/courses/server-actions';
import {
  getRuntimeSubmission,
  startContentRuntimeSubmission,
  submitRuntimeSubmission,
} from '@/lib/learning/grading-runtime-actions';
import { Clock, Code, FileText, MessageSquare, Play, Save, Send, Upload } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';

interface ContentItem {
  id: string;
  title: string;
  type: 'lesson' | 'activity' | 'quiz' | 'assignment' | 'peer-review';
  status: 'locked' | 'available' | 'in-progress' | 'completed';
  duration?: number;
  description?: string;
  order: number;
  isRequired: boolean;
  activityType?: 'text' | 'code' | 'file' | 'quiz' | 'discussion';
  content?: unknown;
  progress?: number;
}

interface ActivityComponentProps {
  item: ContentItem;
  courseId?: string;
  existingSubmissionId?: string | null;
  onComplete: (score?: number) => void;
}

interface QuizActivityQuestion<Entry> {
  id: string;
  data: Entry;
}

type QuizActivityContent =
  | {
      questions: Array<QuizActivityQuestion<QuizLearnerEntry>>;
      serverGraded: true;
    }
  | {
      questions: Array<QuizActivityQuestion<QuizPracticeEntry>>;
      serverGraded: false;
    };

export function ActivityComponent({
  item,
  courseId,
  existingSubmissionId,
  onComplete,
}: ActivityComponentProps) {
  const [hasStarted, setHasStarted] = useState(false);
  const [isStarting, setIsStarting] = useState(Boolean(existingSubmissionId));
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [runtimeError, setRuntimeError] = useState<string | null>(null);
  const [runtimeSubmission, setRuntimeSubmission] = useState<
    AssessmentSubmissionRuntimeViewV1<QuizLearnerDeliveryItemV1> | null
  >(null);
  const [submission, setSubmission] = useState<Record<string, unknown>>({});
  const [currentStep, setCurrentStep] = useState(0);
  const [quizAnswers, setQuizAnswers] = useState<Record<string, QuizAnswer>>({});
  const [quizSubmissionResults, setQuizSubmissionResults] = useState<
    Record<string, QuizSubmissionResult>
  >({});
  const startIdempotencyKey = useRef(createIdempotencyKey());
  const submitCommand = useRef<{ payload: string; key: string } | null>(null);

  useEffect(() => {
    if (!existingSubmissionId || item.type !== 'quiz') return;

    let cancelled = false;

    void getRuntimeSubmission(existingSubmissionId).then((result) => {
      if (cancelled) return;
      setIsStarting(false);
      if (!result.success) {
        setRuntimeError(result.error);
        return;
      }

      const nextSubmission =
        result.data as AssessmentSubmissionRuntimeViewV1<QuizLearnerDeliveryItemV1>;
      const quiz = readQuizActivityContent(item.content, nextSubmission);
      setRuntimeSubmission(nextSubmission);
      setQuizAnswers(readSubmittedQuizAnswers(nextSubmission));
      setQuizSubmissionResults(
        quiz.serverGraded
          ? buildQuizSubmissionResults(nextSubmission, quiz)
          : {},
      );
      setHasStarted(true);
    });

    return () => {
      cancelled = true;
    };
  }, [existingSubmissionId, item.content, item.type]);

  const handleStart = async () => {
    const authoredQuiz =
      item.type === 'quiz'
        ? readQuizActivityContent(item.content, null)
        : null;
    if (!authoredQuiz?.serverGraded) {
      setHasStarted(true);
      return;
    }

    setIsStarting(true);
    setRuntimeError(null);
    const result = await startContentRuntimeSubmission(
      item.id,
      startIdempotencyKey.current,
    );
    setIsStarting(false);
    if (!result.success) {
      setRuntimeError(result.error);
      return;
    }

    setRuntimeSubmission(
      result.data as AssessmentSubmissionRuntimeViewV1<QuizLearnerDeliveryItemV1>,
    );
    if (result.data.contentCompleted) onComplete();
    setHasStarted(true);
  };

  const handleSubmit = async () => {
    setIsSubmitting(true);

    try {
      const quiz =
        item.type === 'quiz'
          ? readQuizActivityContent(item.content, runtimeSubmission)
          : null;
      const quizResponse = quiz
        ? createQuizAnswerEnvelope(Object.fromEntries(
            quiz.questions.map((question) => {
              const answer =
                quizAnswers[question.id] ??
                createEmptyQuizAnswer(question.data.type);
              return [question.id, answer];
            }),
          ))
        : null;

      if (quiz?.serverGraded) {
        if (!runtimeSubmission || !quizResponse) {
          setRuntimeError('The official quiz attempt is not available.');
          return;
        }
        if (runtimeSubmission.status !== 'inProgress') {
          setRuntimeError('This attempt has already been submitted.');
          return;
        }

        const canonicalResponse = JSON.stringify(quizResponse);
        if (submitCommand.current?.payload !== canonicalResponse) {
          submitCommand.current = {
            payload: canonicalResponse,
            key: createIdempotencyKey(),
          };
        }
        const result = await submitRuntimeSubmission(
          runtimeSubmission.submissionId,
          quizResponse,
          submitCommand.current.key,
        );
        if (!result.success) {
          setRuntimeError(result.error);
          return;
        }

        const nextSubmission =
          result.data as AssessmentSubmissionRuntimeViewV1<QuizLearnerDeliveryItemV1>;
        setRuntimeSubmission(nextSubmission);
        setRuntimeError(null);
        setQuizSubmissionResults(buildQuizSubmissionResults(nextSubmission, quiz));
        if (nextSubmission.contentCompleted) {
          const visible = nextSubmission.execution.learnerVisibleResult;
          const score =
            visible?.score != null && visible.maxScore > 0
              ? Math.round((visible.score / visible.maxScore) * 100)
              : undefined;
          onComplete(score);
        }
        submitCommand.current = null;
        return;
      }

      const submissionData = {
        activityId: item.id,
        courseId,
        activityType: item.activityType || 'text',
        content: quizResponse ?? submission,
        isGraded:
          item.type === 'quiz'
            ? quiz?.serverGraded ?? false
            : item.type === 'assignment',
        attempt: 1,
      };

      const result = await submitActivity(submissionData);

      if (result.success) {
        const localPracticeScore =
          quiz && !quiz.serverGraded ? calculateQuizScore(quiz) : undefined;
        onComplete(result.score ?? localPracticeScore);
      } else {
        console.error('Submission failed:', result.message);
      }
    } catch (error) {
      console.error('Error submitting activity:', error);
    } finally {
      setIsSubmitting(false);
    }
  };

  const calculateQuizScore = (
    quiz: Extract<QuizActivityContent, { serverGraded: false }>,
  ): number => {
    const totals = quiz.questions.reduce(
      (current, question) => {
        const points = Number(question.data.points ?? "00000001.0000");
        const answer =
          quizAnswers[question.id] ??
          createEmptyQuizAnswer(question.data.type);
        const result = evaluateQuizAnswer(question.data, answer);

        return {
          possible: current.possible + points,
          earned: current.earned + (result.status === 'correct' ? points : 0),
        };
      },
      { possible: 0, earned: 0 },
    );

    return totals.possible > 0
      ? Math.round((totals.earned / totals.possible) * 100)
      : 0;
  };

  const getQuizContent = (): QuizActivityContent => {
    return readQuizActivityContent(item.content, runtimeSubmission);
  };

  const getActivityIcon = () => {
    switch (item.activityType || item.type) {
      case 'text':
        return <FileText className="h-16 w-16 text-blue-400" />;
      case 'code':
        return <Code className="h-16 w-16 text-purple-400" />;
      case 'file':
        return <Upload className="h-16 w-16 text-green-400" />;
      case 'quiz':
        return <MessageSquare className="h-16 w-16 text-yellow-400" />;
      default:
        return <Play className="h-16 w-16 text-slate-400" />;
    }
  };

  const renderQuizActivity = () => {
    const quiz = getQuizContent();
    const currentQuestion = quiz.questions[currentStep];
    if (!currentQuestion) {
      return (
        <div className="rounded-md border p-8 text-center text-sm text-muted-foreground">
          This quiz has no published questions.
        </div>
      );
    }
    const isLastQuestion = currentStep === quiz.questions.length - 1;
    const answer =
      quizAnswers[currentQuestion.id] ??
      createEmptyQuizAnswer(currentQuestion.data.type);
    const learnerEntry = quiz.serverGraded
      ? quiz.questions[currentStep]?.data
      : null;
    const practiceEntry = !quiz.serverGraded
      ? quiz.questions[currentStep]?.data
      : null;
    const setAnswer = (nextAnswer: QuizAnswer) => {
      setQuizAnswers((current) => ({
        ...current,
        [currentQuestion.id]: nextAnswer,
      }));
    };

    return (
      <div className="space-y-6">
        {runtimeError && (
          <div role="alert" className="rounded-md border border-destructive bg-destructive/10 p-3 text-sm text-destructive">
            {runtimeError}
          </div>
        )}
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-semibold">
            Question {currentStep + 1} of {quiz.questions.length}
          </h3>
          <Badge variant="outline">
            {Number(currentQuestion.data.points ?? "00000001.0000")} points
          </Badge>
        </div>

        <Card>
          <CardContent className="p-6">
            {quiz.serverGraded ? (
              <QuizPlayer
                entry={learnerEntry!}
                answer={answer}
                disabled={runtimeSubmission?.status !== 'inProgress'}
                onAnswerChange={setAnswer}
                onSubmit={(nextAnswer) => {
                  setAnswer(nextAnswer);
                  setQuizSubmissionResults((current) => ({
                    ...current,
                    [currentQuestion.id]: {
                      status: 'pending',
                      feedback: 'Answer recorded. Submit the quiz to grade it.',
                    },
                  }));
                }}
                submissionResult={
                  quizSubmissionResults[currentQuestion.id] ?? { status: 'idle' }
                }
              />
            ) : (
              <QuizPracticePlayer
                entry={practiceEntry!}
                answer={answer}
                onAnswerChange={setAnswer}
              />
            )}
          </CardContent>
        </Card>

        <div className="flex justify-between">
          <Button variant="outline" onClick={() => setCurrentStep(Math.max(0, currentStep - 1))} disabled={currentStep === 0} className="border-gray-600">
            Previous
          </Button>

          {isLastQuestion ? (
            <Button
              onClick={handleSubmit}
              disabled={isSubmitting || (quiz.serverGraded && runtimeSubmission?.status !== 'inProgress')}
              className="bg-green-600 hover:bg-green-700"
            >
              {isSubmitting ? 'Submitting...' : 'Submit Quiz'}
            </Button>
          ) : (
            <Button onClick={() => setCurrentStep(Math.min(quiz.questions.length - 1, currentStep + 1))} className="bg-blue-600 hover:bg-blue-700">
              Next
            </Button>
          )}
        </div>
      </div>
    );
  };

  const renderTextActivity = () => (
    <div className="space-y-6">
      <Card className="bg-gradient-to-br from-slate-900/50 to-slate-800/50 backdrop-blur-sm border-slate-700/50 shadow-lg">
        <CardHeader>
          <CardTitle className="text-white">Activity Instructions</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-slate-400 mb-4">{item.description || 'Complete this text-based activity by providing your response below.'}</p>

          {item.id === 'activity-1' && (
            <div className="bg-gradient-to-br from-slate-800/50 to-slate-700/50 p-4 rounded border border-slate-600 backdrop-blur-sm">
              <h4 className="font-semibold mb-2 text-white">Setup Instructions:</h4>
              <ol className="list-decimal list-inside space-y-2 text-slate-400">
                <li>Download and install your preferred game engine (Unity, Unreal Engine, or Godot)</li>
                <li>Set up a code editor (Visual Studio Code, Visual Studio, or similar)</li>
                <li>Create a new project in your chosen engine</li>
                <li>Familiarize yourself with the interface and basic navigation</li>
                <li>Document any challenges you encountered during setup</li>
              </ol>
            </div>
          )}
        </CardContent>
      </Card>

      <Card className="bg-gradient-to-br from-slate-900/50 to-slate-800/50 backdrop-blur-sm border-slate-700/50 shadow-lg">
        <CardHeader>
          <CardTitle className="text-white">Your Response</CardTitle>
        </CardHeader>
        <CardContent>
          <Textarea
            placeholder="Describe your setup process, any challenges encountered, and what you learned..."
            value={(submission.response as string) || ''}
            onChange={(e) => setSubmission((prev) => ({ ...prev, response: e.target.value }))}
            className="bg-slate-800/50 border-slate-600 text-white min-h-[200px] backdrop-blur-sm"
          />
          <p className="text-sm text-slate-400 mt-2">Minimum 100 words required for completion.</p>
        </CardContent>
      </Card>

      <div className="flex justify-end gap-2">
        <Button variant="outline" onClick={() => setSubmission((prev) => ({ ...prev, saved: true }))} className="bg-slate-800/50 border-slate-600 text-slate-200 hover:bg-slate-700/50 hover:border-slate-500">
          <Save className="h-4 w-4 mr-2" />
          Save Draft
        </Button>
        <Button
          onClick={handleSubmit}
          disabled={isSubmitting || ((submission.response as string) || '').length < 100}
          className="bg-gradient-to-r from-green-600 to-teal-600 hover:from-green-700 hover:to-teal-700 border-0 shadow-lg hover:shadow-xl hover:shadow-green-500/25 transition-all"
        >
          <Send className="h-4 w-4 mr-2" />
          {isSubmitting ? 'Submitting...' : 'Submit Activity'}
        </Button>
      </div>
    </div>
  );

  const renderCodeActivity = () => (
    <div className="space-y-6">
      <Card className="bg-gradient-to-br from-slate-900/50 to-slate-800/50 backdrop-blur-sm border-slate-700/50 shadow-lg">
        <CardHeader>
          <CardTitle className="text-white">Coding Challenge</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-slate-400 mb-4">Create a simple player movement script based on the lesson content.</p>

          <div className="bg-gradient-to-br from-slate-800/50 to-slate-700/50 p-4 rounded border border-slate-600 mb-4 backdrop-blur-sm">
            <h4 className="font-semibold mb-2 text-white">Requirements:</h4>
            <ul className="list-disc list-inside space-y-1 text-slate-400">
              <li>Create a script that handles player input (WASD or arrow keys)</li>
              <li>Implement basic movement in 2D or 3D space</li>
              <li>Include comments explaining your code</li>
              <li>Test your script and document any issues</li>
            </ul>
          </div>
        </CardContent>
      </Card>

      <Card className="bg-gradient-to-br from-slate-900/50 to-slate-800/50 backdrop-blur-sm border-slate-700/50 shadow-lg">
        <CardHeader>
          <CardTitle className="text-white">Code Submission</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            <div>
              <Label htmlFor="language">Programming Language</Label>
              <Input
                id="language"
                placeholder="e.g., C#, JavaScript, GDScript"
                value={(submission.language as string) || ''}
                onChange={(e) => setSubmission((prev) => ({ ...prev, language: e.target.value }))}
                className="bg-gray-800 border-gray-600 text-white"
              />
            </div>

            <div>
              <Label htmlFor="code">Your Code</Label>
              <Textarea
                id="code"
                placeholder="Paste your player movement script here..."
                value={(submission.code as string) || ''}
                onChange={(e) => setSubmission((prev) => ({ ...prev, code: e.target.value }))}
                className="bg-gray-800 border-gray-600 text-white font-mono min-h-[300px]"
              />
            </div>

            <div>
              <Label htmlFor="explanation">Code Explanation</Label>
              <Textarea
                id="explanation"
                placeholder="Explain how your code works and any challenges you faced..."
                value={(submission.explanation as string) || ''}
                onChange={(e) => setSubmission((prev) => ({ ...prev, explanation: e.target.value }))}
                className="bg-gray-800 border-gray-600 text-white min-h-[150px]"
              />
            </div>
          </div>
        </CardContent>
      </Card>

      <div className="flex justify-end gap-2">
        <Button variant="outline" onClick={() => setSubmission((prev) => ({ ...prev, saved: true }))} className="border-gray-600">
          <Save className="h-4 w-4 mr-2" />
          Save Draft
        </Button>
        <Button onClick={handleSubmit} disabled={isSubmitting || !((submission.code as string) || '').trim()} className="bg-green-600 hover:bg-green-700">
          <Send className="h-4 w-4 mr-2" />
          {isSubmitting ? 'Submitting...' : 'Submit Code'}
        </Button>
      </div>
    </div>
  );

  if (!hasStarted) {
    return (
      <div className="text-center py-12">
        <div className="mb-6">{getActivityIcon()}</div>
        <h3 className="text-xl font-semibold mb-2 mt-4 text-white">{item.title}</h3>
        <p className="text-slate-400 mb-6 max-w-md mx-auto">{item.description || 'Ready to start this activity?'}</p>
        <div className="flex items-center justify-center gap-4 mb-6">
          {item.duration && (
            <div className="flex items-center gap-1 text-sm text-slate-400">
              <Clock className="h-4 w-4" />
              {item.duration} minutes
            </div>
          )}
          {item.isRequired && (
            <Badge variant="secondary" className="bg-gradient-to-r from-purple-500/20 to-blue-500/20 border-purple-500/30 text-purple-300">
              Required
            </Badge>
          )}
        </div>
        {runtimeError && (
          <p role="alert" className="mb-4 text-sm text-destructive">
            {runtimeError}
          </p>
        )}
        <Button
          onClick={() => void handleStart()}
          disabled={isStarting}
          className="bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 border-0 shadow-lg hover:shadow-xl hover:shadow-blue-500/25 transition-all"
        >
          {isStarting ? 'Starting...' : 'Start Activity'}
        </Button>
      </div>
    );
  }

  return (
    <div className="max-w-4xl mx-auto">
      {item.type === 'quiz' && renderQuizActivity()}
      {item.activityType === 'text' && renderTextActivity()}
      {item.activityType === 'code' && renderCodeActivity()}
      {item.activityType === 'file' && (
        <div className="text-center py-12">
          <Upload className="h-16 w-16 text-gray-400 mx-auto mb-4" />
          <p className="text-gray-400">File upload activity - Implementation in progress</p>
        </div>
      )}
    </div>
  );
}

function readQuizActivityContent(
  content: unknown,
  runtimeSubmission: AssessmentSubmissionRuntimeViewV1<QuizLearnerDeliveryItemV1> | null,
): QuizActivityContent {
  if (runtimeSubmission) {
    const { delivery } = runtimeSubmission.execution;
    const questions = delivery.itemOrder.map((itemId) => {
      const itemDelivery = delivery.items[itemId];
      if (
        !itemDelivery ||
        itemDelivery.adapterKey !== QUIZ_ASSESSMENT_TYPE_ADAPTER.key ||
        itemDelivery.adapterVersion !== QUIZ_ASSESSMENT_TYPE_ADAPTER.version
      ) {
        throw new Error(`Unsupported quiz delivery for item ${itemId}.`);
      }
      const payload = itemDelivery.learnerPayload;
      if (payload.itemId !== itemId) {
        throw new Error(`Quiz delivery item ${itemId} has a mismatched payload.`);
      }
      return { id: itemId, data: payload.entry };
    });
    return { questions, serverGraded: true };
  }

  if (!isQuizRuntimeContentDocument(content)) {
    return { questions: [], serverGraded: false };
  }

  const rawQuestions = content.document.order.flatMap((entry) => {
    if (
      !Array.isArray(entry) ||
      typeof entry[0] !== 'string' ||
      entry[1] !== 'quiz'
    ) {
      return [];
    }

    const data = content.document.blocks[entry[0]];
    if (!data || typeof data !== 'object' || Array.isArray(data)) return [];
    return [{ id: entry[0], data }];
  });

  return content.mode === 'server-graded'
    ? {
        questions: rawQuestions as Array<
          QuizActivityQuestion<QuizLearnerEntry>
        >,
        serverGraded: true,
      }
    : {
        questions: rawQuestions as Array<
          QuizActivityQuestion<QuizPracticeEntry>
        >,
        serverGraded: false,
      };
}

function readSubmittedQuizAnswers(
  submission: AssessmentSubmissionRuntimeViewV1<QuizLearnerDeliveryItemV1>,
): Record<string, QuizAnswer> {
  if (!submission.execution.submittedResponse) return {};

  try {
    return {
      ...parseQuizAnswerEnvelope(submission.execution.submittedResponse).payload
        .answers,
    };
  } catch {
    return {};
  }
}

function createIdempotencyKey(): string {
  return globalThis.crypto?.randomUUID?.() ?? `grading-${Date.now()}-${Math.random()}`;
}

function buildQuizSubmissionResults(
  submission: AssessmentSubmissionRuntimeViewV1<QuizLearnerDeliveryItemV1>,
  quiz: Extract<QuizActivityContent, { serverGraded: true }>,
): Record<string, QuizSubmissionResult> {
  const visible = submission.execution.learnerVisibleResult;
  if (!visible) {
    return Object.fromEntries(
      quiz.questions.map((question) => [
        question.id,
        {
          status: 'pending' as const,
          feedback: submission.execution.requiresInstructorReview
            ? 'Submitted for instructor review.'
            : 'Submitted. The result has not been released yet.',
        },
      ]),
    );
  }

  const byId = new Map(visible.items.map((item) => [item.itemId, item]));
  return Object.fromEntries(
    quiz.questions.map((question) => {
      const item = byId.get(question.id);
      if (!item || item.score == null) {
        return [question.id, { status: 'pending' as const, feedback: item?.feedback }];
      }
      return [
        question.id,
        {
          status: item.score === item.maxScore ? ('correct' as const) : ('incorrect' as const),
          feedback: item.feedback,
        },
      ];
    }),
  );
}
