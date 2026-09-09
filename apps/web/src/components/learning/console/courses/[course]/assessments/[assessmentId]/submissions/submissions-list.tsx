'use client';

import { Loader2 } from 'lucide-react';
import { Link } from '@/i18n/navigation';
import { Badge } from '@game-guild/ui/components/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@game-guild/ui/components/table';
import type { LearningAssessmentsAssessmentSubmission } from '@game-guild/client';
import { useLearningBase } from '@/lib/learning/use-learning-base';
import { scoreUnitsToPoints } from '@/lib/learning/academic-values';

// ponytail: simple list render; add server-side pagination when submission count exceeds 200

const dateFormatter = new Intl.DateTimeFormat('en-US', {
  year: 'numeric',
  month: 'short',
  day: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
});

export type SubmissionStatusVariant =
  | 'default'
  | 'secondary'
  | 'destructive'
  | 'outline';

export function statusBadgeVariant(
  status: LearningAssessmentsAssessmentSubmission['status'],
): SubmissionStatusVariant {
  switch (status) {
    case 'Submitted':
      return 'default';
    case 'Graded':
      return 'secondary';
    case 'Late':
      return 'destructive';
    case 'InProgress':
    case 'Returned':
    case undefined:
      return 'outline';
  }
}

export interface SubmissionsListProps {
  courseSlug: string;
  assessmentId: string;
  assessmentSlug: string;
  maxScore: number;
  submissions: LearningAssessmentsAssessmentSubmission[];
  error?: string;
  isLoading?: boolean;
}

export function SubmissionsList({
  courseSlug,
  assessmentId,
  assessmentSlug,
  maxScore,
  submissions,
  error,
  isLoading = false,
}: SubmissionsListProps): React.JSX.Element {
  const learningBase = useLearningBase();
  if (isLoading) {
    return (
      <div data-testid="submissions-loading" className="flex items-center gap-2 text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" />
        <span>Loading submissions…</span>
      </div>
    );
  }

  if (error) {
    return (
      <div data-testid="submissions-error" className="text-destructive">
        {error}
      </div>
    );
  }

  if (submissions.length === 0) {
    return (
      <div data-testid="submissions-empty" className="text-muted-foreground">
        No submissions yet for this assessment.
      </div>
    );
  }

  return (
    <Table data-testid="submissions-table">
      <TableHeader>
        <TableRow>
          <TableHead>Student</TableHead>
          <TableHead>Attempt</TableHead>
          <TableHead>Started</TableHead>
          <TableHead>Submitted</TableHead>
          <TableHead>Status</TableHead>
          <TableHead>Score</TableHead>
          <TableHead>Actions</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {submissions.map((submission) => (
          <SubmissionRow
            key={submission.id ?? `${submission.userId}-${submission.attemptNumber}`}
            submission={submission}
            courseSlug={courseSlug}
            assessmentId={assessmentId}
            assessmentSlug={assessmentSlug}
            maxScore={maxScore}
          />
        ))}
      </TableBody>
    </Table>
  );
}

interface SubmissionRowProps {
  submission: LearningAssessmentsAssessmentSubmission;
  courseSlug: string;
  assessmentId: string;
  assessmentSlug: string;
  maxScore: number;
}

function SubmissionRow({
  submission,
  courseSlug,
  assessmentId,
  assessmentSlug,
  maxScore,
}: SubmissionRowProps): React.JSX.Element {
  const learningBase = useLearningBase();
  const studentLabel =
    submission.userId && submission.userId.length > 8
      ? `${submission.userId.slice(0, 8)}…`
      : (submission.userId ?? '—');

  const submittedAt = submission.submittedAt
    ? dateFormatter.format(new Date(submission.submittedAt))
    : '—';

  const startedAt = submission.startedAt
    ? dateFormatter.format(new Date(submission.startedAt))
    : '—';

  const scoreLabel =
    submission.score != null
      ? `${scoreUnitsToPoints(submission.score)}/${maxScore}`
      : '—';

  const gradeHref = `${learningBase}/courses/${courseSlug}/assessments/${assessmentSlug}/submissions/${submission.id}/grade`;

  return (
    <TableRow data-testid={`submission-row-${submission.id}`}>
      <TableCell>{studentLabel}</TableCell>
      <TableCell>{submission.attemptNumber ?? '—'}</TableCell>
      <TableCell>{startedAt}</TableCell>
      <TableCell>{submittedAt}</TableCell>
      <TableCell>
        <Badge variant={statusBadgeVariant(submission.status)} data-testid={`submission-status-${submission.id}`}>
          {submission.status ?? 'InProgress'}
        </Badge>
      </TableCell>
      <TableCell>{scoreLabel}</TableCell>
      <TableCell>
        <Link
          href={gradeHref}
          className="text-primary underline-offset-4 hover:underline"
          data-testid={`submission-grade-link-${submission.id}`}
        >
          Grade
        </Link>
      </TableCell>
    </TableRow>
  );
}
