'use client';

import { submitAssessment, submitContentActivity } from '@/lib/learner-activity-actions';
import { getPreferredSubmissionModality } from '@/lib/learner-activity-contracts';
import type {
  LearningAssessmentsAssessment,
  LearningAssessmentsLearnerAssessmentSubmission,
  LearningCoursesProgramContentType,
  ProjectsProjectApiOutput,
} from '@game-guild/client';
import { Alert, AlertDescription, AlertTitle } from '@game-guild/ui/components/alert';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Input } from '@game-guild/ui/components/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@game-guild/ui/components/select';
import { Textarea } from '@game-guild/ui/components/textarea';
import { CheckCircle2, Clock3, FolderKanban, Send, Upload } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { FormEvent, useState } from 'react';

export type LearnerActivityDescriptor =
  | { kind: 'assessment'; assessment: LearningAssessmentsAssessment; submission?: LearningAssessmentsLearnerAssessmentSubmission; projects?: ProjectsProjectApiOutput[] }
  | {
      kind: 'content';
      contentId: string;
      contentType: Extract<LearningCoursesProgramContentType, 'Discussion' | 'Reflection' | 'Survey'>;
      title: string;
      description?: string;
      completed?: boolean;
    };

function responseLabel(activity: LearnerActivityDescriptor, modality: string) {
  if (activity.kind === 'content') {
    if (activity.contentType === 'Reflection') return 'Your reflection';
    if (activity.contentType === 'Survey') return 'Your response';
    return 'Your contribution';
  }
  if (modality === 'Code') return 'Your code';
  if (modality === 'Url' || modality === 'Media') return 'Submission URL';
  return 'Your submission';
}

