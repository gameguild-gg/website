'use client';

import { useEffect, useMemo, useState } from 'react';
import type {
  LearningAssessmentsGradingQueueAssessment,
  LearningAssessmentsGradingQueueItem,
  LearningAssessmentsInstructorPeerReview,
  LearningAssessmentsRubricCriterion,
} from '@game-guild/client';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Input } from '@game-guild/ui/components/input';
import { Textarea } from '@game-guild/ui/components/textarea';
import { hasReviewMethod } from '@/lib/learning/assessment-grading-methods';
import { parseReviewMethods, parseScoreValue } from '@game-guild/grading';
import { gradeSubmission } from '@/lib/learning/grade-action';
import { pointsToScoreUnits, scoreUnitsToPoints } from '@/lib/learning/academic-values';
import { composeFeedback } from './compose-feedback';
import { fetchPeerReviewsAction } from './speedgrader-actions';
import type { ComputedScore } from './code-grader-panel';
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
  /** Queue assessment summary — carries hasRubric + rubric (with criterion ids). */
  assessment: LearningAssessmentsGradingQueueAssessment;
  /** Run-tests result seeded from the code viewer (coding assessments). */
  computedScore?: ComputedScore | null;
}

interface CriterionState {
  points: string;
  comment: string;
}

function parsePointInput(value: string): { points: number; units: number } | null {
  if (value.trim() === '') return null;
  try {
    return { points: Number(value), units: pointsToScoreUnits(value) };
  } catch {
    return null;
  }
}

function sortedCriteria(rubric: LearningAssessmentsGradingQueueAssessment['rubric']): LearningAssessmentsRubricCriterion[] {
  return [...(rubric?.criteria ?? [])].sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
}

/**
 * SpeedGrader right panel. Rubric mode (assessment.hasRubric): per-criterion
 * points capped [0..criterion.Points] + comment; the score is AUTO-DERIVED
 * from Σ — no manual score input, and submit is enabled iff every criterion
 * is within its cap (partial credit is the normal case; Σ need NOT equal
 * maxScore). Plain mode: single score input 0..maxScore.
 */
