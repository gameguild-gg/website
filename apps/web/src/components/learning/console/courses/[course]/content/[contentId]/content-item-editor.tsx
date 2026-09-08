"use client";

import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  useTransition,
} from "react";
import { useRouter } from "next/navigation";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@game-guild/ui/components/card";
import { Badge } from "@game-guild/ui/components/badge";
import { Button } from "@game-guild/ui/components/button";
import { Input } from "@game-guild/ui/components/input";
import { Label } from "@game-guild/ui/components/label";
import { Textarea } from "@game-guild/ui/components/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@game-guild/ui/components/select";
import { Switch } from "@game-guild/ui/components/switch";
import { Separator } from "@game-guild/ui/components/separator";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@game-guild/ui/components/alert-dialog";
import { buttonVariants } from "@game-guild/ui/components/button";
import { ArrowLeft, Clock, Eye, Loader2, Pencil, Save } from "lucide-react";
import type { SerializedEditorState } from "lexical";
import type { LearningCoursesLessonContentFormat, LearningCoursesVisibility } from "@game-guild/client";
import {
  readContentGradingDefinition,
  type ContentGradingDefinition,
} from "@game-guild/grading";
import type { ContentItemDetail } from "@/lib/learning/types";
import type { CodingDefinition } from "@/lib/learning/queries/assessments";
import {
  createAssessment,
  deleteAssessment,
  restoreAssessment,
  updateAssessment,
  updateContent,
} from "@/lib/learning/actions";
import { CONTENT_ITEM_VISIBILITIES, formatEnumLabel } from "@/lib/learning/enums";
import { getLessonFormatLabel } from "@/lib/learning/lesson-formats";
import { estimateReadingMinutes } from "@/lib/learning/reading-time";
import { normalizeSlug, slugify } from "@/lib/slugify";
import { LearnerLessonRenderer } from "@/components/learning/learner-lesson-renderer";
import { LessonContentEditor } from "./lesson-content-editor";
import { LessonCodeEditor } from "./lesson-code-editor";
import { LessonVideoEditor } from "./lesson-video-editor";
import { QuizContentEditor } from "./quiz-content-editor";
import { useLearningBase } from '@/lib/learning/use-learning-base';

function formatContentTypeLabel(type: ContentItemDetail["type"]) {
  if (type === "Questionnaire") return "Quiz";
  return type;
}

const AUTOSAVE_DELAY_MS = 3000;

// ── Component ────────────────────────────────────────────────────────────────

interface ContentItemEditorProps {
  courseId: string;
  item: ContentItemDetail;
  courseTitle: string;
  linkedAssessmentId?: string;
  linkedAssessmentSlug?: string;
  // ponytail: raw [Flags] string from linked Assessment — editor does substring check
  // for "AutoGraded" rather than re-fetching the assessment.
  linkedAssessmentGradingMethods?: string;
  initialCodingDefinition?: CodingDefinition | null;
}

