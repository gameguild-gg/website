'use client';

import { useMemo, useRef, useState } from 'react';
import type {
  AssessmentExecutionRuntimeViewV1,
  AssessmentTestRunRuntimeViewV1,
  InstructorReviewResolutionV1,
  PeerReviewClaimRuntimeViewV1,
  ReviewMethods,
  ScoreValue,
  SelfReviewEvidenceV1,
} from '@game-guild/grading';
import { REVIEW_METHOD_FLAGS } from '@game-guild/grading';
import {
  createQuizAnswerEnvelope,
  parseQuizAnswerEnvelope,
  QUIZ_ASSESSMENT_TYPE_ADAPTER,
  type QuizLearnerDeliveryItemV1,
} from '@game-guild/grading-adapter-quiz';
import { createEmptyQuizAnswer, type QuizAnswer } from '@game-guild/quiz';
import { QuizPlayer, type QuizSubmissionResult } from '@game-guild/quiz-surface/player';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@game-guild/ui/components/dialog';
import { Input } from '@game-guild/ui/components/input';
import { Textarea } from '@game-guild/ui/components/textarea';
import {
  AlertTriangle,
  CheckCircle2,
  ClipboardCheck,
  Loader2,
  Play,
  RotateCcw,
} from 'lucide-react';
import {
  claimTestRunPeerReview,
  getTestRunPeerReview,
  restartAssessmentTestRun,
  resolveTestRunInstructorReview,
  startAssessmentTestRun,
  submitAssessmentTestRun,
  submitTestRunPeerReview,
  submitTestRunSelfReview,
} from '@/lib/learning/grading-runtime-actions';
import {
  pointsToScoreUnits,
  scoreUnitsToPoints,
} from '@/lib/learning/academic-values';
import { SelfReviewForm } from './self-review-form';

export interface RuntimeQuizTestRunProps {
  assessmentId: string;
  revisionId: string;
  reviewMethods: ReviewMethods;
  peerReviewsRequired?: number;
  disabled?: boolean;
}

interface ScoreDraft {
  points: string;
  feedback: string;
}