export function GradingPanel({ item, assessment, computedScore }: GradingPanelProps): React.JSX.Element {
  const router = useRouter();
  const criteria = useMemo(() => sortedCriteria(assessment.rubric), [assessment.rubric]);
  const rubricMode = assessment.hasRubric === true && criteria.length > 0;
  const maxScoreUnits = parseScoreValue(assessment.maxScore ?? 10_000);
  const maxScore = scoreUnitsToPoints(maxScoreUnits);

  const [criterionState, setCriterionState] = useState<Record<string, CriterionState>>(() =>
    Object.fromEntries(criteria.map((criterion) => [criterion.id ?? '', { points: '', comment: '' }])),
  );
  const [plainScore, setPlainScore] = useState<string>('');
  const [overallComment, setOverallComment] = useState('');
  const [autoFeedback, setAutoFeedback] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const [reviews, setReviews] = useState<LearningAssessmentsInstructorPeerReview[] | null>(null);
  const peerReviewEnabled = hasReviewMethod(
    parseReviewMethods(
      (assessment as typeof assessment & { reviewMethods?: unknown }).reviewMethods ?? 0,
      { allowDraft: true },
    ),
    'PeerReview',
  );

  // Reset per-item grading state when the queue item changes.
  useEffect(() => {
    setCriterionState(Object.fromEntries(criteria.map((criterion) => [criterion.id ?? '', { points: '', comment: '' }])));
    setPlainScore('');
    setOverallComment('');
    setAutoFeedback('');
    setError(null);
    setReviews(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item.submissionId]);

  // Seed the plain score input + auto feedback from the code viewer's
  // run-tests result (ignored in rubric mode — the score is Σ, read-only).
  useEffect(() => {
    if (computedScore) {
      setPlainScore(String(computedScore.score));
      setAutoFeedback(computedScore.autoFeedback);
    }
  }, [computedScore]);

  useEffect(() => {
    if (!peerReviewEnabled || !item.submissionId) return;
    let cancelled = false;
    fetchPeerReviewsAction(item.submissionId).then((result) => {
      if (cancelled) return;
      setReviews(result.ok ? result.reviews : []);
    });
    return () => {
      cancelled = true;
    };
  }, [peerReviewEnabled, item.submissionId]);

  // --- Rubric validation ---------------------------------------------------

  const rows = criteria.map((criterion) => {
    const id = criterion.id ?? '';
    const raw = criterionState[id]?.points ?? '';
    const capUnits = parseScoreValue(criterion.points ?? 0);
    const cap = scoreUnitsToPoints(capUnits);
    const parsed = parsePointInput(raw);
    const inRange = parsed !== null && parsed.units <= capUnits;
    return {
      criterion,
      id,
      raw,
      parsed: parsed?.points ?? Number.NaN,
      units: parsed?.units ?? 0,
      cap,
      inRange,
      filled: raw.trim() !== '',
    };
  });
  const totalUnits = rows.reduce((sum, row) => sum + (row.inRange ? row.units : 0), 0);
  const total = scoreUnitsToPoints(totalUnits);
  const totalAboveMax = totalUnits > maxScoreUnits;
  const rubricComplete = rows.every((row) => row.filled && row.inRange);
  const rubricValid = rubricComplete && !totalAboveMax;

  // --- Plain validation ----------------------------------------------------

  const plain = parsePointInput(plainScore);
  const plainParsed = plain?.points ?? Number.NaN;
  const plainInRange = plain !== null && plain.units <= maxScoreUnits;
  const plainValid = plainScore.trim() !== '' && plainInRange;

  const canSubmit = !submitting && (rubricMode ? rubricValid : plainValid);

  async function handleSubmit() {
    if (!canSubmit || !item.submissionId) return;
    setSubmitting(true);
    setError(null);
    try {
      const feedback = composeFeedback({
        overallComment,
        autoFeedback,
      });
      const result = await gradeSubmission(
        rubricMode
          ? {
              submissionId: item.submissionId,
              score: total,
              feedback,
              rubricScores: JSON.stringify(
                Object.fromEntries(
                  rows.map((row) => [
                    row.id,
                    {
                      points: row.parsed,
                      comment: (criterionState[row.id]?.comment ?? '').trim(),
                    },
                  ]),
                ),
              ),
            }
          : {
              submissionId: item.submissionId,
              score: plainParsed,
              feedback,
            },
      );
      if (!result.success) {
        setError(result.error);
        setSubmitting(false);
        return;
      }
      // Keep position; refresh so status/score update in the queue + header.
      router.refresh();
      setSubmitting(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
      setSubmitting(false);
    }
  }

  return (
    <div data-testid="grading-panel" className="h-full space-y-4 overflow-auto p-4">
      {/* Attempt meta */}
      <div data-testid="attempt-meta" className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
        <span>attempt {item.attemptNumber ?? 1}{item.attemptCount ? ` of ${item.attemptCount}` : ''}</span>
        {item.submittedAt && <span>· {dateFormatter.format(new Date(item.submittedAt))}</span>}
        {item.isLate && (
          <Badge data-testid="late-badge" variant="destructive">
            Late
          </Badge>
        )}
        {item.status && <Badge variant="outline">{item.status}</Badge>}
        {item.assignmentScore != null && (
          <Badge data-testid="assignment-score-badge" variant="secondary">
            Assignment: {scoreUnitsToPoints(item.assignmentScore)}/{maxScore}
          </Badge>
        )}
        {item.assignmentPassed != null && (
          <Badge data-testid="assignment-passed-badge" variant={item.assignmentPassed ? 'default' : 'destructive'}>
            {item.assignmentPassed ? 'Passed' : 'Not passed'}
          </Badge>
        )}
      </div>

      {/* Group banner */}
      {item.isGroup && (item.memberNames?.length ?? 0) > 0 && (
        <div data-testid="group-banner" className="rounded-md border bg-muted/40 p-3">
          <p className="text-sm font-medium">Grade applies to {item.memberNames?.length} members</p>
          <div data-testid="group-members" className="mt-2 flex flex-wrap gap-1">
            {item.memberNames?.map((name) => (
              <Badge key={name} variant="secondary">
                {name}
              </Badge>
            ))}
          </div>
        </div>
      )}

      {/* Score area */}
      {rubricMode ? (
        <div data-testid="rubric-grid" className="space-y-3">
          {rows.map((row) => (
            <div key={row.id} data-testid={`criterion-row-${row.id}`} className="space-y-2 rounded-md border p-3">
              <div className="flex items-start justify-between gap-2">
                <p className="text-sm font-medium">{row.criterion.description || 'Criterion'}</p>
                <span className="whitespace-nowrap text-xs text-muted-foreground">
                  0..{row.cap} · / {row.cap}
                </span>
              </div>
              <div className="flex items-center gap-2">
                <Input
                  data-testid={`criterion-points-${row.id}`}
                  type="number"
                  min={0}
                  max={row.cap}
                  step="0.01"
                  value={row.raw}
                  onChange={(e) =>
                    setCriterionState((prev) => ({
                      ...prev,
                      [row.id]: {
                        ...(prev[row.id] ?? { points: '', comment: '' }),
                        points: e.target.value,
                      },
                    }))
                  }
                  className="w-24"
                  aria-label={`Points for ${row.criterion.description ?? row.id}`}
                />
                {row.filled && !row.inRange && (
                  <p data-testid={`criterion-error-${row.id}`} className="text-xs text-destructive" role="alert">
                    Enter 0 to {row.cap}
                  </p>
                )}
              </div>
              <Input
                data-testid={`criterion-comment-${row.id}`}
                value={criterionState[row.id]?.comment ?? ''}
                onChange={(e) =>
                  setCriterionState((prev) => ({
                    ...prev,
                    [row.id]: {
                      ...(prev[row.id] ?? { points: '', comment: '' }),
                      comment: e.target.value,
                    },
                  }))
                }
                placeholder="Comment (optional)"
                className="text-sm"
              />
            </div>
          ))}
          <div className="flex items-center justify-between rounded-md bg-muted/40 p-3">
            <span className="text-sm text-muted-foreground">Total</span>
            <span data-testid="rubric-total" className={`text-sm font-semibold tabular-nums ${totalAboveMax ? 'text-destructive' : ''}`}>
              {total}
            </span>
          </div>
          {totalAboveMax && (
            <p className="text-xs text-destructive" role="alert">
              Total exceeds the assessment max score ({maxScore}).
            </p>
          )}
          <p data-testid="derived-score" className="text-sm font-medium text-muted-foreground">
            Score: {total} / {maxScore} (auto-derived from rubric)
          </p>
        </div>
      ) : (
        <div className="space-y-2">
          <label htmlFor="plain-score" className="text-sm font-medium">
            Score
          </label>
          <Input
            id="plain-score"
            data-testid="plain-score-input"
            type="number"
            min={0}
            max={maxScore}
            step="0.01"
            value={plainScore}
            onChange={(e) => setPlainScore(e.target.value)}
            className="w-28"
          />
          <span className="text-sm text-muted-foreground">out of {maxScore}</span>
          {plainScore.trim() !== '' && !plainInRange && (
            <p data-testid="plain-score-error" className="text-xs text-destructive" role="alert">
              Enter 0 to {maxScore}
            </p>
          )}
        </div>
      )}

      {/* Overall comment */}
      <div className="space-y-2">
        <label htmlFor="overall-comment" className="text-sm font-medium">
          Overall comment
        </label>
        <Textarea
          id="overall-comment"
          data-testid="overall-comment"
          rows={4}
          value={overallComment}
          onChange={(e) => setOverallComment(e.target.value)}
          placeholder="Overall feedback for the student"
        />
      </div>

      {error && (
        <div role="alert" className="rounded-md border border-destructive bg-destructive/10 p-3 text-sm text-destructive">
          {error}
        </div>
      )}

      <Button type="button" onClick={handleSubmit} disabled={!canSubmit} data-testid="submit-grade">
        Submit grade
      </Button>

      {/* Peer reviews (instructor-named; todo 8d endpoint) */}
      {peerReviewEnabled && (
        <section data-testid="peer-reviews" className="space-y-2 border-t pt-4">
          <h2 className="text-sm font-semibold">Peer reviews</h2>
          {reviews === null && <p className="text-sm text-muted-foreground">Loading peer reviews…</p>}
          {reviews?.length === 0 && <p className="text-sm text-muted-foreground">No peer reviews submitted yet.</p>}
          {reviews?.map((review) => (
            <div key={review.reviewId} data-testid={`peer-review-${review.reviewId}`} className="space-y-1 rounded-md border p-3">
              <div className="flex items-center justify-between gap-2">
                <span className="text-sm font-medium">{review.reviewerName ?? 'Unknown reviewer'}</span>
                {review.score != null && (
                  <Badge variant="secondary">
                    {scoreUnitsToPoints(review.score)}/{maxScore}
                  </Badge>
                )}
              </div>
              {review.feedback && <p className="text-sm text-muted-foreground">{review.feedback}</p>}
            </div>
          ))}
        </section>
      )}
    </div>
  );
}
