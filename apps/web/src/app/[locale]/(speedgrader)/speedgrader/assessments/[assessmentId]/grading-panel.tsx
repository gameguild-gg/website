'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import type {
  LearningAssessmentsGradingQueueAssessment,
  LearningAssessmentsGradingQueueItem,
} from '@game-guild/client';
import type {
  AssessmentSubmissionRuntimeViewV1,
  GradeItemResultV1,
} from '@game-guild/grading';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Input } from '@game-guild/ui/components/input';
import { Textarea } from '@game-guild/ui/components/textarea';
import {
  getRuntimeSubmission,
  regradeRuntimeSubmission,
  releaseRuntimeSubmission,
  resolveRuntimeInstructorReview,
} from '@/lib/learning/grading-runtime-actions';
import {
  pointsToScoreUnits,
  scoreUnitsToPoints,
} from '@/lib/learning/academic-values';
import { useRouter } from '@/i18n/navigation';

const dateFormatter = new Intl.DateTimeFormat('en-US', {
  year: 'numeric',
  month: 'short',
  day: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
});

export interface GradingPanelProps {
  item: LearningAssessmentsGradingQueueItem;
  assessment: LearningAssessmentsGradingQueueAssessment;
}

interface ItemResolutionState {
  points: string;
  feedback: string;
}

export function GradingPanel(props: GradingPanelProps): React.JSX.Element {
  return <GradingPanelSession key={props.item.submissionId} {...props} />;
}