export function RuntimeQuizTestRun({
  assessmentId,
  revisionId,
  reviewMethods,
  peerReviewsRequired = 1,
  disabled = false,
}: RuntimeQuizTestRunProps): React.JSX.Element {
  const [open, setOpen] = useState(false);
  const [run, setRun] =
    useState<AssessmentTestRunRuntimeViewV1<QuizLearnerDeliveryItemV1> | null>(
      null,
    );
  const [answers, setAnswers] = useState<Record<string, QuizAnswer>>({});
  const [recorded, setRecorded] = useState<ReadonlySet<string>>(new Set());
  const [scores, setScores] = useState<Record<string, ScoreDraft>>({});
  const [overallFeedback, setOverallFeedback] = useState('');
  const [overrideReason, setOverrideReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedSubjectId, setSelectedSubjectId] = useState<string | null>(null);
  const [peerClaims, setPeerClaims] = useState<
    Record<string, PeerReviewClaimRuntimeViewV1>
  >({});
  const [completedPeerReviewSubjects, setCompletedPeerReviewSubjects] =
    useState<ReadonlySet<string>>(new Set());
  const [peerScore, setPeerScore] = useState('');
  const [peerFeedback, setPeerFeedback] = useState('');
  const answerDrafts = useRef<
    Record<string, { answers: Record<string, QuizAnswer>; recorded: ReadonlySet<string> }>
  >({});
  const startKey = useRef(createIdempotencyKey());
  const submitCommand = useRef<{ hash: string; key: string } | null>(null);
  const selfReviewCommand = useRef<{ hash: string; key: string } | null>(null);

  const subjects = run?.subjects?.length
    ? run.subjects
    : run
      ? [
          {
            subjectId: 'primary',
            personaKey: run.personaKey,
            personaDisplayName: run.personaDisplayName,
            execution: run.execution,
          },
        ]
      : [];
  const activeSubject =
    subjects.find((subject) => subject.subjectId === selectedSubjectId) ??
    subjects[0];
  const execution = activeSubject?.execution ?? null;
  const activeSubjectKey = activeSubject?.subjectId ?? 'primary';
  const activePeerClaim = peerClaims[activeSubjectKey] ?? null;
  const isPeerReview =
    (reviewMethods & REVIEW_METHOD_FLAGS.PeerReview) !== 0;
  const items = useMemo(
    () => (execution ? readQuizItems(execution) : []),
    [execution],
  );
  const scoreRows = items.map((item) => {
    const raw = scores[item.itemId]?.points ?? '';
    let score: ScoreValue | null = null;
    try {
      if (raw.trim()) score = pointsToScoreUnits(raw);
    } catch {
      score = null;
    }
    const maxScore = execution?.itemMaxScores[item.itemId] ?? 0;
    return {
      ...item,
      raw,
      score,
      maxScore,
      valid: score != null && score >= 0 && score <= maxScore,
    };
  });
  const canResolve =
    execution?.requiresInstructorReview === true &&
    scoreRows.length > 0 &&
    scoreRows.every((row) => row.valid) &&
    !busy;
  const allSubjectsSubmitted =
    subjects.length > 0 &&
    subjects.every((subject) => subject.execution.submittedResponse != null);
  const showPeerReview =
    isPeerReview &&
    execution?.submittedResponse != null &&
    !completedPeerReviewSubjects.has(activeSubjectKey);
  const peerTargetAnswers = useMemo(() => {
    if (!activePeerClaim?.responseEnvelopeCanonicalJson) return null;
    try {
      return parseQuizAnswerEnvelope(
        JSON.parse(activePeerClaim.responseEnvelopeCanonicalJson),
      ).payload.answers;
    } catch {
      return null;
    }
  }, [activePeerClaim]);
  const peerMaximum = execution
    ? Object.values(execution.itemMaxScores).reduce(
        (total, value) => total + value,
        0,
      )
    : 0;
  let parsedPeerScore: ScoreValue | null = null;
  try {
    if (peerScore.trim()) parsedPeerScore = pointsToScoreUnits(peerScore);
  } catch {
    parsedPeerScore = null;
  }
  const canSubmitPeerReview =
    activePeerClaim?.status === 'assigned' &&
    parsedPeerScore != null &&
    parsedPeerScore >= 0 &&
    parsedPeerScore <= peerMaximum &&
    peerFeedback.trim().length > 0 &&
    !busy;

  async function begin() {
    setBusy(true);
    setError(null);
    const result = await startAssessmentTestRun(
      assessmentId,
      revisionId,
      startKey.current,
      'instructor-test-learner',
      'Instructor test learner',
      isPeerReview
        ? Array.from(
            { length: Math.min(Math.max(peerReviewsRequired + 1, 2), 16) },
            (_, index) => ({
              personaKey: `peer-${index + 1}`,
              personaDisplayName: `Synthetic learner ${index + 1}`,
            }),
          )
        : undefined,
    );
    setBusy(false);
    if (!result.success) {
      setError(result.error);
      return;
    }
    adoptRun(
      result.data as AssessmentTestRunRuntimeViewV1<QuizLearnerDeliveryItemV1>,
    );
  }

  async function submit() {
    if (!run || !execution || execution.submittedResponse) return;
    const response = createQuizAnswerEnvelope(
      Object.fromEntries(
        items.map((item) => [
          item.itemId,
          answers[item.itemId] ?? createEmptyQuizAnswer(item.delivery.entry.type),
        ]),
      ),
    );
    const hash = JSON.stringify(response);
    if (submitCommand.current?.hash !== hash) {
      submitCommand.current = { hash, key: createIdempotencyKey() };
    }
    setBusy(true);
    setError(null);
    const result = await submitAssessmentTestRun(
      run.testRunId,
      response,
      submitCommand.current.key,
      activeSubject?.subjectId === 'primary' ? undefined : activeSubject?.subjectId,
    );
    setBusy(false);
    if (!result.success) {
      setError(result.error);
      return;
    }
    adoptRun(
      result.data as AssessmentTestRunRuntimeViewV1<QuizLearnerDeliveryItemV1>,
    );
  }

  async function resolveInstructorReview() {
    if (!run || !execution || !canResolve) return;
    const resolution: InstructorReviewResolutionV1 = {
      schemaVersion: 1,
      items: scoreRows.map((row) => ({
        itemId: row.itemId,
        score: row.score!,
        feedback: scores[row.itemId]?.feedback.trim() || null,
      })),
      feedback: overallFeedback.trim() || null,
      overrideReason: overrideReason.trim() || null,
    };
    setBusy(true);
    setError(null);
    const result = await resolveTestRunInstructorReview(
      run.testRunId,
      resolution,
      createIdempotencyKey(),
      activeSubject?.subjectId === 'primary' ? undefined : activeSubject?.subjectId,
    );
    setBusy(false);
    if (!result.success) {
      setError(result.error);
      return;
    }
    adoptRun(
      result.data as AssessmentTestRunRuntimeViewV1<QuizLearnerDeliveryItemV1>,
    );
  }

  async function submitSelfReview(evidence: SelfReviewEvidenceV1) {
    if (!run || !execution) return;
    const hash = JSON.stringify(evidence);
    if (selfReviewCommand.current?.hash !== hash) {
      selfReviewCommand.current = { hash, key: createIdempotencyKey() };
    }
    setBusy(true);
    setError(null);
    const result = await submitTestRunSelfReview(
      run.testRunId,
      evidence,
      selfReviewCommand.current.key,
      activeSubject?.subjectId === 'primary' ? undefined : activeSubject?.subjectId,
    );
    setBusy(false);
    if (!result.success) {
      setError(result.error);
      return;
    }
    selfReviewCommand.current = null;
    adoptRun(
      result.data as AssessmentTestRunRuntimeViewV1<QuizLearnerDeliveryItemV1>,
    );
  }

  async function claimPeerReview() {
    if (!run || activeSubjectKey === 'primary') return;
    setBusy(true);
    setError(null);
    const claimed = await claimTestRunPeerReview(
      run.testRunId,
      activeSubjectKey,
    );
    if (!claimed.success) {
      setBusy(false);
      setError(claimed.error);
      return;
    }
    const loaded = await getTestRunPeerReview(
      run.testRunId,
      claimed.data.reviewId,
    );
    setBusy(false);
    if (!loaded.success) {
      setError(loaded.error);
      return;
    }
    setPeerClaims((current) => ({
      ...current,
      [activeSubjectKey]: loaded.data,
    }));
    setPeerScore('');
    setPeerFeedback('');
  }

  async function submitPeerReview() {
    if (!run || !activePeerClaim || parsedPeerScore == null) return;
    setBusy(true);
    setError(null);
    const result = await submitTestRunPeerReview(
      run.testRunId,
      activePeerClaim.id,
      parsedPeerScore,
      peerFeedback.trim(),
    );
    setBusy(false);
    if (!result.success) {
      setError(result.error);
      return;
    }
    setCompletedPeerReviewSubjects((current) =>
      new Set(current).add(activeSubjectKey),
    );
    setPeerClaims((current) => {
      const next = { ...current };
      delete next[activeSubjectKey];
      return next;
    });
    setPeerScore('');
    setPeerFeedback('');
    adoptRun(
      result.data as AssessmentTestRunRuntimeViewV1<QuizLearnerDeliveryItemV1>,
    );
  }

  async function restart() {
    if (!run) return;
    setBusy(true);
    setError(null);
    const result = await restartAssessmentTestRun(
      run.testRunId,
      createIdempotencyKey(),
    );
    setBusy(false);
    if (!result.success) {
      setError(result.error);
      return;
    }
    submitCommand.current = null;
    selfReviewCommand.current = null;
    answerDrafts.current = {};
    setPeerClaims({});
    setCompletedPeerReviewSubjects(new Set());
    adoptRun(
      result.data as AssessmentTestRunRuntimeViewV1<QuizLearnerDeliveryItemV1>,
    );
  }

  function adoptRun(
    next: AssessmentTestRunRuntimeViewV1<QuizLearnerDeliveryItemV1>,
  ) {
    setRun(next);
    const nextSubjects = next.subjects?.length
      ? next.subjects
      : [
          {
            subjectId: 'primary',
            personaKey: next.personaKey,
            personaDisplayName: next.personaDisplayName,
            execution: next.execution,
          },
        ];
    const nextSubject =
      nextSubjects.find((subject) => subject.subjectId === selectedSubjectId) ??
      nextSubjects[0];
    setSelectedSubjectId(nextSubject.subjectId);
    adoptExecution(nextSubject.execution, nextSubject.subjectId);
  }

  function adoptExecution(
    nextExecution: AssessmentExecutionRuntimeViewV1<QuizLearnerDeliveryItemV1>,
    subjectId: string,
  ) {
    const nextItems = readQuizItems(nextExecution);
    let nextAnswers: Record<string, QuizAnswer> = {};
    if (nextExecution.submittedResponse) {
      nextAnswers = parseQuizAnswerEnvelope(nextExecution.submittedResponse)
        .payload.answers;
    } else if (answerDrafts.current[subjectId]) {
      nextAnswers = answerDrafts.current[subjectId].answers;
    } else {
      nextAnswers = Object.fromEntries(
        nextItems.map((item) => [
          item.itemId,
          createEmptyQuizAnswer(item.delivery.entry.type),
        ]),
      );
    }
    setAnswers(nextAnswers);
    setRecorded(
      nextExecution.submittedResponse
        ? new Set(nextItems.map((item) => item.itemId))
        : answerDrafts.current[subjectId]?.recorded ?? new Set(),
    );

    const resultByItem = new Map(
      (nextExecution.instructorVisibleResult?.items ?? []).map((item) => [
        item.itemId,
        item,
      ]),
    );
    setScores(
      Object.fromEntries(
        nextItems.map((item) => {
          const result = resultByItem.get(item.itemId);
          return [
            item.itemId,
            {
              points:
                result?.score == null
                  ? ''
                  : String(scoreUnitsToPoints(result.score)),
              feedback: result?.feedback ?? '',
            },
          ];
        }),
      ),
    );
    setOverallFeedback(nextExecution.instructorVisibleResult?.feedback ?? '');
    setOverrideReason('');
  }

  function selectSubject(subjectId: string) {
    if (!run || subjectId === activeSubjectKey) return;
    answerDrafts.current[activeSubjectKey] = { answers, recorded };
    const subject = subjects.find((candidate) => candidate.subjectId === subjectId);
    if (!subject) return;
    setSelectedSubjectId(subjectId);
    setPeerScore('');
    setPeerFeedback('');
    setError(null);
    adoptExecution(subject.execution, subjectId);
  }

  function updateAnswer(itemId: string, answer: QuizAnswer, markRecorded = false) {
    const nextAnswers = { ...answers, [itemId]: answer };
    const nextRecorded = markRecorded
      ? new Set([...recorded, itemId])
      : recorded;
    setAnswers(nextAnswers);
    if (markRecorded) setRecorded(nextRecorded);
    answerDrafts.current[activeSubjectKey] = {
      answers: nextAnswers,
      recorded: nextRecorded,
    };
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger render={<Button type="button" size="sm" variant="outline" disabled={disabled} />}>
        <Play className="mr-2 size-4" />
        Test assessment
      </DialogTrigger>
      <DialogContent className="flex h-[min(90vh,900px)] w-[calc(100vw-2rem)] max-w-6xl flex-col gap-0 overflow-hidden p-0">
        <DialogHeader className="border-b px-6 py-4">
          <div className="flex flex-wrap items-center gap-2">
            <DialogTitle>Assessment test run</DialogTitle>
            {run && <RunStatus run={run} />}
          </div>
          <DialogDescription>
            Exercises the candidate revision as a synthetic learner without
            creating academic results.
          </DialogDescription>
        </DialogHeader>

        <div className="min-h-0 flex-1 overflow-auto p-6">
          {!run && (
            <div className="grid min-h-64 place-items-center">
              <Button type="button" onClick={() => void begin()} disabled={busy}>
                {busy ? (
                  <Loader2 className="mr-2 size-4 animate-spin" />
                ) : (
                  <Play className="mr-2 size-4" />
                )}
                Start test run
              </Button>
            </div>
          )}

          {run && (
            <div className="space-y-6">
              {subjects.length > 1 && (
                <div
                  className="grid gap-1 rounded-md border bg-muted/30 p-1 sm:grid-cols-2 lg:grid-cols-4"
                  role="tablist"
                  aria-label="Synthetic learners"
                >
                  {subjects.map((subject) => (
                    <Button
                      key={subject.subjectId}
                      type="button"
                      size="sm"
                      variant={
                        subject.subjectId === activeSubjectKey
                          ? 'secondary'
                          : 'ghost'
                      }
                      role="tab"
                      aria-selected={subject.subjectId === activeSubjectKey}
                      onClick={() => selectSubject(subject.subjectId)}
                    >
                      {subject.personaDisplayName}
                      {subject.execution.instructorVisibleResult?.state ===
                        'final' && (
                        <CheckCircle2 className="ml-2 size-3" />
                      )}
                    </Button>
                  ))}
                </div>
              )}

              <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                <Badge variant="outline">
                  Revision {run.definitionRevisionId.slice(0, 8)}
                </Badge>
                <span>Delivery {execution?.deliveryHash.slice(0, 12)}</span>
                <span>{activeSubject?.personaDisplayName}</span>
              </div>

              <div className="space-y-4">
                {items.map((item, index) => (
                  <section
                    key={item.itemId}
                    className="space-y-3 rounded-md border bg-card p-4"
                  >
                    <div className="flex items-center justify-between gap-3">
                      <h3 className="text-sm font-semibold">
                        Question {index + 1}
                      </h3>
                      <Badge variant="secondary">
                        {scoreUnitsToPoints(
                          execution?.itemMaxScores[item.itemId] ?? 0,
                        )}{' '}
                        pts
                      </Badge>
                    </div>
                    <QuizPlayer
                      entry={item.delivery.entry}
                      answer={
                        answers[item.itemId] ??
                        createEmptyQuizAnswer(item.delivery.entry.type)
                      }
                      onAnswerChange={(answer) => updateAnswer(item.itemId, answer)}
                      onSubmit={(answer) => updateAnswer(item.itemId, answer, true)}
                      submissionResult={readPlayerResult(
                        execution!,
                        item.itemId,
                        recorded,
                      )}
                      disabled={Boolean(execution?.submittedResponse)}
                    />
                  </section>
                ))}
              </div>

              {!execution?.submittedResponse && (
                <Button type="button" onClick={() => void submit()} disabled={busy}>
                  {busy && <Loader2 className="mr-2 size-4 animate-spin" />}
                  Run grading workflow
                </Button>
              )}

              {execution?.submittedResponse && execution.selfReview && (
                <SelfReviewForm
                  items={items.map((item) => ({
                    itemId: item.itemId,
                    label: item.delivery.entry.stem || item.itemId,
                    maxScore: execution.itemMaxScores[item.itemId] ?? 0,
                  }))}
                  state={execution.selfReview}
                  busy={busy}
                  onSubmit={submitSelfReview}
                />
              )}

              {showPeerReview && (
                <section className="space-y-4 border-t pt-6">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div>
                      <h3 className="font-semibold">Peer review</h3>
                      <p className="text-sm text-muted-foreground">
                        Review an anonymous response as the selected synthetic learner.
                      </p>
                    </div>
                    {!activePeerClaim && (
                      <Button
                        type="button"
                        variant="outline"
                        onClick={() => void claimPeerReview()}
                        disabled={busy || !allSubjectsSubmitted}
                      >
                        {busy ? (
                          <Loader2 className="mr-2 size-4 animate-spin" />
                        ) : (
                          <ClipboardCheck className="mr-2 size-4" />
                        )}
                        Claim anonymous response
                      </Button>
                    )}
                  </div>

                  {activePeerClaim && peerTargetAnswers && (
                    <div className="space-y-4 rounded-md border bg-muted/20 p-4">
                      {items.map((item, index) => (
                        <div key={item.itemId} className="space-y-2">
                          <p className="text-sm font-medium">
                            Question {index + 1}
                          </p>
                          <QuizPlayer
                            entry={item.delivery.entry}
                            answer={
                              peerTargetAnswers[item.itemId] ??
                              createEmptyQuizAnswer(item.delivery.entry.type)
                            }
                            onAnswerChange={() => undefined}
                            onSubmit={() => undefined}
                            submissionResult={{
                              status: 'pending',
                              feedback: 'Anonymous response',
                            }}
                            disabled
                          />
                        </div>
                      ))}
                      <div className="grid gap-3 md:grid-cols-[10rem_minmax(0,1fr)]">
                        <div className="space-y-2">
                          <label
                            htmlFor={`peer-score-${activeSubjectKey}`}
                            className="text-sm font-medium"
                          >
                            Score / {scoreUnitsToPoints(peerMaximum)}
                          </label>
                          <Input
                            id={`peer-score-${activeSubjectKey}`}
                            type="number"
                            min={0}
                            max={scoreUnitsToPoints(peerMaximum)}
                            step="0.01"
                            value={peerScore}
                            onChange={(event) => setPeerScore(event.target.value)}
                          />
                        </div>
                        <div className="space-y-2">
                          <label
                            htmlFor={`peer-feedback-${activeSubjectKey}`}
                            className="text-sm font-medium"
                          >
                            Feedback
                          </label>
                          <Textarea
                            id={`peer-feedback-${activeSubjectKey}`}
                            value={peerFeedback}
                            onChange={(event) =>
                              setPeerFeedback(event.target.value)
                            }
                            rows={3}
                          />
                        </div>
                      </div>
                      <Button
                        type="button"
                        onClick={() => void submitPeerReview()}
                        disabled={!canSubmitPeerReview}
                      >
                        {busy && <Loader2 className="mr-2 size-4 animate-spin" />}
                        Submit peer review
                      </Button>
                    </div>
                  )}
                </section>
              )}

              {execution?.requiresInstructorReview && (
                <section className="space-y-4 border-t pt-6">
                  <div>
                    <h3 className="font-semibold">Instructor review</h3>
                    <p className="text-sm text-muted-foreground">
                      Resolve every item. The server calculates the total.
                    </p>
                  </div>
                  {scoreRows.map((row) => (
                    <div
                      key={row.itemId}
                      className="grid gap-3 rounded-md border p-3 md:grid-cols-[minmax(0,1fr)_8rem]"
                    >
                      <div className="space-y-2">
                        <p className="text-sm font-medium">
                          {row.delivery.entry.stem || row.itemId}
                        </p>
                        <Textarea
                          aria-label={`Feedback for ${row.itemId}`}
                          value={scores[row.itemId]?.feedback ?? ''}
                          onChange={(event) =>
                            setScores((current) => ({
                              ...current,
                              [row.itemId]: {
                                ...(current[row.itemId] ?? { points: '' }),
                                feedback: event.target.value,
                              },
                            }))
                          }
                          rows={2}
                        />
                      </div>
                      <div className="space-y-2">
                        <label
                          htmlFor={`test-score-${row.itemId}`}
                          className="text-sm font-medium"
                        >
                          Score / {scoreUnitsToPoints(row.maxScore)}
                        </label>
                        <Input
                          id={`test-score-${row.itemId}`}
                          type="number"
                          min={0}
                          max={scoreUnitsToPoints(row.maxScore)}
                          step="0.01"
                          value={row.raw}
                          onChange={(event) =>
                            setScores((current) => ({
                              ...current,
                              [row.itemId]: {
                                ...(current[row.itemId] ?? { feedback: '' }),
                                points: event.target.value,
                              },
                            }))
                          }
                        />
                      </div>
                    </div>
                  ))}
                  <Textarea
                    aria-label="Overall test feedback"
                    value={overallFeedback}
                    onChange={(event) => setOverallFeedback(event.target.value)}
                    placeholder="Overall feedback"
                    rows={3}
                  />
                  <Input
                    aria-label="Override reason"
                    value={overrideReason}
                    onChange={(event) => setOverrideReason(event.target.value)}
                    placeholder="Reason when changing an automated result"
                  />
                  <Button
                    type="button"
                    onClick={() => void resolveInstructorReview()}
                    disabled={!canResolve}
                  >
                    {busy && <Loader2 className="mr-2 size-4 animate-spin" />}
                    Complete instructor review
                  </Button>
                </section>
              )}

              {execution?.instructorVisibleResult?.state === 'final' && (
                <section className="space-y-3 rounded-md border bg-muted/30 p-4">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div>
                      <p className="font-semibold">Diagnostic result</p>
                      <p className="text-sm text-muted-foreground">
                        {scoreUnitsToPoints(
                          execution.instructorVisibleResult.score ?? 0,
                        )}{' '}
                        /{' '}
                        {scoreUnitsToPoints(
                          execution.instructorVisibleResult.maxScore,
                        )}
                      </p>
                    </div>
                    {run.readyForPublication ? (
                      <Badge>
                        <CheckCircle2 className="mr-1 size-3" />
                        Ready to publish
                      </Badge>
                    ) : (
                      <Badge variant="outline">
                        <AlertTriangle className="mr-1 size-3" />
                        Not ready to publish
                      </Badge>
                    )}
                  </div>
                  {run.diagnostics.length > 0 && (
                    <ul className="list-disc space-y-1 pl-5 text-sm text-muted-foreground">
                      {run.diagnostics.map((diagnostic) => (
                        <li key={diagnostic}>{diagnostic}</li>
                      ))}
                    </ul>
                  )}
                </section>
              )}
            </div>
          )}

          {error && (
            <div
              role="alert"
              className="mt-4 rounded-md border border-destructive bg-destructive/10 p-3 text-sm text-destructive"
            >
              {error}
            </div>
          )}
        </div>

        {run && (
          <div className="flex justify-end border-t px-6 py-3">
            <Button
              type="button"
              variant="outline"
              onClick={() => void restart()}
              disabled={busy}
            >
              <RotateCcw className="mr-2 size-4" />
              Restart test run
            </Button>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

function readQuizItems(
  execution: AssessmentExecutionRuntimeViewV1<QuizLearnerDeliveryItemV1>,
): Array<{ itemId: string; delivery: QuizLearnerDeliveryItemV1 }> {
  return execution.delivery.itemOrder.map((itemId) => {
    const item = execution.delivery.items[itemId];
    if (
      !item ||
      item.adapterKey !== QUIZ_ASSESSMENT_TYPE_ADAPTER.key ||
      item.adapterVersion !== QUIZ_ASSESSMENT_TYPE_ADAPTER.version
    ) {
      throw new Error(`Unsupported test-run delivery for item ${itemId}.`);
    }
    if (item.learnerPayload.itemId !== itemId) {
      throw new Error(`Test-run delivery item ${itemId} is mismatched.`);
    }
    return { itemId, delivery: item.learnerPayload };
  });
}

function readPlayerResult(
  execution: AssessmentExecutionRuntimeViewV1<QuizLearnerDeliveryItemV1>,
  itemId: string,
  recorded: ReadonlySet<string>,
): QuizSubmissionResult {
  const result = execution.instructorVisibleResult?.items.find(
    (item) => item.itemId === itemId,
  );
  if (result?.state === 'graded' && result.score != null) {
    return {
      status: result.score === result.maxScore ? 'correct' : 'incorrect',
      feedback: result.feedback ?? undefined,
    };
  }
  if (execution.submittedResponse || recorded.has(itemId)) {
    return {
      status: 'pending',
      feedback: execution.submittedResponse
        ? 'Submitted to the grading workflow.'
        : 'Answer recorded for this test run.',
    };
  }
  return { status: 'idle' };
}

function RunStatus({
  run,
}: {
  run: AssessmentTestRunRuntimeViewV1;
}): React.JSX.Element {
  return <Badge variant="secondary">{run.status}</Badge>;
}

function createIdempotencyKey(): string {
  return (
    globalThis.crypto?.randomUUID?.() ??
    `assessment-test-${Date.now()}-${Math.random()}`
  );
}
