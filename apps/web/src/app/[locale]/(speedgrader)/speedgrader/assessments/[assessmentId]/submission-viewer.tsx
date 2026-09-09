'use client';

import { useEffect, useState } from 'react';
import type { LearningAssessmentsAssessmentSubmission } from '@game-guild/client';
import { TextViewer } from './text-viewer';
import { UrlViewer } from './url-viewer';
import { FileViewer } from './file-viewer';
import { MediaViewer } from './media-viewer';
import { CodeGraderPanel, type ComputedScore } from './code-grader-panel';
import { codePayloadToFiles } from '@/lib/coding-assignment/code-payload';
import type { CodingAssignmentContent as WebCodingAssignmentContent } from '@/lib/coding-assignment/client';
import { fetchSubmissionAction } from './speedgrader-actions';
import { parseSubmittedModalities } from './submitted-modalities';

export { parseSubmittedModalities };

export interface SubmissionViewerProps {
  /** Canonical submission id (queue item.submissionId — fetch happens here). */
  submissionId: string;
  /** Full coding assignment — required for the IDE code viewer. */
  codingAssignment?: WebCodingAssignmentContent | null;
  manifestUrl?: string;
  onComputedScore?: (result: ComputedScore) => void;
}

/**
 * SpeedGrader left panel: fetches the submission for the current queue item
 * and renders EVERY present payload stacked (submissions may be multi-modality).
 */
export function SubmissionViewer({
  submissionId,
  codingAssignment,
  manifestUrl = '/emception/manifest.json',
  onComputedScore,
}: SubmissionViewerProps): React.JSX.Element {
  const [submission, setSubmission] = useState<LearningAssessmentsAssessmentSubmission | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setSubmission(null);
    setError(null);
    fetchSubmissionAction(submissionId).then((result) => {
      if (cancelled) return;
      if (result.ok) {
        setSubmission(result.submission);
      } else {
        setError(result.error);
      }
    });
    return () => {
      cancelled = true;
    };
  }, [submissionId]);

  if (error) {
    return (
      <div data-testid="viewer-error" role="alert" className="p-4 text-sm text-destructive">
        {error}
      </div>
    );
  }
  if (!submission) {
    return (
      <div data-testid="viewer-loading" className="p-4 text-sm text-muted-foreground">
        Loading submission…
      </div>
    );
  }

  const modalities = parseSubmittedModalities(submission.submittedModalities);
  const panes: React.ReactNode[] = [];

  if (modalities.has('Text') && submission.textPayload) {
    panes.push(<TextViewer key="text" text={submission.textPayload} />);
  }
  if (modalities.has('Url') && submission.urlPayload) {
    panes.push(<UrlViewer key="url" url={submission.urlPayload} />);
  }
  if (modalities.has('Code') && submission.codePayload) {
    panes.push(
      codingAssignment ? (
        <CodeGraderPanel
          key="code"
          assignment={codingAssignment}
          submittedFiles={safeParseCodeFiles(submission.codePayload)}
          maxScore={codingAssignment.Grading.MaxScore}
          manifestUrl={manifestUrl}
          submissionId={submissionId}
          onComputedScore={onComputedScore}
        />
      ) : (
        <CodeFallback key="code" payload={submission.codePayload} />
      ),
    );
  }
  if (modalities.has('File') && submission.filePayload) {
    panes.push(<FileViewer key="file" payload={submission.filePayload} />);
  }
  if (modalities.has('Media') && submission.mediaPayload) {
    panes.push(<MediaViewer key="media" url={submission.mediaPayload} />);
  }

  if (panes.length === 0) {
    return (
      <div data-testid="viewer-empty" className="p-4 text-sm text-muted-foreground">
        This submission has no viewable payload.
      </div>
    );
  }

  return (
    <div data-testid="submission-viewer" className="h-full space-y-4 overflow-auto p-4">
      {panes}
    </div>
  );
}

function safeParseCodeFiles(payload: string) {
  try {
    return codePayloadToFiles(payload);
  } catch (err) {
    // ponytail: surface the parse failure so an empty IDE is diagnosable
    // (payload shape drift / truncated JSON) instead of a silent empty array.
    console.error('[speedgrader] Failed to parse codePayload:', err);
    return [];
  }
}

/** Code submission without a loadable coding assignment: raw file listing. */
function CodeFallback({ payload }: { payload: string }): React.JSX.Element {
  const files = safeParseCodeFiles(payload);
  return (
    <div data-testid="code-fallback" className="space-y-3 rounded-md border bg-card p-4">
      {files.map((file) => (
        <div key={file.path} className="space-y-1">
          <p className="text-sm font-medium">{file.path}</p>
          <pre className="overflow-auto rounded bg-muted p-2 text-xs text-muted-foreground">{file.content}</pre>
        </div>
      ))}
      {files.length === 0 && <pre className="overflow-auto rounded bg-muted p-2 text-xs text-muted-foreground">{payload}</pre>}
    </div>
  );
}