function GradingPanelSession({
  item,
  assessment,
}: GradingPanelProps): React.JSX.Element {
  const router = useRouter();
  const [submission, setSubmission] =
    useState<AssessmentSubmissionRuntimeViewV1 | null>(null);
  const [resolutions, setResolutions] = useState<
    Record<string, ItemResolutionState>
  >({});
  const [overallFeedback, setOverallFeedback] = useState('');
  const [criterionScores, setCriterionScores] = useState<Record<string, string>>({});
  const [overrideReason, setOverrideReason] = useState('');
  const [regradeReason, setRegradeReason] = useState('');
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const orderedItemIds = useMemo(() => {
    if (!submission) return [];
    const known = new Set(Object.keys(submission.execution.itemMaxScores));
    const ordered = submission.execution.delivery.itemOrder.filter((id) =>
      known.delete(id),
    );
    return [...ordered, ...known];
  }, [submission]);

  const acceptRuntimeSubmission = useCallback((nextSubmission: AssessmentSubmissionRuntimeViewV1) => {
    setSubmission(nextSubmission);
    const resultByItem = new Map(
      (nextSubmission.execution.instructorVisibleResult?.items ?? []).map(
        (result) => [result.itemId, result],
      ),
    );
    const next = Object.fromEntries(
      Object.entries(nextSubmission.execution.itemMaxScores).map(
        ([itemId]) => {
          const result = resultByItem.get(itemId);
          return [
            itemId,
            {
              points:
                result?.score == null
                  ? ''
                  : String(scoreUnitsToPoints(result.score)),
              feedback: result?.feedback ?? '',
            },
          ];
        },
      ),
    );
    setResolutions(next);
    setOverallFeedback(
      nextSubmission.execution.instructorVisibleResult?.feedback ?? '',
    );
    setCriterionScores(Object.fromEntries(Object.entries(
      nextSubmission.execution.instructorVisibleRubricScores ?? {},
    ).map(([id, units]) => [id, String(scoreUnitsToPoints(units))])));
  }, []);

  useEffect(() => {
    let cancelled = false;
    getRuntimeSubmission(item.submissionId ?? '').then((result) => {
      if (cancelled) return;
      setLoading(false);
      if (!result.success) {
        setError(result.error);
        return;
      }
      acceptRuntimeSubmission(result.data);
    });
    return () => { cancelled = true; };
  }, [item.submissionId, acceptRuntimeSubmission]);

  const frozenRubric = submission?.execution.instructorVisibleRubric;
  const rubricValues = frozenRubric?.criteria.map((criterion) => {
    try {
      const raw = criterionScores[criterion.id] ?? '';
      if (!raw.trim()) return null;
      const units = pointsToScoreUnits(raw);
      return units <= criterion.points ? units : null;
    } catch { return null; }
  });
  const rubricComplete = rubricValues?.every((value) => value !== null);
  const rubricTotal = rubricComplete ? rubricValues.reduce<number>((sum, value) => sum + value, 0) : 0;

  const itemRows = orderedItemIds.map((itemId) => {
    const maxUnits = submission?.execution.itemMaxScores[itemId] ?? 0;
    const raw = itemId === 'code' && frozenRubric
      ? rubricComplete ? String(scoreUnitsToPoints(rubricTotal)) : ''
      : resolutions[itemId]?.points ?? '';
    let scoreUnits: ReturnType<typeof pointsToScoreUnits> | null = null;
    try {
      if (raw.trim()) scoreUnits = pointsToScoreUnits(raw);
    } catch {
      scoreUnits = null;
    }
    return {
      itemId,
      label: readItemLabel(submission, itemId),
      maxUnits,
      maxPoints: scoreUnitsToPoints(maxUnits),
      raw,
      scoreUnits,
      valid: scoreUnits != null && scoreUnits <= maxUnits,
      prior: submission?.execution.instructorVisibleResult?.items.find(
        (entry) => entry.itemId === itemId,
      ),
    };
  });
  const canResolve =
    submission?.execution.requiresInstructorReview === true &&
    itemRows.length > 0 &&
    itemRows.every((row) => row.valid) &&
    !submitting;
  const finalResult =
    submission?.execution.instructorVisibleResult?.state === 'final'
      ? submission.execution.instructorVisibleResult
      : null;
  const canRelease = Boolean(
    submission &&
      finalResult &&
      !submission.execution.released &&
      submission.execution.activeRoundId,
  );

  async function submitReview() {
    if (!submission || !canResolve) return;
    setSubmitting(true);
    setError(null);
    const result = await resolveRuntimeInstructorReview(
      submission.submissionId,
      {
        schemaVersion: 1,
        items: itemRows.map((row) => ({
          itemId: row.itemId,
          score: row.scoreUnits!,
          feedback: resolutions[row.itemId]?.feedback.trim() || null,
        })),
        feedback: overallFeedback.trim() || null,
        overrideReason: overrideReason.trim() || null,
        ...(frozenRubric ? {
          rubricScores: Object.fromEntries(frozenRubric.criteria.map((criterion) => [
            criterion.id, pointsToScoreUnits(criterionScores[criterion.id] ?? ''),
          ])),
        } : {}),
      },
      createIdempotencyKey(),
    );
    setSubmitting(false);
    if (!result.success) {
      setError(result.error);
      return;
    }
    acceptRuntimeSubmission(result.data);
    router.refresh();
  }

  async function releaseResult() {
    if (!submission || !submission.execution.activeRoundId) return;
    setSubmitting(true);
    setError(null);
    const result = await releaseRuntimeSubmission(
      {
        submissionId: submission.submissionId,
        version: submission.version,
        expectedRoundId: submission.execution.activeRoundId,
      },
      'Released by instructor from SpeedGrader.',
      createIdempotencyKey(),
    );
    setSubmitting(false);
    if (!result.success) {
      setError(result.error);
      return;
    }
    const refreshed = await getRuntimeSubmission(submission.submissionId);
    if (refreshed.success) acceptRuntimeSubmission(refreshed.data);
    router.refresh();
  }

  async function startRegrade() {
    if (!submission || !regradeReason.trim()) return;
    setSubmitting(true);
    setError(null);
    const result = await regradeRuntimeSubmission(
      submission.submissionId,
      regradeReason.trim(),
      createIdempotencyKey(),
    );
    setSubmitting(false);
    if (!result.success) {
      setError(result.error);
      return;
    }
    acceptRuntimeSubmission(result.data);
    setRegradeReason('');
    router.refresh();
  }

  return (
    <div data-testid="grading-panel" className="h-full space-y-4 overflow-auto p-4">
      <div data-testid="attempt-meta" className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
        <span>
          attempt {item.attemptNumber ?? 1}
          {item.attemptCount ? ` of ${item.attemptCount}` : ''}
        </span>
        {item.submittedAt && (
          <span>· {dateFormatter.format(new Date(item.submittedAt))}</span>
        )}
        {item.isLate && <Badge variant="destructive">Late</Badge>}
        {submission?.execution.requiresInstructorReview && (
          <Badge variant="secondary">Instructor review</Badge>
        )}
        {submission?.execution.released && <Badge>Released</Badge>}
      </div>

      {item.isGroup && (item.memberNames?.length ?? 0) > 0 && (
        <div data-testid="group-banner" className="rounded-md border bg-muted/40 p-3">
          <p className="text-sm font-medium">
            One result applies to {item.memberNames?.length} frozen participants
          </p>
          <div data-testid="group-members" className="mt-2 flex flex-wrap gap-1">
            {item.memberNames?.map((name) => (
              <Badge key={name} variant="secondary">{name}</Badge>
            ))}
          </div>
        </div>
      )}

      {loading && <p className="text-sm text-muted-foreground">Loading grading round...</p>}

      {!loading && submission && (
        <>
          {frozenRubric && (
            <section data-testid="rubric-grid" className="space-y-3" aria-label="Frozen scoring rubric">
              <h2 className="text-sm font-semibold">{frozenRubric.title}</h2>
              {frozenRubric.criteria.map((criterion) => (
                <div key={criterion.id} data-testid={`criterion-row-${criterion.id}`} className="space-y-2 rounded-md border p-3">
                  <label htmlFor={`criterion-${criterion.id}`} className="text-sm font-medium">{criterion.description}</label>
                  <div className="flex items-center gap-2">
                    <Input id={`criterion-${criterion.id}`} data-testid={`criterion-points-${criterion.id}`}
                      type="number" min={0} max={scoreUnitsToPoints(criterion.points)} step="0.01"
                      value={criterionScores[criterion.id] ?? ''}
                      disabled={!submission.execution.requiresInstructorReview || submitting}
                      onChange={(event) => setCriterionScores((current) => ({ ...current, [criterion.id]: event.target.value }))}
                      className="w-28" />
                    <span>/ {scoreUnitsToPoints(criterion.points)}</span>
                  </div>
                </div>
              ))}
              <p data-testid="rubric-total">Total: {scoreUnitsToPoints(rubricTotal)}</p>
            </section>
          )}
          <section className="space-y-3" aria-label="Item scores">
            <div className="flex items-center justify-between gap-2">
              <h2 className="text-sm font-semibold">Item scores</h2>
              <span className="text-xs text-muted-foreground">
                Total is calculated by the server
              </span>
            </div>
            {itemRows.map((row) => (
              <div key={row.itemId} className="space-y-2 rounded-md border p-3">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="text-sm font-medium">{row.label}</p>
                    <p className="text-xs text-muted-foreground">{row.itemId}</p>
                  </div>
                  {row.prior && <ResultStateBadge result={row.prior} />}
                </div>
                <div className="flex items-center gap-2">
                  <Input
                    data-testid={`item-score-${row.itemId}`}
                    aria-label={`Score for ${row.label}`}
                    type="number"
                    min={0}
                    max={row.maxPoints}
                    step="0.01"
                    value={row.raw}
                    disabled={!submission.execution.requiresInstructorReview || submitting ||
                      (row.itemId === 'code' && Boolean(frozenRubric))}
                    onChange={(event) =>
                      setResolutions((current) => ({
                        ...current,
                        [row.itemId]: {
                          ...(current[row.itemId] ?? { feedback: '' }),
                          points: event.target.value,
                        },
                      }))
                    }
                    className="w-28"
                  />
                  <span className="text-sm text-muted-foreground">/ {row.maxPoints}</span>
                </div>
                {row.raw && !row.valid && (
                  <p role="alert" className="text-xs text-destructive">
                    Enter a value from 0 to {row.maxPoints}.
                  </p>
                )}
                <Input
                  value={resolutions[row.itemId]?.feedback ?? ''}
                  disabled={!submission.execution.requiresInstructorReview || submitting}
                  onChange={(event) =>
                    setResolutions((current) => ({
                      ...current,
                      [row.itemId]: {
                        ...(current[row.itemId] ?? { points: '' }),
                        feedback: event.target.value,
                      },
                    }))
                  }
                  placeholder="Item feedback"
                />
              </div>
            ))}
          </section>

          <div className="space-y-2">
            <label htmlFor="overall-feedback" className="text-sm font-medium">Overall feedback</label>
            <Textarea
              id="overall-feedback"
              value={overallFeedback}
              disabled={!submission.execution.requiresInstructorReview || submitting}
              onChange={(event) => setOverallFeedback(event.target.value)}
              rows={3}
            />
          </div>

          {submission.execution.requiresInstructorReview && (
            <div className="space-y-2">
              <label htmlFor="override-reason" className="text-sm font-medium">Override reason</label>
              <Input
                id="override-reason"
                value={overrideReason}
                onChange={(event) => setOverrideReason(event.target.value)}
                placeholder="Required when policy demands an override reason"
              />
            </div>
          )}

          {finalResult && (
            <div className="rounded-md border bg-muted/40 p-3 text-sm">
              Final result: {scoreUnitsToPoints(finalResult.score ?? 0)} /{' '}
              {scoreUnitsToPoints(finalResult.maxScore)}
            </div>
          )}

          <div className="flex flex-wrap gap-2">
            {submission.execution.requiresInstructorReview && (
              <Button
                type="button"
                data-testid={frozenRubric ? 'submit-grade' : 'resolve-instructor-review'}
                onClick={() => void submitReview()}
                disabled={!canResolve}
              >
                Finalize review
              </Button>
            )}
            {canRelease && (
              <Button
                type="button"
                variant="outline"
                data-testid="release-result"
                onClick={() => void releaseResult()}
                disabled={submitting}
              >
                Release result
              </Button>
            )}
          </div>

          {finalResult && (
            <div className="space-y-2 border-t pt-4">
              <label htmlFor="regrade-reason" className="text-sm font-medium">Regrade</label>
              <div className="flex gap-2">
                <Input
                  id="regrade-reason"
                  value={regradeReason}
                  onChange={(event) => setRegradeReason(event.target.value)}
                  placeholder="Reason for opening a new round"
                />
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => void startRegrade()}
                  disabled={submitting || !regradeReason.trim()}
                >
                  Start regrade
                </Button>
              </div>
            </div>
          )}

          {submission.execution.history.length > 0 && (
            <section className="space-y-2 border-t pt-4">
              <h2 className="text-sm font-semibold">Round history</h2>
              {submission.execution.history.map((round) => (
                <div key={round.roundId} className="flex items-center justify-between gap-2 text-sm">
                  <span>Round {round.roundNumber}: {round.reason}</span>
                  <Badge variant="outline">
                    {round.status}{round.released ? ' · released' : ''}
                  </Badge>
                </div>
              ))}
            </section>
          )}
        </>
      )}

      {error && (
        <div role="alert" className="rounded-md border border-destructive bg-destructive/10 p-3 text-sm text-destructive">
          {error}
        </div>
      )}

      {!loading && !submission && !error && (
        <p className="text-sm text-muted-foreground">
          No runtime grading execution is available for {assessment.title ?? 'this assessment'}.
        </p>
      )}
    </div>
  );
}

function readItemLabel(
  submission: AssessmentSubmissionRuntimeViewV1 | null,
  itemId: string,
): string {
  const payload = submission?.execution.delivery.items[itemId]?.learnerPayload;
  if (isRecord(payload) && isRecord(payload.entry) && typeof payload.entry.stem === 'string') {
    return payload.entry.stem;
  }
  return `Item ${itemId}`;
}

function ResultStateBadge({ result }: { result: GradeItemResultV1 }) {
  return (
    <Badge variant={result.state === 'graded' ? 'secondary' : 'outline'}>
      {result.state}
    </Badge>
  );
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function createIdempotencyKey(): string {
  return globalThis.crypto?.randomUUID?.() ?? `grading-${Date.now()}-${Math.random()}`;
}