export function ContentItemEditor({
  courseId,
  item,
  courseTitle,
  linkedAssessmentId,
  linkedAssessmentSlug,
  linkedAssessmentGradingMethods,
  initialCodingDefinition,
}: ContentItemEditorProps) {
  const learningBase = useLearningBase();
  const router = useRouter();
  const [isPending, startTransition] = useTransition();

  const [title, setTitle] = useState(item.title);
  const [slug, setSlug] = useState(item.slug);
  // Slug starts in auto mode regardless of the stored value (it may be a
  // legacy backfill): title edits regenerate it until the slug is edited
  // directly in this session, which detaches it.
  const [autoSlug, setAutoSlug] = useState(true);
  const [description, setDescription] = useState(item.description ?? "");
  const [visibility, setVisibility] =
    useState<LearningCoursesVisibility>(item.visibility);
  const [isRequired, setIsRequired] = useState<boolean>(
    (item.settings?.isRequired as boolean) ?? true,
  );
  const [estimatedMinutes, setEstimatedMinutes] = useState<string>(
    item.estimatedMinutesSource === "Manual" && item.duration != null
      ? String(item.duration)
      : "",
  );
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [previewMode, setPreviewMode] = useState(false);
  const [codingError, setCodingError] = useState<string | null>(null);
  const [lastSavedAt, setLastSavedAt] = useState<Date | null>(
    item.updatedAt ? new Date(item.updatedAt) : null,
  );

  // ── Graded toggle (Task 7) ──
  // checked mirrors linkedAssessmentId (server-side non-deleted link).
  // recentlyDeletedAssessmentId remembers what the OFF transition just soft-deleted so
  // a re-toggle ON restores instead of creating a duplicate — the GET assessments
  // endpoint filters deleted rows server-side, so this is the only client-side signal.
  const [gradedChecked, setGradedChecked] =
    useState<boolean>(!!linkedAssessmentId);
  const [recentlyDeletedAssessmentId, setRecentlyDeletedAssessmentId] =
    useState<string | null>(null);
  const [showGradedOffConfirm, setShowGradedOffConfirm] = useState(false);
  const [gradedError, setGradedError] = useState<string | null>(null);
  const [isGradedPending, startGradedTransition] = useTransition();

  const isLesson = item.type === "Lesson";
  const isQuiz = item.type === "Questionnaire";
  const isCode = item.type === "Code";
  // ponytail: substring check is enough — full parseGradingMethods is overkill for one flag.
  const isAutoGraded = (linkedAssessmentGradingMethods ?? "").includes(
    "AutoGraded",
  );
  // ponytail: isGradedType drives the Graded toggle (wider set); the Coding Assignment card
  // below gates on `isCode && gradedChecked && isAutoGraded` per Task 11 — only Code content
  // with AutoGraded-flagged linked assessment exposes the coding-tests bridge.
  const GRADED_CONTENT_TYPES: ReadonlySet<string> = new Set([
    "Assignment",
    "Project",
    "Code",
  ]);
  const isGradedType = GRADED_CONTENT_TYPES.has(item.type);

  // ── Lexical editor state (Lesson only) ──
  const initialLexicalState = useMemo(
    () => item.jsonBody as SerializedEditorState | null,
    [item.jsonBody],
  );
  const [editorState, setEditorState] =
    useState<SerializedEditorState | null>(initialLexicalState);
  const [codeBody, setCodeBody] = useState(item.content ?? "");
  const [videoUrl, setVideoUrl] = useState(item.content ?? "");
  const initialQuizContent = useMemo(
    () => (isQuiz ? (item.jsonBody ?? undefined) : undefined),
    [isQuiz, item.jsonBody],
  );
  const [quizRevision, setQuizRevision] = useState(0);
  const quizContentRef = useRef<Record<string, unknown> | undefined>(
    initialQuizContent,
  );
  const quizAssessmentIdRef = useRef<string | undefined>(linkedAssessmentId);

  // ── Selected lesson format ──
  const initialSelectedFormat = useMemo(() => {
    if (!isLesson) return "Markdown";
    if (item.lessonFormat) return item.lessonFormat;
    if (item.jsonBody) return "Lexical";
    return "Markdown";
  }, [isLesson, item.lessonFormat, item.jsonBody]);
  const [selectedFormat] =
    useState<LearningCoursesLessonContentFormat>(initialSelectedFormat);

  const autoHint = useMemo(() => {
    if (!isLesson) return null;
    switch (selectedFormat) {
      case "Lexical":
        return estimateReadingMinutes({
          jsonBody: (editorState ?? null) as Record<
            string,
            unknown
          > | null,
        });
      case "Markdown":
      case "RevealJs":
        return estimateReadingMinutes({ body: codeBody || null });
      case "Video":
        return null;
      default:
        return null;
    }
  }, [codeBody, editorState, isLesson, selectedFormat]);

  const handleEditorChange = useCallback((state: SerializedEditorState) => {
    setEditorState(state);
  }, []);

  const handleQuizContentChange = useCallback(
    (content: Record<string, unknown>) => {
      quizContentRef.current = content;
      setQuizRevision((r) => r + 1);
    },
    [],
  );

  const contentTypeLabel = formatContentTypeLabel(item.type);

  function handleTitleChange(value: string) {
    setTitle(value);
    if (autoSlug) {
      setSlug(slugify(value));
    }
  }

  function handleSlugChange(value: string) {
    setAutoSlug(false);
    setSlug(slugify(value));
  }

  function buildSavePayload() {
    // Lesson: Lexical -> jsonBody (object); text formats -> body (string).
    // Quiz is structured content and must persist in jsonBody.
    let bodyToSave: string | undefined;
    let jsonBodyToSave: Record<string, unknown> | undefined;
    if (isLesson) {
      switch (selectedFormat) {
        case "Lexical":
          jsonBodyToSave = (editorState ?? undefined) as
            Record<string, unknown> | undefined;
          break;
        case "Markdown":
        case "RevealJs":
          bodyToSave = codeBody || undefined;
          break;
        case "Video":
          bodyToSave = videoUrl || undefined;
          break;
      }
    } else if (isQuiz) {
      jsonBodyToSave = quizContentRef.current;
    }
    return {
      title: (title ?? "").trim(),
      // Backend keeps the stored slug when sent whitespace — derive locally
      // so a cleared field can't silently revert to the old slug. Re-slugify
      // to strip the trailing hyphen live typing can leave behind. Slugs can
      // be missing on legacy items, so coerce before slugify.
      slug:
        normalizeSlug(slug ?? "") ||
        normalizeSlug(title ?? ""),
      description: description.trim() || undefined,
      body: bodyToSave,
      jsonBody: jsonBodyToSave,
      visibility,
      isRequired,
      estimatedMinutes: estimatedMinutes ? Number(estimatedMinutes) : null,
      estimatedMinutesSource: estimatedMinutes ? ("Manual" as const) : ("Auto" as const),
    };
  }

  const snapshotRef = useRef<string | null>(null);
  const saveInFlightRef = useRef(false);
  const buildPayloadRef = useRef(buildSavePayload);

  async function performSave(isManual: boolean) {
    if (!isManual && saveInFlightRef.current) return;
    setError(null);
    if (isManual) setSaved(false);

    const payload = buildSavePayload();
    const payloadJson = JSON.stringify(payload);
    const quizGrading = isQuiz
      ? readContentGradingDefinition(payload.jsonBody)
      : null;

    saveInFlightRef.current = true;
    startTransition(async () => {
      try {
        const result = await updateContent({
          courseId,
          contentId: item.id,
          title: payload.title,
          slug: payload.slug,
          description: payload.description,
          body: payload.body,
          ...(isLesson
            ? { jsonBody: payload.jsonBody, lessonFormat: selectedFormat }
            : {}),
          ...(isQuiz ? { jsonBody: payload.jsonBody } : {}),
          visibility: payload.visibility,
          isRequired: payload.isRequired,
          estimatedMinutes: payload.estimatedMinutes,
          estimatedMinutesSource: payload.estimatedMinutesSource,
        });

        if (!result.success) {
          setError(result.error);
          return;
        }

        if (isQuiz) {
          const assessmentResult = await reconcileQuizAssessment(quizGrading);
          if (!assessmentResult.success) {
            setError(
              `Quiz content was saved, but its assessment could not be synchronized: ${assessmentResult.error}`,
            );
            return;
          }
        }

        snapshotRef.current = payloadJson;
        setLastSavedAt(new Date());
        if (isManual) setSaved(true);

        // The route param IS the slug — after a slug change the current URL is
        // stale, so replace it instead of refreshing in place. Autosave skips
        // router.refresh() to avoid re-rendering server data mid-typing.
        const savedSlug = payload.slug;
        if (savedSlug && savedSlug !== item.slug) {
          router.replace(
            `${learningBase}/courses/${encodeURIComponent(courseId)}/content/${savedSlug}` as Parameters<
              typeof router.push
            >[0],
          );
        } else if (isManual) {
          router.refresh();
        }
      } finally {
        saveInFlightRef.current = false;
      }
    });
  }

  const performSaveRef = useRef(performSave);

  // Keeps both latest-closure refs fresh after every render so the debounced
  // timer below always fires against current state.
  useEffect(() => {
    buildPayloadRef.current = buildSavePayload;
    performSaveRef.current = performSave;
  });

  // Debounced autosave: waits for edits to settle, then saves silently. The
  // JSON snapshot keeps mount-time and no-op re-renders from firing requests;
  // the in-flight guard keeps a slow save from racing a newer one.
  useEffect(() => {
    const payloadJson = JSON.stringify(buildPayloadRef.current());
    if (snapshotRef.current === null) {
      snapshotRef.current = payloadJson;
      return;
    }
    if (payloadJson === snapshotRef.current) return;
    if (!title.trim()) return;

    const timer = setTimeout(() => {
      const latestJson = JSON.stringify(buildPayloadRef.current());
      if (latestJson === snapshotRef.current) return;
      void performSaveRef.current(false);
    }, AUTOSAVE_DELAY_MS);
    return () => clearTimeout(timer);
  }, [
    title,
    slug,
    description,
    visibility,
    isRequired,
    estimatedMinutes,
    editorState,
    codeBody,
    videoUrl,
    quizRevision,
    isLesson,
    isQuiz,
    selectedFormat,
  ]);

  function handleSave() {
    if (!title.trim()) {
      setError("Title is required.");
      return;
    }
    performSave(true);
  }

  async function reconcileQuizAssessment(
    grading: ContentGradingDefinition | null,
  ): Promise<{ success: true } | { success: false; error: string }> {
    const assessmentId = quizAssessmentIdRef.current;

    if (!grading?.enabled) {
      if (!assessmentId) return { success: true };

      const result = await deleteAssessment(courseId, assessmentId);
      if (!result.success) return result;

      quizAssessmentIdRef.current = undefined;
      return { success: true };
    }

    const assessmentFields = {
      title: title.trim(),
      description: description.trim() || undefined,
      maxScore: Math.max(1, Math.round(grading.score.maxScore)),
      timeLimitMinutes: grading.attempts.timeLimitMinutes ?? null,
      maxAttempts: grading.attempts.maxAttempts ?? null,
      isRequired,
      presentationMode:
        grading.presentation.mode === "single-step"
          ? ("SingleStep" as const)
          : ("Continuous" as const),
      gradingMethods: "AutoGraded,InstructorGraded",
    };

    if (assessmentId) {
      const result = await updateAssessment({
        courseId,
        assessmentId,
        contentId: item.id,
        ...assessmentFields,
      });
      return result.success ? { success: true } : result;
    }

    const result = await createAssessment({
      courseId,
      type: "Quiz",
      contentId: item.id,
      submissionModalities: "StructuredAnswer",
      ...assessmentFields,
    });
    if (!result.success) return result;

    quizAssessmentIdRef.current = result.data.id;
    return { success: true };
  }

  function handleBack() {
    router.push(
      "/workspace/learning/courses/" +
        encodeURIComponent(courseId) +
        "/content",
    );
  }

  function codingDefinitionRoute(assessmentSlug: string) {
    return (
      "/workspace/learning/courses/" +
      encodeURIComponent(courseId) +
      "/assessments/" +
      assessmentSlug +
      "/coding-definition"
    );
  }

  async function handleConfigureCoding() {
    setCodingError(null);
    if (!linkedAssessmentSlug && !linkedAssessmentId) {
      setCodingError(
        "No assessment is linked to this content item yet. Add an assessment in the Assessments tab first.",
      );
      return;
    }
    router.push(codingDefinitionRoute(linkedAssessmentSlug ?? linkedAssessmentId!));
  }

  // ── Graded toggle handlers (Task 7) ──
  // ponytail: content-to-assessment type map mirrors addContent (actions.ts). Inlined here
  // rather than imported because the action's map keys LearningCoursesProgramContentType and
  // we only need the four graded branches — re-using the action's const would pull server-only
  // code into the client bundle.
  const CONTENT_TO_ASSESSMENT_TYPE: Record<
    string,
    "Assignment" | "Quiz" | "Project"
  > = {
    Assignment: "Assignment",
    Project: "Project",
    Code: "Assignment",
  };

  function handleGradedToggle(next: boolean) {
    if (next === gradedChecked || isGradedPending) return;
    if (!next) {
      setShowGradedOffConfirm(true);
      return;
    }
    const restoreTargetId = recentlyDeletedAssessmentId ?? linkedAssessmentId;
    startGradedTransition(async () => {
      setGradedChecked(true);
      setGradedError(null);
      if (restoreTargetId) {
        const result = await restoreAssessment(courseId, restoreTargetId);
        if (!result.success) {
          setGradedChecked(false);
          setGradedError(result.error);
          return;
        }
        setRecentlyDeletedAssessmentId(null);
        router.refresh();
        return;
      }
      const assessmentType =
        CONTENT_TO_ASSESSMENT_TYPE[item.type] ?? "Assignment";
      const result = await createAssessment({
        courseId,
        title: item.title,
        type: assessmentType,
        contentId: item.id,
        submissionModalities: item.type === "Code" ? "Code" : undefined,
        gradingMethods:
          item.type === "Code"
            ? "AutoGraded,InstructorGraded"
            : "InstructorGraded",
      });
      if (!result.success) {
        setGradedChecked(false);
        setGradedError(result.error);
        return;
      }
      router.refresh();
    });
  }

  function confirmGradedOff() {
    const targetId = linkedAssessmentId;
    setShowGradedOffConfirm(false);
    if (!targetId) return;
    startGradedTransition(async () => {
      setGradedChecked(false);
      setGradedError(null);
      const result = await deleteAssessment(courseId, targetId);
      if (!result.success) {
        setGradedChecked(true);
        setGradedError(result.error);
        return;
      }
      setRecentlyDeletedAssessmentId(targetId);
      router.refresh();
    });
  }

  // ── Coding test-plan stats (Code + AutoGraded only) ──
  // ponytail: narrow cast on the testPlan shape; the public endpoint strips hidden cases
  // server-side so cases[].length is the public count and the hidden count is unknown here.
  const codingCases =
    (
      initialCodingDefinition?.testPlan as
        { cases?: Array<{ kind?: string }> } | null | undefined
    )?.cases ?? [];
  const stdioCaseCount = codingCases.filter(
    (c) => c.kind === "stdio" || c.kind === "stdio-file",
  ).length;
  const functionalCaseCount = codingCases.filter(
    (c) => c.kind === "doctest" || c.kind === "clang-query",
  ).length;

  const previewContent: unknown = (() => {
    switch (selectedFormat) {
      case "Lexical":
        return editorState;
      case "Markdown":
      case "RevealJs":
        return codeBody;
      case "Video":
        return videoUrl;
      default:
        return "";
    }
  })();

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="sm" onClick={handleBack}>
          <ArrowLeft className="mr-2 h-4 w-4" />
          Back
        </Button>
        <div className="flex-1">
          <p className="text-muted-foreground text-sm">{courseTitle}</p>
          <h1 className="text-2xl font-bold">{item.title}</h1>
        </div>
        <Badge variant="secondary">{contentTypeLabel}</Badge>
      </div>

      {/* Publication settings — configured before the content */}
      <Card>
        <CardHeader>
          <CardTitle>{contentTypeLabel} publication</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-6 md:grid-cols-3">
          <div className="space-y-2">
            <Label htmlFor="visibility">
              {contentTypeLabel} visibility
            </Label>
            <Select
              value={visibility}
              onValueChange={(v) =>
                setVisibility(v as LearningCoursesVisibility)
              }
            >
              <SelectTrigger id="visibility">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {CONTENT_ITEM_VISIBILITIES.map((v) => (
                  <SelectItem key={v} value={v}>
                    {formatEnumLabel(v)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-muted-foreground text-xs">
              Controls enrolled-student access only. Public course
              landing-page visibility is managed in Listing.
            </p>
          </div>

          <div className="space-y-2">
            <Label htmlFor="required">Required</Label>
            <div className="flex h-9 items-center">
              <Switch
                id="required"
                checked={isRequired}
                onCheckedChange={setIsRequired}
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="duration">
              <Clock className="mr-1 inline h-3 w-3" />
              Estimated minutes
            </Label>
            <div className="flex items-center gap-2">
              <Input
                id="duration"
                type="number"
                min={0}
                value={estimatedMinutes}
                onChange={(e) => setEstimatedMinutes(e.target.value)}
                placeholder={autoHint ? `Auto (~${autoHint} min)` : "Auto"}
              />
              {estimatedMinutes && (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setEstimatedMinutes("")}
                  title="Reset to auto estimate"
                >
                  Auto
                </Button>
              )}
            </div>
            {autoHint && !estimatedMinutes && (
              <p className="text-muted-foreground text-xs">
                Leave blank to keep auto (~{autoHint} min). Type a number to
                pin it manually.
              </p>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Actions + info */}
      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Actions</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {error && <p className="text-destructive text-sm">{error}</p>}
            {saved && (
              <p className="text-sm text-green-600">Saved successfully.</p>
            )}
            {lastSavedAt && (
              <p
                className="text-muted-foreground text-xs"
                data-testid="last-saved-at"
              >
                Last saved {lastSavedAt.toLocaleTimeString()}
              </p>
            )}
            <div className="flex flex-wrap items-center gap-2">
              <Button onClick={handleSave} disabled={isPending}>
                {isPending ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Save className="mr-2 h-4 w-4" />
                )}
                Save Changes
              </Button>
              <Button variant="outline" onClick={handleBack}>
                Cancel
              </Button>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Info</CardTitle>
          </CardHeader>
          <CardContent className="text-muted-foreground space-y-1 text-sm">
            <p>Type: {contentTypeLabel}</p>
            <p>Status: {item.status}</p>
            <p>Created: {new Date(item.createdAt).toLocaleDateString()}</p>
            <p>Updated: {new Date(item.updatedAt).toLocaleString()}</p>
          </CardContent>
        </Card>
      </div>

      {/* Content editor */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between gap-2">
            <CardTitle>{contentTypeLabel} content</CardTitle>
            {isLesson && selectedFormat !== "Markdown" && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setPreviewMode((v) => !v)}
              >
                {previewMode ? (
                  <>
                    <Pencil className="mr-2 h-4 w-4" />
                    Edit
                  </>
                ) : (
                  <>
                    <Eye className="mr-2 h-4 w-4" />
                    Preview
                  </>
                )}
              </Button>
            )}
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="title">Title</Label>
            <Input
              id="title"
              value={title}
              onChange={(e) => handleTitleChange(e.target.value)}
              placeholder="Content title"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="slug">URL Slug</Label>
            <Input
              id="slug"
              value={slug}
              onChange={(e) => handleSlugChange(e.target.value)}
              onBlur={() => setSlug(normalizeSlug(slug))}
              placeholder="introduction-to-game-development"
            />
            <p className="text-muted-foreground text-xs">
              Auto-generated from title. Edit to customize.
            </p>
          </div>

          <div className="space-y-2">
            <Label htmlFor="description">Description</Label>
            <Textarea
              id="description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Brief description for enrolled students"
              rows={3}
            />
          </div>

          <Separator />

          {isGradedType && (
            <div
              className="space-y-3 rounded-md border p-4"
              data-testid="graded-section"
            >
              <div className="flex items-center justify-between gap-2">
                <div>
                  <p className="font-medium">Graded</p>
                  <p className="text-muted-foreground text-sm">
                    Link this content to a gradebook assessment.
                  </p>
                </div>
                <Switch
                  aria-label="Graded"
                  checked={gradedChecked}
                  disabled={isGradedPending}
                  onCheckedChange={handleGradedToggle}
                />
              </div>
              {isGradedPending && (
                <p className="text-muted-foreground text-sm">
                  <Loader2 className="mr-2 inline h-3 w-3 animate-spin" />
                  Updating gradebook link…
                </p>
              )}
              {gradedError && (
                <p className="text-destructive text-sm">{gradedError}</p>
              )}
              <AlertDialog
                open={showGradedOffConfirm}
                onOpenChange={setShowGradedOffConfirm}
              >
                <AlertDialogContent>
                  <AlertDialogHeader>
                    <AlertDialogTitle>Remove grading?</AlertDialogTitle>
                    <AlertDialogDescription>
                      This soft-deletes the linked assessment. Existing
                      submissions are preserved. Toggle Graded back on to
                      restore it.
                    </AlertDialogDescription>
                  </AlertDialogHeader>
                  <AlertDialogFooter>
                    <AlertDialogCancel>Cancel</AlertDialogCancel>
                    <AlertDialogAction
                      className={buttonVariants({ variant: "destructive" })}
                      onClick={confirmGradedOff}
                    >
                      Remove grading
                    </AlertDialogAction>
                  </AlertDialogFooter>
                </AlertDialogContent>
              </AlertDialog>
            </div>
          )}

          <Separator />

          {/* ── Lesson format display + body editor ── */}
          {isLesson && (
            <div className="space-y-2">
              <Label htmlFor="lesson-format">Lesson format</Label>
              <Input
                id="lesson-format"
                value={getLessonFormatLabel(selectedFormat)}
                readOnly
                className="bg-muted"
              />
            </div>
          )}

          {isLesson && selectedFormat === "Lexical" && !previewMode && (
            <LessonContentEditor
              itemId={item.id}
              initialState={initialLexicalState}
              onChange={handleEditorChange}
            />
          )}

          {isLesson && selectedFormat === "Markdown" && (
            <div className="grid gap-4 lg:grid-cols-2">
              <LessonCodeEditor
                key={item.id}
                initialValue={codeBody}
                language="markdown"
                placeholder="Write lesson content in Markdown."
                onChange={setCodeBody}
              />
              <div
                data-testid="lesson-preview"
                className="flex h-[452px] flex-col overflow-hidden rounded-lg border border-gray-200 dark:border-gray-700"
              >
                <div className="border-b border-gray-200 px-4 py-2 text-sm font-medium text-muted-foreground dark:border-gray-700">
                  Preview
                </div>
                <div className="flex-1 overflow-auto p-4">
                  <LearnerLessonRenderer
                    courseId={courseId}
                    itemId={item.id}
                    format="Markdown"
                    content={codeBody}
                  />
                </div>
              </div>
            </div>
          )}

          {isLesson && selectedFormat === "Html" && !previewMode && (
            <LessonCodeEditor
              key={item.id}
              initialValue={codeBody}
              language="html"
              placeholder="Write lesson content in HTML."
              onChange={setCodeBody}
            />
          )}

          {isLesson && selectedFormat === "RevealJs" && !previewMode && (
            <LessonCodeEditor
              key={item.id}
              initialValue={codeBody}
              language="markdown"
              placeholder="Author slides in Markdown — separate slides with --- on its own line."
              onChange={setCodeBody}
            />
          )}

          {isLesson && selectedFormat === "Video" && !previewMode && (
            <LessonVideoEditor
              key={item.id}
              initialValue={videoUrl}
              onChange={setVideoUrl}
            />
          )}

          {isLesson && selectedFormat !== "Markdown" && previewMode && (
            <div
              data-testid="lesson-preview"
              className="rounded-md border p-4"
            >
              <LearnerLessonRenderer
                courseId={courseId}
                itemId={item.id}
                format={selectedFormat}
                content={previewContent}
              />
            </div>
          )}

          {isQuiz && (
            <QuizContentEditor
              key={item.id}
              initialContent={initialQuizContent}
              onChange={handleQuizContentChange}
              mode={previewMode ? "preview" : "edit"}
            />
          )}

          {isCode && gradedChecked && isAutoGraded && (
            <div
              className="space-y-3 rounded-md border p-4"
              data-testid="coding-tests-section"
            >
              <div className="flex items-center justify-between gap-2">
                <div>
                  <p className="font-medium">Coding Tests</p>
                  <p className="text-muted-foreground text-sm">
                    Configure test cases, starter files, and the run
                    environment in the coding-definition editor.
                  </p>
                </div>
                {initialCodingDefinition ? (
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() =>
                      (linkedAssessmentSlug ?? linkedAssessmentId) &&
                      router.push(
                        codingDefinitionRoute(
                          linkedAssessmentSlug ?? linkedAssessmentId!,
                        ),
                      )
                    }
                    disabled={!linkedAssessmentSlug && !linkedAssessmentId}
                  >
                    <Pencil className="mr-2 h-4 w-4" />
                    Edit Coding Tests
                  </Button>
                ) : (
                  <Button
                    type="button"
                    size="sm"
                    onClick={handleConfigureCoding}
                    disabled={!linkedAssessmentId}
                  >
                    <Pencil className="mr-2 h-4 w-4" />
                    Configure Coding Tests
                  </Button>
                )}
              </div>
              {initialCodingDefinition ? (
                <div className="text-muted-foreground space-y-1 text-sm">
                  <p>Language: {initialCodingDefinition.language}</p>
                  <p>Test cases: {codingCases.length} (public)</p>
                  <p>
                    Types: {stdioCaseCount} stdin/stdout ·{" "}
                    {functionalCaseCount} functional
                  </p>
                  <p>
                    Passing score: {initialCodingDefinition.passingScore}/
                    {initialCodingDefinition.maxScore}
                  </p>
                </div>
              ) : !linkedAssessmentId ? (
                <p className="text-muted-foreground text-sm">
                  Link this content item to an assessment to enable coding
                  tests.
                </p>
              ) : null}
              {codingError && (
                <p className="text-destructive text-sm">{codingError}</p>
              )}
            </div>
          )}

          {!isLesson && !isQuiz && !isGradedType && (
            <div className="space-y-2">
              <Label>Body</Label>
              <p className="text-muted-foreground text-sm py-8 text-center">
                Editor for <strong>{contentTypeLabel}</strong> content is
                not yet available.
              </p>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
