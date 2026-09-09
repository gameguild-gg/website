'use client';

import { useEffect, useMemo, useState } from 'react';
import type { LearningAssessmentsAnonymousReviewSubmission, LearningAssessmentsRubricCriterion } from '@game-guild/client';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Input } from '@game-guild/ui/components/input';
import { Textarea } from '@game-guild/ui/components/textarea';
import { TextViewer } from '@/app/[locale]/(speedgrader)/speedgrader/assessments/[assessmentId]/text-viewer';
import { UrlViewer } from '@/app/[locale]/(speedgrader)/speedgrader/assessments/[assessmentId]/url-viewer';
import { FileViewer } from '@/app/[locale]/(speedgrader)/speedgrader/assessments/[assessmentId]/file-viewer';
import { MediaViewer } from '@/app/[locale]/(speedgrader)/speedgrader/assessments/[assessmentId]/media-viewer';
import { fetchPeerReviewWorkspace, submitPeerReview } from '@/lib/learning/actions-peer-review';
import { pointsToScoreUnits, scoreUnitsToPoints } from '@/lib/learning/academic-values';
import { parseScoreValue } from '@game-guild/grading';
import { useRouter } from '@/i18n/navigation';

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

function sortedCriteria(rubric: LearningAssessmentsAnonymousReviewSubmission['rubric']): LearningAssessmentsRubricCriterion[] {
  return [...(rubric?.criteria ?? [])].sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
}

/**
 * Student peer-review workspace. The DTO carries no identity fields and this
 * component renders none — the header names the submission "Anonymous".
 * Viewers are imported from the (speedgrader) route folder (single source).
 */
export function ReviewWorkspace({ reviewId }: { reviewId: string }): React.JSX.Element {
  const router = useRouter();
  const [review, setReview] = useState<LearningAssessmentsAnonymousReviewSubmission | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setReview(null);
    setLoadError(null);
    fetchPeerReviewWorkspace(reviewId).then((result) => {
      if (cancelled) return;
      if (result.ok) {
        setReview(result.review);
      } else {
        setLoadError(result.error);
      }
    });
    return () => {
      cancelled = true;
    };
  }, [reviewId]);

  if (loadError) {
    return (
      <div role="alert" className="p-4 text-sm text-destructive">
        {loadError}
      </div>
    );
  }
  if (!review) {
    return <p className="p-4 text-sm text-muted-foreground">Loading review…</p>;
  }

  return <WorkspaceForm key={reviewId} review={review} onDone={() => router.push('/learn/reviews')} />;
}

function WorkspaceForm({ review, onDone }: { review: LearningAssessmentsAnonymousReviewSubmission; onDone: () => void }): React.JSX.Element {
  const criteria = useMemo(() => sortedCriteria(review.rubric), [review.rubric]);
  const rubricMode = (review.rubric?.criteria?.length ?? 0) > 0;
  const maxScoreUnits = parseScoreValue(review.assessment?.maxScore ?? 10_000);
  const maxScore = scoreUnitsToPoints(maxScoreUnits);
  const title = review.assessment?.title ?? 'Untitled assessment';

  const [criterionState, setCriterionState] = useState<Record<string, CriterionState>>(() =>
    Object.fromEntries(criteria.map((criterion) => [criterion.id ?? '', { points: '', comment: '' }])),
  );
  const [plainScore, setPlainScore] = useState('');
  const [feedback, setFeedback] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

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
  const rubricComplete = rows.every((row) => row.filled && row.inRange);

  const plain = parsePointInput(plainScore);
  const plainParsed = plain?.points ?? Number.NaN;
  const plainInRange = plain !== null && plain.units <= maxScoreUnits;

  const canSubmit = !submitting && (rubricMode ? rubricComplete : plainInRange);

  async function handleSubmit() {
    if (!canSubmit) return;
    if (!feedback.trim()) {
      setError('Feedback comment is required');
      return;
    }
    setSubmitting(true);
    setError(null);
    const result = await submitPeerReview(
      review.reviewId ?? '',
      rubricMode
        ? {
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
        : { score: plainParsed, feedback },
    );
    if (!result.success) {
      setError(result.error);
      setSubmitting(false);
      return;
    }
    onDone();
  }

  if (review.status === 'Submitted') {
    return (
      <div className="space-y-4 p-4">
        <h1 data-testid="anonymous-header" className="text-lg font-semibold">
          Anonymous submission · attempt {review.attemptNumber ?? 1} · {title}
        </h1>
        <p className="text-sm text-muted-foreground">Review already submitted.</p>
      </div>
    );
  }

  return (
    <div className="space-y-6 p-4">
      <header className="space-y-1">
        <h1 data-testid="anonymous-header" className="text-lg font-semibold">
          Anonymous submission · attempt {review.attemptNumber ?? 1} · {title}
        </h1>
        <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <Badge variant="outline">Peer review</Badge>
          {review.submittedAt && <span>submitted {new Date(review.submittedAt).toLocaleDateString()}</span>}
          <span>
            Score: {rubricMode ? total : plainParsed} / {maxScore}
            {rubricMode ? ' (auto-derived from rubric)' : ''}
          </span>
        </div>
      </header>

      <section data-testid="peer-submission" className="space-y-4">
        {review.textPayload && <TextViewer text={review.textPayload} />}
        {review.urlPayload && <UrlViewer url={review.urlPayload} />}
        {review.filePayload && <FileViewer payload={review.filePayload} />}
        {review.mediaPayload && <MediaViewer url={review.mediaPayload} />}
        {review.codePayload && (
          <pre data-testid="peer-code-payload" className="overflow-auto rounded-md border bg-muted p-3 text-xs text-muted-foreground">
            {review.codePayload}
          </pre>
        )}
        {!review.textPayload && !review.urlPayload && !review.filePayload && !review.mediaPayload && !review.codePayload && (
          <p className="text-sm text-muted-foreground">This submission has no viewable payload.</p>
        )}
      </section>

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
            <span data-testid="rubric-total" className="text-sm font-semibold tabular-nums">
              {total}
            </span>
          </div>
        </div>
      ) : (
        <div className="space-y-2">
          <label htmlFor="peer-score" className="text-sm font-medium">
            Score
          </label>
          <Input
            id="peer-score"
            data-testid="peer-score-input"
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
            <p data-testid="peer-score-error" className="text-xs text-destructive" role="alert">
              Enter 0 to {maxScore}
            </p>
          )}
        </div>
      )}

      <div className="space-y-2">
        <label htmlFor="peer-feedback" className="text-sm font-medium">
          Feedback <span className="text-destructive">*</span>
        </label>
        <Textarea
          id="peer-feedback"
          data-testid="peer-feedback"
          rows={4}
          value={feedback}
          onChange={(e) => setFeedback(e.target.value)}
          placeholder="Feedback for your peer (required)"
        />
      </div>

      {error && (
        <div role="alert" className="rounded-md border border-destructive bg-destructive/10 p-3 text-sm text-destructive">
          {error}
        </div>
      )}

      <Button type="button" onClick={handleSubmit} disabled={!canSubmit} data-testid="submit-review">
        Submit review
      </Button>
    </div>
  );
}
