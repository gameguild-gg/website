"use client";

import { useRouter } from "@/i18n/navigation";
import { filesToCodePayload } from "@/lib/coding-assignment/code-payload";
import type { CodingAssignmentContent } from "@/lib/coding-assignment/types";
import type { LearnerMutationResult } from "@/lib/learner/activity-actions";
import { startIndividualRuntimeSubmission, submitRuntimeSubmission } from "@/lib/learning/grading-runtime-actions";
import { readCodeRuntimeDefinition } from "@/lib/coding-assignment/runtime";
import type { AssessmentSubmissionRuntimeViewV1 } from "@game-guild/grading";
import { buildAssessmentExecutionPlan } from "@game-guild/emception-ui/assessment/plan";
import type {
  AssessmentRunResult,
  AssessmentSession,
  CodingAssessmentEditorProps,
} from "@game-guild/emception-ui/assessment/editor";
import { EMCEPTION_MANIFEST_URL } from "@/lib/emception/manifest-url";
import {
  createAssessmentWorkspaceConfig,
  type CodingLanguage,
} from "@game-guild/emception-ui/assessment/presets";
import { workspaceStorageKey } from "@game-guild/emception-ui/assessment/storage";
import { Button } from "@game-guild/ui/components/button";
import { scoreUnitsToPoints } from "@/lib/learning/academic-values";
import type { TestReport, WorkspaceConfig } from "emception";
import Script from "next/script";
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ComponentType,
  type FormEvent,
} from "react";
import { PublicTestEstimateBanner } from "./public-test-estimate-banner";
import { publicSeedFiles, type SeedFile } from "./resolve-seed";

type CodingEditorLoader = () => Promise<
  ComponentType<CodingAssessmentEditorProps>
>;

async function loadCodingEditor(): Promise<
  ComponentType<CodingAssessmentEditorProps>
> {
  const { CodingAssessmentEditor } =
    await import("@game-guild/emception-ui/assessment/editor");
  return CodingAssessmentEditor;
}

function IdeSkeleton() {
  return (
    <div
      data-testid="ide-skeleton"
      className="flex h-96 items-center justify-center rounded-md border border-white/10 bg-white/[0.03] text-sm text-muted-foreground"
    >
      Loading IDE…
    </div>
  );
}

export interface CodingActivityClientProps {
  assessmentId: string;
  enrollmentId: string;
  courseId: string;
  slug: string;
  /** v1 CodingAssignmentContent (Public-only — server strips Private). */
  assignment: CodingAssignmentContent;
  manifestUrl?: string;
  /** Namespaces the draft-storage token per user (page supplies; optional so
   *  the page can wire it independently — a bare assessmentId keeps the
   *  pre-namespacing keys, still reachable via the legacy fallback read). */
  userId?: string;
  /** Prior submission files for overlay restore; null = no submission yet. */
  submissionFiles?: SeedFile[] | null;
  /** Alternate lazy loader used by embedded hosts and deterministic tests. */
  loadEditor?: CodingEditorLoader;
}

export function CodingActivityClient(props: CodingActivityClientProps) {
  return <CodingActivitySession key={`${props.assessmentId}:${props.enrollmentId}`} {...props} />;
}

