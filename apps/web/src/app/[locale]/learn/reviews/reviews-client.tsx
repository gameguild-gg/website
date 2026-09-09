'use client';

import { useState } from 'react';
import type { LearningAssessmentsReceivedPeerReview } from '@game-guild/client';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Card, CardContent, CardHeader, CardTitle } from '@game-guild/ui/components/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@game-guild/ui/components/tabs';
import { claimPeerReview } from '@/lib/learning/actions-peer-review';
import type { LearningTask } from '@/lib/learning/queries/tasks';
import type { ReceivedFeedbackGroup } from './page';
import { useRouter } from '@/i18n/navigation';
import { scoreUnitsToPoints } from '@/lib/learning/academic-values';

const dateFormatter = new Intl.DateTimeFormat('en-US', {
  year: 'numeric',
  month: 'short',
  day: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
});

function RubricScoresSummary({ payload }: { payload: string }) {
  let entries: { points: number; comment?: string }[] = [];
  try {
    const parsed: unknown = JSON.parse(payload);
    if (parsed && typeof parsed === 'object') {
      entries = Object.values(parsed as Record<string, { points?: number; comment?: string }>).map((entry) => ({
        points: scoreUnitsToPoints(entry?.points ?? 0),
        comment: entry?.comment,
      }));
    }
  } catch {
    return null;
  }
  if (entries.length === 0) return null;
  return (
    <div data-testid="rubric-scores-summary" className="flex flex-wrap gap-1">
      {entries.map((entry, index) => (
        <Badge key={index} variant="outline">
          {entry.points} pts{entry.comment ? ` — ${entry.comment}` : ''}
        </Badge>
      ))}
    </div>
  );
}

function ReviewTaskCard({ task }: { task: LearningTask }) {
  const router = useRouter();
  const [claiming, setClaiming] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const completed = task.reviewsCompleted ?? 0;
  const required = task.reviewsRequired ?? 0;
  const remaining = Math.max(required - completed, 0);

  async function handleClaim() {
    setClaiming(true);
    setError(null);
    const result = await claimPeerReview(task.assessmentId);
    if (!result.success) {
      setError(result.error);
      setClaiming(false);
      return;
    }
    router.push(`/learn/reviews/${result.data.reviewId}`);
  }

  return (
    <Card data-testid={`review-task-${task.assessmentId}`}>
      <CardHeader>
        <div className="flex min-w-0 items-start justify-between gap-3">
          <div className="min-w-0">
            <CardTitle className="truncate text-base">{task.assessmentTitle}</CardTitle>
            <p className="mt-1 text-sm text-muted-foreground">{task.courseTitle}</p>
          </div>
          <Badge variant="secondary">
            {completed} / {required} reviews completed
          </Badge>
        </div>
        <div className="mt-3 flex items-center justify-between gap-3">
          <span className="text-xs text-muted-foreground">{task.dueAt ? `Due ${dateFormatter.format(new Date(task.dueAt))}` : 'No due date'}</span>
          {remaining > 0 ? (
            <Button size="sm" onClick={handleClaim} disabled={claiming} data-testid={`claim-review-${task.assessmentId}`}>
              Review a peer
            </Button>
          ) : null}
        </div>
        {error && (
          <p role="alert" className="mt-2 text-sm text-destructive">
            {error}
          </p>
        )}
      </CardHeader>
    </Card>
  );
}

function ReceivedReviewCard({ review }: { review: LearningAssessmentsReceivedPeerReview }) {
  return (
    <div data-testid={`received-review-${review.reviewId}`} className="space-y-1 rounded-md border p-3">
      <div className="flex items-center justify-between gap-2">
        <span className="text-sm font-medium">Anonymous peer</span>
        {review.score != null && (
          <Badge variant="secondary">{scoreUnitsToPoints(review.score)}</Badge>
        )}
      </div>
      {review.feedback && <p className="text-sm text-muted-foreground">{review.feedback}</p>}
      {review.rubricScoresPayload && <RubricScoresSummary payload={review.rubricScoresPayload} />}
      {review.submittedAt && <p className="text-xs text-muted-foreground">{dateFormatter.format(new Date(review.submittedAt))}</p>}
    </div>
  );
}

interface PeerReviewsPageProps {
  reviewTasks: LearningTask[];
  received: ReceivedFeedbackGroup[];
}

export function PeerReviewsPage({ reviewTasks, received }: PeerReviewsPageProps): React.JSX.Element {
  return (
    <div className="space-y-6 p-4">
      <header>
        <h1 className="text-2xl font-bold tracking-tight">Peer reviews</h1>
        <p className="text-sm text-muted-foreground">Review your peers' work anonymously and see the feedback you received.</p>
      </header>

      <Tabs defaultValue="review">
        <TabsList>
          <TabsTrigger value="review">To review</TabsTrigger>
          <TabsTrigger value="received">Feedback received</TabsTrigger>
        </TabsList>

        <TabsContent value="review" className="mt-4 grid gap-3 md:grid-cols-2">
          {reviewTasks.length === 0 ? (
            <p className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground md:col-span-2">
              No peer reviews to complete right now.
            </p>
          ) : (
            reviewTasks.map((task) => <ReviewTaskCard key={`${task.courseId}:${task.assessmentId}`} task={task} />)
          )}
        </TabsContent>

        <TabsContent value="received" className="mt-4 space-y-4">
          {received.length === 0 ? (
            <p className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">No peer feedback received yet.</p>
          ) : (
            received.map((group) => (
              <Card key={`${group.courseTitle}:${group.assessmentId}`}>
                <CardHeader>
                  <CardTitle className="text-base">{group.assessmentTitle}</CardTitle>
                  <p className="text-sm text-muted-foreground">{group.courseTitle}</p>
                  <div className="mt-2 space-y-2">
                    {group.reviews.map((review) => (
                      <ReceivedReviewCard key={review.reviewId} review={review} />
                    ))}
                  </div>
                </CardHeader>
              </Card>
            ))
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
}