export function LearnerActivityForm({
  courseId,
  courseSlug,
  enrollmentId,
  activity,
}: {
  courseId: string;
  courseSlug: string;
  enrollmentId: string;
  activity: LearnerActivityDescriptor;
}) {
  const router = useRouter();
  const [response, setResponse] = useState('');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);
  const modality = activity.kind === 'assessment' ? getPreferredSubmissionModality(activity.assessment.type, activity.assessment.submissionModalities) : 'Text';
  const finalSubmission =
    activity.kind === 'assessment' && activity.submission && activity.submission.status !== 'InProgress' ? activity.submission : undefined;

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPending(true);
    setError(null);
    const data = new FormData(event.currentTarget);
    const result = activity.kind === 'assessment' ? await submitAssessment(data) : await submitContentActivity(data);
    setPending(false);
    if (!result.success) {
      setError(result.error || 'The response could not be submitted.');
      return;
    }
    setSuccess(true);
    router.refresh();
  }

  if (success) {
    return (
      <Alert className="border-emerald-500/30 bg-emerald-500/10 text-emerald-100">
        <CheckCircle2 className="size-4" />
        <AlertTitle>Submission received</AlertTitle>
        <AlertDescription>Your response is stored in the course record. Grades and instructor feedback will appear here when available.</AlertDescription>
      </Alert>
    );
  }

  if (finalSubmission) {
    return (
      <div className="space-y-4 rounded-md border border-white/10 bg-white/[0.03] p-5">
        <div className="flex flex-wrap items-center gap-2">
          <Badge className="bg-emerald-500/15 text-emerald-300">{finalSubmission.status}</Badge>
          {finalSubmission.score != null ? (
            <strong className="text-lg text-white">{finalSubmission.score} points</strong>
          ) : (
            <span className="text-sm text-slate-400">Awaiting grading</span>
          )}
        </div>
        {finalSubmission.feedback ? (
          <div className="border-l-2 border-violet-400 pl-4">
            <p className="text-xs font-medium uppercase tracking-wide text-violet-300">Instructor feedback</p>
            <p className="mt-2 text-sm leading-6 text-slate-200">{finalSubmission.feedback}</p>
          </div>
        ) : null}
      </div>
    );
  }

  if (activity.kind === 'content' && activity.completed) {
    return (
      <Alert className="border-emerald-500/30 bg-emerald-500/10 text-emerald-100">
        <CheckCircle2 className="size-4" />
        <AlertTitle>Activity completed</AlertTitle>
        <AlertDescription>Your course response has already been submitted.</AlertDescription>
      </Alert>
    );
  }

  if (activity.kind === 'assessment' && activity.assessment.type === 'Quiz') {
    return (
      <Alert>
        <AlertTitle>Quiz attempt unavailable</AlertTitle>
        <AlertDescription>This quiz cannot be answered through the generic assessment form.</AlertDescription>
      </Alert>
    );
  }

  const label = responseLabel(activity, modality);
  const projects = activity.kind === 'assessment' ? (activity.projects ?? []) : [];
  const contentKind = activity.kind === 'content' ? activity.contentType.toLowerCase() : null;
  const projectSubmissionUnavailable = modality === 'Project' && projects.length === 0;
  const projectsUrl = `${process.env.NEXT_PUBLIC_WEB_URL || 'http://localhost:3000'}/projects`;

  return (
    <form onSubmit={handleSubmit} className="space-y-5">
      {activity.kind === 'assessment' ? (
        <>
          <input type="hidden" name="assessmentId" value={activity.assessment.id} />
          <input type="hidden" name="modality" value={modality} />
        </>
      ) : (
        <>
          <input type="hidden" name="courseId" value={courseId} />
          <input type="hidden" name="contentId" value={activity.contentId} />
          <input type="hidden" name="kind" value={contentKind || ''} />
        </>
      )}
      <input type="hidden" name="enrollmentId" value={enrollmentId} />
      <input type="hidden" name="courseSlug" value={courseSlug} />

      {modality === 'File' ? (
        <div className="space-y-2">
          <label htmlFor="activity-file" className="text-sm font-medium text-white">
            Choose file
          </label>
          <Input id="activity-file" name="file" type="file" required className="border-white/10 bg-white/[0.03]" />
          <p className="text-xs text-slate-500">The file is stored privately and linked to this assessment attempt.</p>
        </div>
      ) : modality === 'Project' && projects.length > 0 ? (
        <div className="space-y-2">
          <label htmlFor="project-response" className="text-sm font-medium text-white">
            Project
          </label>
          <input type="hidden" name="response" value={response} />
          <Select value={response} onValueChange={setResponse} required>
            <SelectTrigger id="project-response" className="w-full">
              <SelectValue placeholder="Choose one of your projects" />
            </SelectTrigger>
            <SelectContent>
              {projects
                .filter((project) => project.id)
                .map((project) => (
                  <SelectItem key={project.id} value={project.id!}>
                    {project.title || 'Untitled project'}
                  </SelectItem>
                ))}
            </SelectContent>
          </Select>
          <p className="text-xs text-slate-500">Only existing Game Guild projects can be attached.</p>
        </div>
      ) : projectSubmissionUnavailable ? (
        <div className="flex flex-col gap-4 border-y border-white/10 py-5 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex gap-3">
            <FolderKanban className="mt-0.5 size-5 shrink-0 text-violet-300" />
            <div>
              <p className="font-medium text-white">Create a project before submitting</p>
              <p className="mt-1 text-sm text-slate-400">Project assessments only accept projects owned by your Game Guild account.</p>
            </div>
          </div>
          <Button asChild type="button" variant="outline">
            <Link href={projectsUrl}>Open projects</Link>
          </Button>
        </div>
      ) : modality === 'Url' || modality === 'Media' ? (
        <div className="space-y-2">
          <label htmlFor="activity-response" className="text-sm font-medium text-white">
            {label}
          </label>
          <Input
            id="activity-response"
            name="response"
            type="url"
            required
            value={response}
            onChange={(event) => setResponse(event.target.value)}
            placeholder="https://"
          />
        </div>
      ) : (
        <div className="space-y-2">
          <label htmlFor="activity-response" className="text-sm font-medium text-white">
            {label}
          </label>
          <Textarea
            id="activity-response"
            name="response"
            required
            rows={modality === 'Code' ? 14 : 8}
            value={response}
            onChange={(event) => setResponse(event.target.value)}
            className={`border-white/10 bg-white/[0.03] ${modality === 'Code' ? 'font-mono' : ''}`}
            placeholder={contentKind === 'discussion' ? 'Add a constructive contribution to the course conversation.' : 'Write your response here.'}
          />
        </div>
      )}

      {error ? (
        <Alert variant="destructive">
          <AlertTitle>Submission failed</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      ) : null}
      <div className="flex flex-wrap items-center justify-between gap-3 border-t border-white/10 pt-4">
        <p className="inline-flex items-center gap-2 text-xs text-slate-500">
          <Clock3 className="size-3.5" />
          Submitting creates a timestamped course record.
        </p>
        <Button type="submit" disabled={pending || projectSubmissionUnavailable || (modality === 'Project' && !response)}>
          {modality === 'File' ? <Upload className="size-4" /> : <Send className="size-4" />}
          {pending ? 'Submitting...' : activity.kind === 'assessment' ? 'Submit assessment' : `Submit ${contentKind}`}
        </Button>
      </div>
    </form>
  );
}