function CodingActivitySession({
  assessmentId,
  enrollmentId,
  slug,
  assignment: initialAssignment,
  manifestUrl,
  userId,
  submissionFiles,
  loadEditor = loadCodingEditor,
}: CodingActivityClientProps) {
  const sessionRef = useRef<AssessmentSession | null>(null);
  const router = useRouter();
  const [submitting, setSubmitting] = useState(false);
  const [report, setReport] = useState<TestReport | null>(null);
  const [result, setResult] = useState<LearnerMutationResult | null>(null);
  const [sessionReady, setSessionReady] = useState(false);
  const [assignment, setAssignment] = useState(initialAssignment);
  const [runtime, setRuntime] = useState<AssessmentSubmissionRuntimeViewV1 | null>(null);
  const [runtimeError, setRuntimeError] = useState<string | null>(null);
  const [startKey] = useState(() => crypto.randomUUID());
  const submitKey = useRef<string | null>(null);
  const [Editor, setEditor] =
    useState<ComponentType<CodingAssessmentEditorProps> | null>(null);
  const [editorLoadError, setEditorLoadError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    void startIndividualRuntimeSubmission(assessmentId, enrollmentId, startKey).then((result) => {
      if (!active) return;
      if (!result.success) { setRuntimeError(result.error); return; }
      try {
        setAssignment(readCodeRuntimeDefinition(result.data));
        setRuntime(result.data);
      } catch (error) {
        setRuntimeError(error instanceof Error ? error.message : "The Code attempt is unavailable.");
      }
    }).catch((error: unknown) => {
      if (active) setRuntimeError(error instanceof Error ? error.message : "The Code attempt is unavailable.");
    });
    return () => { active = false; };
  }, [assessmentId, enrollmentId, startKey]);

  // The neutral IDE owns browser-only APIs (Monaco, Worker and WASM). Import it
  // after hydration so the server never evaluates its module graph. This avoids
  // Next's app-page loadable alias while keeping the IDE outside SSR.
  useEffect(() => {
    let active = true;

    void loadEditor()
      .then((CodingAssessmentEditor) => {
        if (active) setEditor(() => CodingAssessmentEditor);
      })
      .catch((error: unknown) => {
        if (!active) return;
        setEditorLoadError(
          error instanceof Error
            ? error.message
            : "Unable to load the coding editor.",
        );
      });

    return () => {
      active = false;
    };
  }, [loadEditor]);

  const receiveSession = useCallback((session: AssessmentSession) => {
    sessionRef.current = session;
    setSessionReady(true);
  }, []);
  const receiveRunResult = useCallback((runResult: AssessmentRunResult) => {
    setReport(runResult.report);
  }, []);

  const seedFiles = useMemo(() => publicSeedFiles(assignment), [assignment]);
  const publicPlan = useMemo(
    () => buildAssessmentExecutionPlan(assignment, "public").plan,
    [assignment],
  );
  const maxScore = assignment.Grading.MaxScore;
  // ponytail: PassingScore moved from GradingConfig to Program (T1/T3). Learner context
  // doesn't surface Program.PassingScore yet — default to 60 (the C# default + historical
  // constant) until T13 wires the course-level field through getCourseLearnerContext.
  const passingScore = 60;

  const language =
    (assignment.Environment.Language as CodingLanguage | undefined) ?? "cpp";
  const restoredFiles = useMemo(() => {
    const response = runtime?.execution.submittedResponse;
    if (!response) return submissionFiles ?? [];
    const payload = response.payload as { files: Record<string, { content: string; encoding: 'text' }> };
    return Object.entries(payload.files).map(([path, file]) => ({ path, ...file }));
  }, [runtime, submissionFiles]);
  const alreadySubmitted = runtime?.execution.submittedResponse != null;
  // The host template supplies language-specific compiler/runtime settings.
  // Its files are the public seed overlaid by a previous server submission;
  // the neutral IDE restores a newer local draft from workspaceStorageKey.
  const workspaceConfig = useMemo<WorkspaceConfig>(() => {
    const files = new Map(
      seedFiles.map(({ path, content, encoding }) => [
        path,
        { encoding, content },
      ]),
    );
    for (const file of restoredFiles) {
      files.set(file.path, { encoding: file.encoding, content: file.content });
    }
    return createAssessmentWorkspaceConfig(language, Object.fromEntries(files));
  }, [language, seedFiles, restoredFiles]);

  if (result?.success) {
    return (
      <div
        role="status"
        className="rounded-md border border-emerald-500/30 bg-emerald-500/10 p-5 text-emerald-100"
      >
        Submission received. Grades and instructor feedback will appear here
        when available.
      </div>
    );
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (submitting || !runtime || alreadySubmitted) return;
    setSubmitting(true);
    try {
      // Submission serializes the current editor state without recompiling it.
      // Public tests have their own explicit action; repeating a full browser
      // toolchain run here can keep the form in "Submitting" for over a minute.
      // The assessment session returns only editable public changes plus
      // permitted student-created text files.
      const modified = (await sessionRef.current?.getSubmissionDelta()) ?? [];
      submitKey.current ??= crypto.randomUUID();
      const outcome = await submitRuntimeSubmission(runtime.submissionId, {
        schemaVersion: 1,
        contentType: "coding-assignment",
        payloadSchema: "code-files/v1",
        payload: { files: JSON.parse(filesToCodePayload([...modified])) as Record<string, { content: string; encoding: 'text' }> },
      }, submitKey.current);
      setResult(outcome.success ? { success: true } : { success: false, error: outcome.error });
      if (outcome.success) {
        router.push(`/learn/courses/${slug}/activities`);
      }
    } catch (error) {
      setResult({
        success: false,
        error:
          error instanceof Error
            ? error.message
            : "Unable to submit the coding activity.",
      });
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <>
      <Script src="/coi-serviceworker.js" strategy="afterInteractive" />
      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="h-[70vh] min-h-[500px]">
          {Editor && runtime ? (
            <Editor
              mode="learner"
              definition={assignment}
              workspaceConfig={workspaceConfig}
              workspaceStorageKey={workspaceStorageKey(
                userId ? `${userId}:${assessmentId}` : assessmentId,
                workspaceConfig.id,
              )}
              manifestUrl={manifestUrl ?? EMCEPTION_MANIFEST_URL}
              maxScore={maxScore}
              passingScore={passingScore}
              onSessionReady={receiveSession}
              onRunResult={receiveRunResult}
            />
          ) : runtimeError || editorLoadError ? (
            <p role="alert" className="text-sm text-destructive">
              {runtimeError || editorLoadError}
            </p>
          ) : (
            <IdeSkeleton />
          )}
        </div>
        {report ? (
          <PublicTestEstimateBanner
            report={report}
            plan={publicPlan}
            maxScore={maxScore}
            passingScore={passingScore}
          />
        ) : null}
        {result?.error ? (
          <p role="alert" className="text-sm text-destructive">
            {result.error}
          </p>
        ) : null}
        {alreadySubmitted ? (
          <div role="status" className="rounded-md border p-4 text-sm">
            <p>Submission received.</p>
            {runtime?.execution.learnerVisibleResult ? (
              <>
                <p>
                  Score: {scoreUnitsToPoints(runtime.execution.learnerVisibleResult.score ?? 0)} /{' '}
                  {scoreUnitsToPoints(runtime.execution.learnerVisibleResult.maxScore)}
                </p>
                {runtime.execution.learnerVisibleResult.feedback ? (
                  <p>{runtime.execution.learnerVisibleResult.feedback}</p>
                ) : null}
              </>
            ) : (
              <p>Grades and instructor feedback will appear here when available.</p>
            )}
          </div>
        ) : null}
        <div className="flex justify-end">
          <Button type="submit" disabled={submitting || !sessionReady || !runtime || alreadySubmitted}>
            {submitting ? "Submitting…" : "Submit"}
          </Button>
        </div>
      </form>
    </>
  );
}
