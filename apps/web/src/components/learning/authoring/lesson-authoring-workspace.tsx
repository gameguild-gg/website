"use client";

import { LearnerLessonRenderer } from "@/components/learning/learner-lesson-renderer";
import { LessonCodeEditor } from "@/components/learning/console/courses/[course]/content/[contentId]/lesson-code-editor";
import { LessonContentEditor } from "@/components/learning/console/courses/[course]/content/[contentId]/lesson-content-editor";
import { LessonVideoEditor } from "@/components/learning/console/courses/[course]/content/[contentId]/lesson-video-editor";
import { LessonExternalLinkEditor } from "@/components/learning/console/courses/[course]/content/[contentId]/lesson-external-link-editor";
import { QuizContentEditor } from "@/components/learning/console/courses/[course]/content/[contentId]/quiz-content-editor";
import { CodingDefinitionEditor } from "@/components/learning/console/courses/[course]/assessments/[assessmentId]/coding-definition/coding-definition-editor";
import { CodingAssignmentPreview } from "@/components/learning/authoring/coding-assignment-preview";
import {
  AssetPickerDialog,
  AssetsProvider,
} from "@game-guild/assets/react";
import type { AssetRecord } from "@game-guild/assets";
import type { CodingAssignmentContent } from "@/lib/coding-assignment/types";
import {
  createAssessment,
  deleteAssessment,
  restoreAssessment,
} from "@/lib/learning/actions";
import { hasReviewMethod } from "@/lib/learning/assessment-grading-methods";
import type { Assessment } from "@/lib/learning/queries/assessments";
import { createReviewMethods } from "@game-guild/grading";
import {
  applyAiProposal,
  cancelAiAuthoringRun,
  createAiAuthoringRun,
  discardAiProposal,
  getAiAuthoringRun,
  getAiConversations,
  getAiEntitlement,
  getAuthoringDraft,
  publishAuthoringDraft,
  saveAuthoringDraft,
  type AiAuthoringMessage,
  type AiAuthoringRun,
  type AiEntitlement,
  type AiProposal,
  type AiProposalKind,
  type AuthoringContentPayload,
  type AuthoringDraft,
} from "@/lib/learning/authoring";
import type {
  CourseContentItemDetailViewModel,
  CourseContentItemViewModel,
} from "@/lib/learning/view-models";
import { useLearningBase } from "@/lib/learning/use-learning-base";
import { configureMonacoWorkers } from "@/lib/learning/configure-monaco-workers";
import { getLearningAssetRepository } from "@/lib/learning/assets/learning-asset-repository";
import { prepareAuthoringAssets } from "@/lib/learning/assets/prepare-authoring-assets";
import { resolveAuthoringContentKind } from "@/lib/learning/authoring-content-kind";
import { normalizeSlug, slugify } from "@/lib/slugify";
import { Badge } from "@game-guild/ui/components/badge";
import { Button } from "@game-guild/ui/components/button";
import { buttonVariants } from "@game-guild/ui/components/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@game-guild/ui/components/dialog";
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
import { Input } from "@game-guild/ui/components/input";
import { Label } from "@game-guild/ui/components/label";
import { ScrollArea } from "@game-guild/ui/components/scroll-area";
import {
  SelectGroup,
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@game-guild/ui/components/select";
import { Separator } from "@game-guild/ui/components/separator";
import { Switch } from "@game-guild/ui/components/switch";
import { Textarea } from "@game-guild/ui/components/textarea";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@game-guild/ui/components/tooltip";
import type { SerializedEditorState } from "lexical";
import {
  ArrowLeft,
  BookOpen,
  Bot,
  Check,
  ChevronsLeft,
  ChevronsRight,
  CircleAlert,
  Clock3,
  FileCode2,
  FileText,
  Image,
  Loader2,
  PanelLeftClose,
  PanelLeftOpen,
  PanelRightClose,
  PanelRightOpen,
  Play,
  RotateCcw,
  Search,
  Send,
  Settings2,
  Sparkles,
  SplitSquareHorizontal,
  Users,
} from "lucide-react";
import { useParams, useRouter } from "next/navigation";
import { useTheme } from "next-themes";
import {
  lazy,
  Suspense,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  useSyncExternalStore,
  useTransition,
} from "react";

configureMonacoWorkers();

const MonacoDiffEditor = lazy(async () => {
  const monacoReact = await import("@monaco-editor/react");
  return { default: monacoReact.DiffEditor };
});

type EditorMode = "editor" | "split" | "preview";
type SaveStatus = "saved" | "saving" | "offline" | "conflict";
type RightPanel = "settings" | "copilot";

interface LessonAuthoringWorkspaceProps {
  courseId: string;
  courseSlug: string;
  courseTitle: string;
  item: CourseContentItemDetailViewModel;
  curriculum: CourseContentItemViewModel[];
  initialDraft: AuthoringDraft;
  linkedAssessment?: Assessment | null;
  initialCodingAssignment?: CodingAssignmentContent | null;
}

const statusCopy: Record<SaveStatus, string> = {
  saved: "Saved",
  saving: "Saving…",
  offline: "Offline",
  conflict: "Conflict",
};

export function AuthoringLocalTime({
  value,
  className,
}: {
  value: string;
  className?: string;
}) {
  const isHydrated = useSyncExternalStore(
    () => () => undefined,
    () => true,
    () => false,
  );
  const date = new Date(value);
  const label =
    isHydrated && !Number.isNaN(date.getTime())
      ? date.toLocaleTimeString()
      : "—";

  return (
    <time className={className} dateTime={value}>
      {label}
    </time>
  );
}

function iconFor(type: CourseContentItemViewModel["type"]) {
  if (type === "Questionnaire") return CircleAlert;
  if (type === "Code") return FileCode2;
  return FileText;
}

function contentItemType(value: unknown): string | null {
  if (typeof value !== "object" || value === null || !("type" in value)) {
    return null;
  }

  return typeof value.type === "string" ? value.type : null;
}

function bodyForPreview(payload: AuthoringContentPayload) {
  if (payload.lessonFormat === "Lexical" || payload.type === "Questionnaire")
    return payload.jsonBody ?? null;
  return payload.body ?? "";
}

function parseStreamFrame(frame: string): unknown | null {
  const data = frame
    .split("\n")
    .filter((line) => line.startsWith("data:"))
    .map((line) => line.slice(5).trimStart())
    .join("\n");
  if (!data) return null;
  try {
    return JSON.parse(data);
  } catch {
    return null;
  }
}

export function LessonAuthoringWorkspace({
  courseId,
  courseSlug,
  courseTitle,
  item,
  curriculum,
  initialDraft,
  linkedAssessment = null,
  initialCodingAssignment = null,
}: LessonAuthoringWorkspaceProps) {
  const router = useRouter();
  const params = useParams<{ locale?: string }>();
  const learningBase = useLearningBase();
  const { resolvedTheme } = useTheme();
  const [draft, setDraft] = useState(initialDraft);
  const [payload, setPayload] = useState(initialDraft.payload);
  const [codingAssignment, setCodingAssignment] = useState(
    initialCodingAssignment,
  );
  const itemType = contentItemType(item);
  const payloadType = contentItemType(payload);
  const linkedAssessmentId = linkedAssessment?.id ?? null;
  const [gradedChecked, setGradedChecked] = useState(!!linkedAssessmentId);
  const [activeAssessmentId, setActiveAssessmentId] = useState<
    string | null | undefined
  >(linkedAssessmentId);
  const [activeReviewMethods, setActiveReviewMethods] = useState(
    linkedAssessment?.reviewMethods,
  );
  const [recentlyDeletedAssessmentId, setRecentlyDeletedAssessmentId] =
    useState<string | null>(null);
  const [showGradedOffConfirm, setShowGradedOffConfirm] = useState(false);
  const [gradedError, setGradedError] = useState<string | null>(null);
  const [isGradedPending, startGradedTransition] = useTransition();
  const [mode, setMode] = useState<EditorMode>(() =>
    resolveAuthoringContentKind(itemType, payloadType) === "lesson"
      ? "split"
      : "editor",
  );
  const [saveStatus, setSaveStatus] = useState<SaveStatus>("saved");
  const [saveError, setSaveError] = useState<string | null>(null);
  const [conflictBackup, setConflictBackup] =
    useState<AuthoringContentPayload | null>(null);
  const [leftOpen, setLeftOpen] = useState(true);
  const [rightOpen, setRightOpen] = useState(true);
  const [mobileLeftOpen, setMobileLeftOpen] = useState(false);
  const [mobileRightOpen, setMobileRightOpen] = useState(false);
  const [rightPanel, setRightPanel] = useState<RightPanel>("settings");
  const [curriculumSearch, setCurriculumSearch] = useState("");
  const [isPublishing, setIsPublishing] = useState(false);
  const [assetPickerOpen, setAssetPickerOpen] = useState(false);
  const [copilotPrompt, setCopilotPrompt] = useState("");
  const [proposalMode, setProposalMode] =
    useState<AiProposalKind>("ReplaceDocument");
  const [isRunning, setIsRunning] = useState(false);
  const [aiError, setAiError] = useState<string | null>(null);
  const [entitlement, setEntitlement] = useState<AiEntitlement | null>(null);
  const [messages, setMessages] = useState<AiAuthoringMessage[]>([]);
  const [conversationId, setConversationId] = useState<string | null>(null);
  const [activeRun, setActiveRun] = useState<AiAuthoringRun | null>(null);
  const [proposal, setProposal] = useState<AiProposal | null>(null);
  const [diffOpen, setDiffOpen] = useState(false);
  const [unifiedDiff, setUnifiedDiff] = useState(false);
  const [savedSnapshot, setSavedSnapshot] = useState(
    JSON.stringify(initialDraft.payload),
  );
  const revisionRef = useRef(initialDraft.revision);
  const payloadRef = useRef(initialDraft.payload);
  const cursorOffsetRef = useRef<number | undefined>(undefined);
  const streamAbortRef = useRef<AbortController | null>(null);
  const restoredRunRef = useRef(false);
  const saveInFlightRef = useRef<Promise<AuthoringDraft | null> | null>(null);
  const assetRepository = useMemo(() => getLearningAssetRepository(), []);
  const assetScope = useMemo(
    () => ({ type: "ProgramContent", id: item.id }),
    [item.id],
  );

  const format =
    payload.lessonFormat ?? (payload.jsonBody ? "Lexical" : "Markdown");
  const contentKind = resolveAuthoringContentKind(itemType, payloadType);
  const isCode = contentKind === "code";
  const isQuiz = contentKind === "quiz";
  const isLesson = contentKind === "lesson";
  const formatLabel = isCode ? "Coding assignment" : isQuiz ? "Quiz" : format;
  const isStructured = isQuiz || (isLesson && format === "Lexical");
  const currentPayloadJson = JSON.stringify(payload);
  const isDirty = savedSnapshot !== currentPayloadJson;
  const activeRunStorageKey = `authoring-ai-run:${courseId}:${item.id}`;

  useEffect(() => () => streamAbortRef.current?.abort(), []);

  useEffect(() => {
    payloadRef.current = payload;
  }, [payload]);

  const persist = useCallback(
    async (nextPayload = payloadRef.current) => {
      if (saveInFlightRef.current) return saveInFlightRef.current;
      setSaveStatus("saving");
      setSaveError(null);
      const revision = revisionRef.current;
      const operation = prepareAuthoringAssets(
        nextPayload,
        assetRepository,
        assetScope,
      )
        .then(() =>
          saveAuthoringDraft(courseId, item.id, revision, nextPayload),
        )
        .then((result) => {
          if (!result.success) {
            if (result.status === 409) {
              setConflictBackup(nextPayload);
              setSaveStatus("conflict");
            } else setSaveStatus("offline");
            setSaveError(result.error);
            return null;
          }
          revisionRef.current = result.data.revision;
          setSavedSnapshot(JSON.stringify(nextPayload));
          setDraft(result.data);
          setSaveStatus("saved");
          return result.data;
        })
        .catch((error: unknown) => {
          setSaveStatus("offline");
          setSaveError(
            error instanceof Error
              ? error.message
              : "Unable to save this draft.",
          );
          return null;
        })
        .finally(() => {
          saveInFlightRef.current = null;
        });
      saveInFlightRef.current = operation;
      return operation;
    },
    [assetRepository, assetScope, courseId, item.id],
  );

  const loadLatestDraft = async () => {
    const result = await getAuthoringDraft(courseId, item.id);
    if (!result.success) {
      setSaveError(result.error);
      return;
    }
    setDraft(result.data);
    setPayload(result.data.payload);
    payloadRef.current = result.data.payload;
    revisionRef.current = result.data.revision;
    setSavedSnapshot(JSON.stringify(result.data.payload));
    setSaveStatus("saved");
    setSaveError(null);
  };

  const restoreConflictCopy = () => {
    // This action is rendered only while a conflict backup exists.
    const backup = conflictBackup as AuthoringContentPayload;
    setPayload(backup);
    payloadRef.current = backup;
    setConflictBackup(null);
    setSaveStatus("saved");
    setSaveError(null);
  };

  useEffect(() => {
    if (!isDirty || !payload.title.trim() || saveStatus === "conflict") return;
    const timer = window.setTimeout(() => void persist(payload), 1200);
    return () => window.clearTimeout(timer);
  }, [isDirty, payload, persist, saveStatus]);

  useEffect(() => {
    if (!isDirty) return;
    const warnAboutUnsavedChanges = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", warnAboutUnsavedChanges);
    return () =>
      window.removeEventListener("beforeunload", warnAboutUnsavedChanges);
  }, [isDirty]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const command = event.ctrlKey || event.metaKey;
      if (command && event.key.toLowerCase() === "s") {
        event.preventDefault();
        void persist();
      }
      if (command && event.shiftKey && event.key.toLowerCase() === "i") {
        event.preventDefault();
        setRightPanel("copilot");
        if (window.matchMedia("(min-width: 1280px)").matches)
          setRightOpen(true);
        else setMobileRightOpen(true);
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [persist]);

  useEffect(() => {
    if (rightPanel !== "copilot" || entitlement) return;
    void Promise.all([
      getAiEntitlement(courseId, item.id),
      getAiConversations(courseId, item.id),
    ]).then(([creditResult, conversationResult]) => {
      if (creditResult.success) setEntitlement(creditResult.data);
      if (conversationResult.success && conversationResult.data[0]) {
        setConversationId(conversationResult.data[0].id);
        setMessages(conversationResult.data[0].messages);
      }
    });
  }, [courseId, entitlement, item.id, rightPanel]);

  const filteredCurriculum = useMemo(() => {
    const query = curriculumSearch.trim().toLowerCase();
    return curriculum
      .filter((entry) => !query || entry.title.toLowerCase().includes(query))
      .sort((a, b) => a.order - b.order);
  }, [curriculum, curriculumSearch]);

  const updatePayload = <K extends keyof AuthoringContentPayload>(
    key: K,
    value: AuthoringContentPayload[K],
  ) => setPayload((current) => ({ ...current, [key]: value }));

  const handleTitle = (value: string) => {
    setPayload((current) => ({
      ...current,
      title: value,
      slug:
        current.slug === slugify(current.title) || !current.slug
          ? slugify(value)
          : current.slug,
    }));
  };

  const handleMediaSelected = (result: AssetRecord | AssetRecord[]) => {
    const selected = Array.isArray(result) ? result[0] : result;
    if (!selected) return;
    // The persist pipeline promotes any asset:// uri found in the payload.
    setPayload((current) => ({
      ...current,
      body: `${current.body ?? ""}\n\n![${selected.name}](${selected.uri})`,
    }));
  };

  const toggleLeftPanel = () => {
    if (window.matchMedia("(min-width: 1024px)").matches)
      setLeftOpen((value) => !value);
    else setMobileLeftOpen((value) => !value);
  };

  const toggleRightPanel = () => {
    if (window.matchMedia("(min-width: 1280px)").matches)
      setRightOpen((value) => !value);
    else setMobileRightOpen((value) => !value);
  };

  const openCopilot = () => {
    setRightPanel("copilot");
    if (window.matchMedia("(min-width: 1280px)").matches) setRightOpen(true);
    else setMobileRightOpen(true);
  };

  const handlePublish = async () => {
    setIsPublishing(true);
    setSaveError(null);
    const savedDraft = isDirty ? await persist() : draft;
    if (!savedDraft) {
      setIsPublishing(false);
      return;
    }
    try {
      await prepareAuthoringAssets(
        payloadRef.current,
        assetRepository,
        assetScope,
      );
    } catch (error) {
      setSaveError(
        error instanceof Error
          ? error.message
          : "Lesson assets are not ready to publish.",
      );
      setSaveStatus("offline");
      setIsPublishing(false);
      return;
    }
    const result = await publishAuthoringDraft(
      courseId,
      item.id,
      savedDraft.revision,
    );
    if (result.success) {
      const publishedDraft = result.data.draft;
      setDraft(publishedDraft);
      setPayload(publishedDraft.payload);
      payloadRef.current = publishedDraft.payload;
      revisionRef.current = publishedDraft.revision;
      setSavedSnapshot(JSON.stringify(publishedDraft.payload));
      setSaveStatus("saved");
      const publishedSlug = result.data.publishedContent.slug;
      if (publishedSlug !== item.slug) {
        router.replace(
          `${learningBase}/courses/${encodeURIComponent(courseSlug)}/content/${encodeURIComponent(publishedSlug)}`,
        );
      } else {
        router.refresh();
      }
    } else {
      setSaveError(result.error);
      setSaveStatus(result.status === 409 ? "conflict" : "offline");
    }
    setIsPublishing(false);
  };

  const GRADED_CONTENT_TYPES: ReadonlySet<string> = new Set([
    "Assignment",
    "Project",
    "Code",
  ]);
  const isGradedType = GRADED_CONTENT_TYPES.has(
    itemType ?? "",
  );
  const isAutomatedReview =
    activeReviewMethods != null
      ? hasReviewMethod(activeReviewMethods, "AutomatedReview")
      : false;
  const CONTENT_TO_ASSESSMENT_TYPE: Record<
    string,
    "Assignment" | "Quiz" | "Project"
  > = {
    Assignment: "Assignment",
    Project: "Project",
    Code: "Assignment",
  };

  const handleGradedToggle = (next: boolean) => {
    if (next === gradedChecked || isGradedPending) return;
    if (!next) {
      setShowGradedOffConfirm(true);
      return;
    }
    const restoreTargetId =
      recentlyDeletedAssessmentId ?? activeAssessmentId ?? null;
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
        setActiveAssessmentId(restoreTargetId);
        setRecentlyDeletedAssessmentId(null);
        router.refresh();
        return;
      }
      const assessmentType =
        CONTENT_TO_ASSESSMENT_TYPE[itemType ?? ""] ?? "Assignment";
      const reviewMethods =
        itemType === "Code"
          ? createReviewMethods("AutomatedReview", true)
          : createReviewMethods("InstructorReview");
      const result = await createAssessment({
        courseId,
        title: payload.title,
        type: assessmentType,
        contentId: item.id,
        submissionModalities: itemType === "Code" ? "Code" : undefined,
        reviewMethods,
      });
      if (!result.success) {
        setGradedChecked(false);
        setGradedError(result.error);
        return;
      }
      setActiveAssessmentId(result.data.id);
      setActiveReviewMethods(reviewMethods);
      router.refresh();
    });
  };

  const confirmGradedOff = () => {
    const targetId = activeAssessmentId ?? null;
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
      setActiveAssessmentId(undefined);
      setRecentlyDeletedAssessmentId(targetId);
      router.refresh();
    });
  };

  const readRunStream = useCallback(
    async (run: AiAuthoringRun) => {
      let lastEventId = 0;
      let reconnectAttempts = 0;
      streamAbortRef.current?.abort();
      const abortController = new AbortController();
      streamAbortRef.current = abortController;
      window.sessionStorage.setItem(activeRunStorageKey, run.id);

      while (!abortController.signal.aborted) {
        try {
          const response = await fetch(
            `/api/learning/authoring/${encodeURIComponent(courseId)}/${encodeURIComponent(item.id)}/runs/${encodeURIComponent(run.id)}/stream`,
            {
              headers: lastEventId
                ? { "Last-Event-ID": String(lastEventId) }
                : {},
              signal: abortController.signal,
            },
          );
          if (!response.ok || !response.body)
            throw new Error("The Copilot stream could not be opened.");
          reconnectAttempts = 0;
          const reader = response.body.getReader();
          const decoder = new TextDecoder();
          let buffer = "";
          while (true) {
            const { done, value } = await reader.read();
            buffer += decoder.decode(value, { stream: !done });
            const frames = buffer.split("\n\n");
            // String#split always returns at least one segment.
            buffer = frames.pop() as string;
            for (const frame of frames) {
              const id = frame.match(/^id:\s*(\d+)/m)?.[1];
              if (id) lastEventId = Number(id);
              const event = parseStreamFrame(frame) as {
                delta?: string;
                proposal?: AiProposal;
                errorCode?: string;
              } | null;
              if (event?.delta) {
                const delta = event.delta;
                setMessages((current) => {
                  const last = current.at(-1);
                  if (last?.role === "assistant" && last.runId === run.id)
                    return [
                      ...current.slice(0, -1),
                      { ...last, content: last.content + delta },
                    ];
                  return [
                    ...current,
                    {
                      id: `stream-${run.id}`,
                      role: "assistant",
                      content: delta,
                      runId: run.id,
                      createdAt: new Date().toISOString(),
                    },
                  ];
                });
              }
              if (event?.proposal) {
                setProposal(event.proposal);
                setDiffOpen(true);
              }
              if (
                event?.errorCode &&
                !["AI_CANCEL_REQUESTED", "AI_CANCELLED"].includes(
                  event.errorCode,
                )
              )
                setAiError(event.errorCode);
            }
            if (done) break;
          }

          const latest = await getAiAuthoringRun(courseId, item.id, run.id);
          if (!latest.success) throw new Error(latest.error);
          setActiveRun(latest.data);
          if (latest.data.proposal) {
            setProposal(latest.data.proposal);
            setDiffOpen(true);
          }
          if (
            ["Completed", "Failed", "Cancelled"].includes(latest.data.status)
          ) {
            window.sessionStorage.removeItem(activeRunStorageKey);
            if (latest.data.status === "Failed")
              setAiError(
                latest.data.errorMessage ?? "Copilot generation failed.",
              );
            const balance = await getAiEntitlement(courseId, item.id);
            if (balance.success) setEntitlement(balance.data);
            return;
          }
        } catch (error) {
          if (abortController.signal.aborted) return;
          reconnectAttempts += 1;
          if (reconnectAttempts > 4) throw error;
        }
        await new Promise((resolve) =>
          window.setTimeout(
            resolve,
            Math.min(750 * 2 ** reconnectAttempts, 5000),
          ),
        );
      }
    },
    [activeRunStorageKey, courseId, item.id],
  );

  useEffect(() => {
    if (restoredRunRef.current) return;
    restoredRunRef.current = true;
    const runId = window.sessionStorage.getItem(activeRunStorageKey);
    if (!runId) return;
    void getAiAuthoringRun(courseId, item.id, runId).then(async (result) => {
      if (!result.success) {
        window.sessionStorage.removeItem(activeRunStorageKey);
        return;
      }
      setActiveRun(result.data);
      if (result.data.proposal) {
        setProposal(result.data.proposal);
        setDiffOpen(result.data.proposal.status === "Pending");
      }
      if (["Completed", "Failed", "Cancelled"].includes(result.data.status)) {
        window.sessionStorage.removeItem(activeRunStorageKey);
        return;
      }
      setRightPanel("copilot");
      setRightOpen(true);
      setIsRunning(true);
      try {
        await readRunStream(result.data);
      } catch (error) {
        setAiError(
          error instanceof Error
            ? error.message
            : "Copilot stopped unexpectedly.",
        );
      } finally {
        setIsRunning(false);
      }
    });
  }, [activeRunStorageKey, courseId, item.id, readRunStream]);

  const runCopilot = async (instruction = copilotPrompt) => {
    if (!instruction.trim() || isRunning) return;
    setAiError(null);
    const saved = isDirty ? await persist() : draft;
    if (!saved) return;
    setIsRunning(true);
    setMessages((current) => [
      ...current,
      {
        id: `local-${crypto.randomUUID()}`,
        role: "user",
        content: instruction.trim(),
        createdAt: new Date().toISOString(),
      },
    ]);
    const kind = isQuiz
      ? "QuizPatch"
      : format === "Lexical"
        ? "LexicalPatch"
        : format === "Video" || format === "ExternalLink"
          ? "MetadataPatch"
          : proposalMode;
    const result = await createAiAuthoringRun(courseId, item.id, {
      conversationId,
      draftRevision: saved.revision,
      instruction: instruction.trim(),
      proposalKind: kind,
      idempotencyKey: crypto.randomUUID(),
    });
    if (!result.success) {
      setAiError(result.error);
      setIsRunning(false);
      return;
    }
    setCopilotPrompt("");
    setActiveRun(result.data);
    setConversationId(result.data.conversationId);
    try {
      await readRunStream(result.data);
    } catch (error) {
      setAiError(
        error instanceof Error
          ? error.message
          : "Copilot stopped unexpectedly.",
      );
    } finally {
      setIsRunning(false);
    }
  };

  const stopCopilot = async () => {
    // The stop control is shown only while a run is active. It can briefly be
    // clicked before run creation returns, so keep only that meaningful guard.
    if (!activeRun) return;
    const result = await cancelAiAuthoringRun(courseId, item.id, activeRun.id);
    if (!result.success) {
      setAiError(result.error);
      return;
    }
    setActiveRun(result.data);
    if (result.data.status === "Cancelled") {
      streamAbortRef.current?.abort();
      window.sessionStorage.removeItem(activeRunStorageKey);
      setIsRunning(false);
      const balance = await getAiEntitlement(courseId, item.id);
      if (balance.success) setEntitlement(balance.data);
    }
  };

  const acceptProposal = async (proposalToApply: AiProposal) => {
    const result = await applyAiProposal(
      courseId,
      item.id,
      proposalToApply.id,
      revisionRef.current,
      proposalToApply.kind === "InsertAtCursor"
        ? cursorOffsetRef.current
        : undefined,
    );
    if (!result.success) {
      setAiError(result.error);
      if (result.status === 409) setSaveStatus("conflict");
      return;
    }
    setDraft(result.data);
    setPayload(result.data.payload);
    revisionRef.current = result.data.revision;
    setSavedSnapshot(JSON.stringify(result.data.payload));
    setProposal({ ...proposalToApply, status: "Applied" });
    setDiffOpen(false);
    setSaveStatus("saved");
  };

  const rejectProposal = async (proposalToDiscard: AiProposal) => {
    const result = await discardAiProposal(
      courseId,
      item.id,
      proposalToDiscard.id,
    );
    if (result.success) setProposal(result.data);
    setDiffOpen(false);
  };

  const renderEditor = () => {
    if (isCode)
      return (
        <CodingDefinitionEditor
          courseId={courseSlug}
          assessmentId={linkedAssessment?.id ?? item.id}
          assessmentSlug={linkedAssessment?.slug ?? payload.slug}
          programId={courseId}
          contentId={item.id}
          assessmentTitle={linkedAssessment?.title ?? payload.title}
          initialContent={codingAssignment}
          embedded
          onSaved={(content) => {
            setCodingAssignment(content);
            router.refresh();
          }}
        />
      );
    if (isQuiz)
      return (
        <QuizContentEditor
          key={`${item.id}-${draft.revision}`}
          initialContent={payload.jsonBody}
          onChange={(value) => updatePayload("jsonBody", value)}
        />
      );
    if (isLesson && format === "Lexical")
      return (
        <LessonContentEditor
          key={`${item.id}-${draft.revision}`}
          itemId={item.id}
          initialState={
            (payload.jsonBody ?? null) as SerializedEditorState | null
          }
          onChange={(value) =>
            updatePayload(
              "jsonBody",
              value as unknown as Record<string, unknown>,
            )
          }
        />
      );
    if (isLesson && format === "Video")
      return (
        <LessonVideoEditor
          key={`${item.id}-${draft.revision}`}
          initialValue={payload.body ?? ""}
          onChange={(value) => updatePayload("body", value)}
        />
      );
    if (isLesson && format === "ExternalLink")
      return (
        <LessonExternalLinkEditor
          key={`${item.id}-${draft.revision}`}
          initialValue={payload.body ?? ""}
          onChange={(value) => updatePayload("body", value)}
        />
      );
    return (
      <LessonCodeEditor
        key={`${item.id}-${draft.revision}`}
        initialValue={payload.body ?? ""}
        language={format === "Html" ? "html" : "markdown"}
        onChange={(value) => updatePayload("body", value)}
        onCursorOffsetChange={(offset) => {
          cursorOffsetRef.current = offset;
        }}
        placeholder={
          format === "RevealJs"
            ? "Separate slides with --- on its own line."
            : undefined
        }
      />
    );
  };

  const editorPanel = (
    <section className="h-full min-w-0 overflow-auto bg-background px-5 py-6 xl:px-8">
      <div className="mx-auto w-full max-w-4xl">
        <Textarea
          aria-label="Lesson title"
          value={payload.title}
          onChange={(event) => handleTitle(event.target.value)}
          rows={1}
          className="mb-5 min-h-0 resize-none overflow-hidden border-0 bg-transparent px-0 text-2xl font-semibold leading-tight shadow-none focus-visible:ring-0 md:text-3xl [field-sizing:content]"
        />
        <div className="authoring-editor-surface [&_[data-slot=card]]:rounded-none [&_[data-slot=card]]:border-0 [&_[data-slot=card]]:shadow-none [&_label]:text-sm">
          {renderEditor()}
        </div>
      </div>
    </section>
  );

  const previewPanel = (
    <section className="h-full min-w-0 overflow-auto bg-muted/20 px-6 py-8 xl:px-10">
      <article className="mx-auto max-w-3xl">
        <p className="mb-3 text-sm font-medium text-primary">{courseTitle}</p>
        <h1 className="text-3xl font-semibold tracking-tight">
          {payload.title}
        </h1>
        {payload.description ? (
          <p className="mt-3 text-base text-muted-foreground">
            {payload.description}
          </p>
        ) : null}
        <Separator className="my-7" />
        {isCode && codingAssignment ? (
          <CodingAssignmentPreview assignment={codingAssignment} />
        ) : isCode ? (
          <div className="flex min-h-80 items-center justify-center rounded-lg border border-dashed p-8 text-center text-sm text-muted-foreground">
            Save the coding assignment to preview the student experience.
          </div>
        ) : isQuiz ? (
          <QuizContentEditor
            initialContent={payload.jsonBody}
            onChange={() => undefined}
            mode="preview"
          />
        ) : (
          <LearnerLessonRenderer
            courseId={courseId}
            itemId={item.id}
            format={format}
            content={bodyForPreview(payload)}
          />
        )}
      </article>
    </section>
  );

  return (
    <div className="flex h-dvh min-h-0 flex-col overflow-hidden bg-background text-foreground">
      <header className="relative z-50 flex h-14 shrink-0 items-center gap-2 border-b bg-background px-3">
        <Tooltip>
          <TooltipTrigger
            render={
              <Button
                variant="ghost"
                size="icon-sm"
                aria-label="Toggle curriculum"
                onClick={toggleLeftPanel}
              />
            }
          >
            {leftOpen ? <PanelLeftClose /> : <PanelLeftOpen />}
          </TooltipTrigger>
          <TooltipContent>Toggle curriculum</TooltipContent>
        </Tooltip>
        <Button
          variant="ghost"
          size="sm"
          className="gap-2 text-muted-foreground"
          onClick={() =>
            router.push(
              `${learningBase}/courses/${encodeURIComponent(courseSlug)}/content`,
            )
          }
        >
          <ArrowLeft />
          <span className="hidden md:inline">Curriculum</span>
        </Button>
        <Separator
          orientation="vertical"
          className="mx-1 hidden h-5 md:block"
        />
        <div className="hidden min-w-0 flex-1 md:block">
          <p className="truncate text-sm font-medium">{payload.title}</p>
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Badge variant="outline" className="h-5 rounded-sm px-1.5 text-sm">
              {item.status === "published" ? "Published" : "Draft"}
            </Badge>
            <span
              className={saveStatus === "conflict" ? "text-destructive" : ""}
            >
              {saveStatus === "saving" ? (
                <Loader2 className="mr-1 inline size-3 animate-spin" />
              ) : null}
              {statusCopy[saveStatus]}
            </span>
          </div>
        </div>
        <Select
          items={[
            { value: "editor", label: "Editor" },
            { value: "preview", label: "Preview" },
          ]}
          value={mode === "preview" ? "preview" : "editor"}
          onValueChange={(value) => setMode(value as "editor" | "preview")}
        >
          <SelectTrigger
            aria-label="Editor view"
            size="sm"
            className="w-24 lg:hidden"
          >
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectGroup>
              <SelectItem value="editor">Editor</SelectItem>
              <SelectItem value="preview">Preview</SelectItem>
            </SelectGroup>
          </SelectContent>
        </Select>
        <div className="hidden items-center rounded-md border bg-muted/25 p-0.5 lg:flex">
          {(["editor", "split", "preview"] as EditorMode[]).map((value) => (
            <Button
              key={value}
              aria-label={value}
              variant={mode === value ? "secondary" : "ghost"}
              size="sm"
              className="h-7 capitalize"
              onClick={() => setMode(value)}
            >
              {value === "split" ? (
                <SplitSquareHorizontal />
              ) : value === "preview" ? (
                <Play />
              ) : (
                <FileCode2 />
              )}
              <span className="hidden xl:inline">{value}</span>
            </Button>
          ))}
        </div>
        <Tooltip>
          <TooltipTrigger
            render={
              <Button
                variant="ghost"
                size="icon-sm"
                aria-label="Open student view"
                onClick={() => {
                  const learnerBase = `/${params.locale ?? "en-US"}/learn/courses/${encodeURIComponent(courseSlug)}`;
                  const href =
                    (payload.type === "Code" ||
                      payload.type === "Questionnaire") &&
                    linkedAssessment
                      ? `${learnerBase}/activities/assessment-${encodeURIComponent(linkedAssessment.id)}`
                      : payload.type === "Lesson"
                        ? `${learnerBase}/lessons/${encodeURIComponent(payload.slug)}`
                        : `${learnerBase}/activities/content-${encodeURIComponent(item.id)}`;
                  window.open(href, "_blank", "noopener,noreferrer");
                }}
              />
            }
          >
            <Users />
          </TooltipTrigger>
          <TooltipContent>Student view</TooltipContent>
        </Tooltip>
        <Button
          variant={
            rightPanel === "copilot" && rightOpen ? "secondary" : "ghost"
          }
          size="sm"
          aria-label="Open Copilot"
          onClick={openCopilot}
        >
          <Sparkles />
          <span className="hidden lg:inline">Copilot</span>
        </Button>
        <Button
          size="sm"
          aria-label="Publish changes"
          disabled={isPublishing || saveStatus === "conflict"}
          onClick={() => void handlePublish()}
        >
          {isPublishing ? <Loader2 className="animate-spin" /> : <Check />}
          <span className="hidden md:inline">Publish changes</span>
        </Button>
        <Tooltip>
          <TooltipTrigger
            render={
              <Button
                variant="ghost"
                size="icon-sm"
                aria-label="Insert media"
                onClick={() => setAssetPickerOpen(true)}
              />
            }
          >
            <Image className="size-4" />
          </TooltipTrigger>
          <TooltipContent>Insert media</TooltipContent>
        </Tooltip>
        <Tooltip>
          <TooltipTrigger
            render={
              <Button
                variant="ghost"
                size="icon-sm"
                aria-label="Toggle lesson panel"
                onClick={toggleRightPanel}
              />
            }
          >
            {rightOpen ? <PanelRightClose /> : <PanelRightOpen />}
          </TooltipTrigger>
          <TooltipContent>Toggle lesson panel</TooltipContent>
        </Tooltip>
      </header>

      {saveError || conflictBackup ? (
        <div
          role="status"
          className={`flex min-h-10 shrink-0 items-center justify-between gap-3 px-4 py-1.5 text-sm ${saveStatus === "conflict" ? "bg-destructive/10 text-destructive" : "bg-muted text-foreground"}`}
        >
          <span className="truncate">
            {saveStatus === "conflict"
              ? saveError
              : conflictBackup
                ? "Latest draft loaded. Your unsaved version is still available."
                : saveError}
          </span>
          <div className="flex shrink-0 items-center gap-1">
            {saveStatus === "conflict" ? (
              <Button
                variant="ghost"
                size="xs"
                onClick={() => void loadLatestDraft()}
              >
                <RotateCcw /> Load latest
              </Button>
            ) : conflictBackup ? (
              <>
                <Button
                  variant="ghost"
                  size="xs"
                  onClick={() => setConflictBackup(null)}
                >
                  Discard my copy
                </Button>
                <Button
                  variant="secondary"
                  size="xs"
                  onClick={restoreConflictCopy}
                >
                  Restore my changes
                </Button>
              </>
            ) : (
              <Button variant="ghost" size="xs" onClick={() => void persist()}>
                <RotateCcw /> Retry
              </Button>
            )}
          </div>
        </div>
      ) : null}

      <main className="relative flex min-h-0 flex-1">
        {mobileLeftOpen ? (
          <button
            type="button"
            aria-label="Close curriculum"
            className="fixed inset-x-0 bottom-0 top-14 z-30 bg-background/80 backdrop-blur-sm lg:hidden"
            onClick={() => setMobileLeftOpen(false)}
          />
        ) : null}
        {leftOpen || mobileLeftOpen ? (
          <aside
            aria-label="Course curriculum"
            className={`${mobileLeftOpen ? "flex" : "hidden"} fixed bottom-0 left-0 top-14 z-40 w-72 shrink-0 flex-col border-r bg-background shadow-xl lg:static lg:z-auto lg:w-64 lg:shadow-none ${leftOpen ? "lg:flex" : "lg:hidden"}`}
          >
            <div className="flex h-14 items-center gap-3 px-4">
              <div className="flex size-8 items-center justify-center rounded-md bg-primary/10 text-primary">
                <BookOpen className="size-4" />
              </div>
              <div className="min-w-0">
                <p className="truncate text-sm font-medium">{courseTitle}</p>
                <p className="text-sm text-muted-foreground">Curriculum</p>
              </div>
            </div>
            <div className="px-3 pb-3">
              <div className="relative">
                <Search className="absolute left-2.5 top-2.5 size-3.5 text-muted-foreground" />
                <Input
                  value={curriculumSearch}
                  onChange={(event) => setCurriculumSearch(event.target.value)}
                  placeholder="Search lessons and quizzes"
                  className="h-8 pl-8 text-sm"
                />
              </div>
            </div>
            <Separator />
            <ScrollArea className="min-h-0 flex-1">
              <nav aria-label="Course curriculum" className="space-y-0.5 p-2">
                {filteredCurriculum.map((entry) => {
                  const Icon = iconFor(entry.type);
                  const active = entry.id === item.id;
                  return (
                    <button
                      key={entry.id}
                      type="button"
                      className={`flex w-full items-center gap-2 rounded-md px-2.5 py-2 text-left text-sm transition-colors ${
                        active
                          ? "bg-primary/10 text-primary"
                          : "text-muted-foreground hover:bg-muted hover:text-foreground"
                      }`}
                      onClick={() => {
                        setMobileLeftOpen(false);
                        router.push(
                          `${learningBase}/courses/${encodeURIComponent(courseSlug)}/content/${encodeURIComponent(entry.slug)}`,
                        );
                      }}
                    >
                      <Icon className="size-3.5 shrink-0" />
                      <span className="min-w-0 flex-1 truncate">
                        {entry.title}
                      </span>
                      {entry.status === "published" ? (
                        <Check
                          className="size-3 text-primary"
                          aria-label="Published"
                        />
                      ) : (
                        <Clock3
                          className="size-3 text-muted-foreground"
                          aria-label="Draft"
                        />
                      )}
                    </button>
                  );
                })}
              </nav>
            </ScrollArea>
            <div className="flex h-10 items-center border-t px-3 text-sm text-muted-foreground">
              <span>{curriculum.length} items</span>
            </div>
          </aside>
        ) : null}

        <div className="min-w-0 flex-1">
          {mode === "split" ? (
            <div className="grid h-full min-h-0 grid-cols-1 2xl:grid-cols-2">
              <div className="min-h-0 border-r">{editorPanel}</div>
              <div className="hidden min-h-0 2xl:block">{previewPanel}</div>
            </div>
          ) : mode === "preview" ? (
            previewPanel
          ) : (
            editorPanel
          )}
        </div>

        {mobileRightOpen ? (
          <button
            type="button"
            aria-label="Close lesson panel"
            className="fixed inset-x-0 bottom-0 top-14 z-30 bg-background/80 backdrop-blur-sm xl:hidden"
            onClick={() => setMobileRightOpen(false)}
          />
        ) : null}
        {rightOpen || mobileRightOpen ? (
          <aside
            aria-label={
              rightPanel === "settings"
                ? "Lesson settings"
                : "AI authoring copilot"
            }
            className={`${mobileRightOpen ? "flex" : "hidden"} fixed bottom-0 right-0 top-14 z-40 w-[min(24rem,92vw)] shrink-0 flex-col border-l bg-background shadow-xl xl:static xl:z-auto xl:w-80 xl:shadow-none ${rightOpen ? "xl:flex" : "xl:hidden"}`}
          >
            <div className="flex h-12 items-center gap-1 border-b px-2">
              <Button
                variant={rightPanel === "settings" ? "secondary" : "ghost"}
                size="sm"
                className="flex-1"
                onClick={() => setRightPanel("settings")}
              >
                <Settings2 /> Settings
              </Button>
              <Button
                variant={rightPanel === "copilot" ? "secondary" : "ghost"}
                size="sm"
                className="flex-1"
                onClick={() => setRightPanel("copilot")}
              >
                <Bot /> Copilot
              </Button>
            </div>
            {rightPanel === "settings" ? (
              <ScrollArea className="min-h-0 flex-1">
                <div className="space-y-5 p-4">
                  <div>
                    <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                      Publishing
                    </h2>
                    <div className="mt-3 space-y-4">
                      <div className="space-y-1.5">
                        <Label htmlFor="authoring-visibility">
                          Lesson access
                        </Label>
                        <Select
                          items={[
                            { value: "Public", label: "Enrolled students" },
                            { value: "MembersOnly", label: "Members only" },
                            { value: "Private", label: "Private" },
                          ]}
                          value={payload.visibility}
                          onValueChange={(value) =>
                            updatePayload(
                              "visibility",
                              value as AuthoringContentPayload["visibility"],
                            )
                          }
                        >
                          <SelectTrigger id="authoring-visibility" className="w-full">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectGroup>
                              <SelectItem value="Public">Enrolled students</SelectItem>
                              <SelectItem value="MembersOnly">Members only</SelectItem>
                              <SelectItem value="Private">Private</SelectItem>
                            </SelectGroup>
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="flex items-center justify-between gap-3">
                        <div>
                          <Label htmlFor="authoring-required">
                            Required for completion
                          </Label>
                          <p className="mt-0.5 text-sm text-muted-foreground">
                            Students must complete this item.
                          </p>
                        </div>
                        <Switch
                          id="authoring-required"
                          checked={payload.isRequired}
                          onCheckedChange={(value) =>
                            updatePayload("isRequired", value)
                          }
                        />
                      </div>
                    </div>
                  </div>
                  {isGradedType ? (
                    <div className="space-y-3 rounded-md border p-4">
                      <div className="flex items-center justify-between gap-3">
                        <div>
                          <p className="font-medium">Graded</p>
                          <p className="text-sm text-muted-foreground">
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
                        <p className="text-sm text-muted-foreground">
                          <Loader2 className="mr-2 inline h-3 w-3 animate-spin" />
                          Updating gradebook link…
                        </p>
                      )}
                      {gradedError && (
                        <p className="text-sm text-destructive">{gradedError}</p>
                      )}
                      {isCode && gradedChecked && isAutomatedReview && (
                        <div className="space-y-1 text-sm text-muted-foreground">
                          <p className="font-medium">Coding tests</p>
                          <p>
                            Language: {codingAssignment?.Environment.Language}
                          </p>
                          <p>
                            Test cases:{" "}
                            {(codingAssignment?.Tests.Public.length ?? 0) +
                              (codingAssignment?.Tests.Private.length ?? 0)}{" "}
                            ({codingAssignment?.Tests.Public.length ?? 0} public)
                          </p>
                          <p>
                            Passing score: {codingAssignment?.Grading.MaxScore}
                          </p>
                        </div>
                      )}
                      <AlertDialog
                        open={showGradedOffConfirm}
                        onOpenChange={setShowGradedOffConfirm}
                      >
                        <AlertDialogContent>
                          <AlertDialogHeader>
                            <AlertDialogTitle>Remove grading?</AlertDialogTitle>
                            <AlertDialogDescription>
                              This soft-deletes the linked assessment.
                              Existing submissions are preserved. Toggle Graded
                              back on to restore it.
                            </AlertDialogDescription>
                          </AlertDialogHeader>
                          <AlertDialogFooter>
                            <AlertDialogCancel>Cancel</AlertDialogCancel>
                            <AlertDialogAction
                              className={buttonVariants({
                                variant: "destructive",
                              })}
                              onClick={confirmGradedOff}
                            >
                              Remove grading
                            </AlertDialogAction>
                          </AlertDialogFooter>
                        </AlertDialogContent>
                      </AlertDialog>
                    </div>
                  ) : null}
                  <Separator />
                  <div className="space-y-3">
                    <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                      Lesson details
                    </h2>
                    <div className="space-y-1.5">
                      <Label htmlFor="authoring-title">Title</Label>
                      <Input
                        id="authoring-title"
                        value={payload.title}
                        onChange={(event) => handleTitle(event.target.value)}
                      />
                    </div>
                    <div className="space-y-1.5">
                      <Label htmlFor="authoring-slug">URL slug</Label>
                      <Input
                        id="authoring-slug"
                        value={payload.slug}
                        onChange={(event) =>
                          updatePayload("slug", slugify(event.target.value))
                        }
                        onBlur={() =>
                          updatePayload("slug", normalizeSlug(payload.slug))
                        }
                      />
                      <p className="break-all text-xs text-muted-foreground">
                        {learningBase}/courses/{courseSlug}/content/
                        {payload.slug}
                      </p>
                    </div>
                    <div className="space-y-1.5">
                      <Label htmlFor="authoring-description">Description</Label>
                      <Textarea
                        id="authoring-description"
                        rows={3}
                        value={payload.description ?? ""}
                        onChange={(event) =>
                          updatePayload("description", event.target.value)
                        }
                      />
                    </div>
                    <div className="space-y-1.5">
                      <Label htmlFor="authoring-minutes">
                        Estimated minutes
                      </Label>
                      <div className="relative">
                        <Clock3 className="absolute left-2.5 top-2.5 size-3.5 text-muted-foreground" />
                        <Input
                          id="authoring-minutes"
                          type="number"
                          min={0}
                          className="pl-8"
                          value={payload.estimatedMinutes ?? ""}
                          onChange={(event) => {
                            const value = event.target.value;
                            updatePayload(
                              "estimatedMinutes",
                              value ? Number(value) : null,
                            );
                            updatePayload(
                              "estimatedMinutesSource",
                              value ? "Manual" : "Auto",
                            );
                          }}
                          placeholder="Auto"
                        />
                      </div>
                    </div>
                  </div>
                  <Separator />
                  <div className="space-y-2 text-sm text-muted-foreground">
                    <div className="flex justify-between">
                      <span>Format</span>
                      <span className="text-foreground">{formatLabel}</span>
                    </div>
                    <div className="flex justify-between">
                      <span>Draft revision</span>
                      <span className="text-foreground">{draft.revision}</span>
                    </div>
                    <div className="flex justify-between">
                      <span>Published version</span>
                      <span className="text-foreground">
                        {draft.basePublishedVersion}
                      </span>
                    </div>
                    <div className="flex justify-between">
                      <span>Last saved</span>
                      <AuthoringLocalTime
                        className="text-foreground"
                        value={draft.lastEditedAt}
                      />
                    </div>
                  </div>
                </div>
              </ScrollArea>
            ) : (
              <div className="flex min-h-0 flex-1 flex-col">
                <div className="border-b px-4 py-3">
                  <div className="flex items-center justify-between gap-2">
                    <div>
                      <p className="text-sm font-medium">
                        AI authoring copilot
                      </p>
                      <p className="text-sm text-muted-foreground">
                        Changes always require your approval.
                      </p>
                    </div>
                    <Badge variant="outline" className="font-mono text-sm">
                      {entitlement
                        ? `${entitlement.availableSoftCredits} SC`
                        : "… SC"}
                    </Badge>
                  </div>
                </div>
                <ScrollArea className="min-h-0 flex-1">
                  <div className="space-y-3 p-3">
                    {messages.length === 0 ? (
                      <div className="py-5 text-center">
                        <Sparkles className="mx-auto size-5 text-primary" />
                        <p className="mt-2 text-sm font-medium">
                          Improve this lesson
                        </p>
                        <p className="mx-auto mt-1 max-w-56 text-sm text-muted-foreground">
                          Ask for a rewrite, explanation, example, code sample,
                          summary, or quiz.
                        </p>
                        <div className="mt-4 grid gap-1.5">
                          {[
                            "Make this clearer and more concise",
                            "Add a practical example",
                            "Create a short knowledge check",
                          ].map((suggestion) => (
                            <Button
                              key={suggestion}
                              variant="outline"
                              size="sm"
                              className="h-auto justify-start py-2 text-left text-sm"
                              onClick={() => void runCopilot(suggestion)}
                            >
                              {suggestion}
                            </Button>
                          ))}
                        </div>
                      </div>
                    ) : null}
                    {messages.map((message) => (
                      <div
                        key={message.id}
                        className={
                          message.role === "user"
                            ? "ml-7 rounded-lg bg-primary px-3 py-2 text-sm text-primary-foreground"
                            : "mr-3 rounded-lg bg-muted px-3 py-2 text-sm leading-relaxed"
                        }
                      >
                        {message.content}
                      </div>
                    ))}
                    {isRunning ? (
                      <div className="flex items-center justify-between gap-3 px-2 py-1 text-sm text-muted-foreground">
                        <span className="flex items-center gap-2">
                          <Loader2 className="size-3 animate-spin" />
                          {activeRun?.errorCode === "AI_CANCEL_REQUESTED"
                            ? "Stopping Copilot…"
                            : "Generating proposal…"}
                        </span>
                        <Button
                          variant="ghost"
                          size="xs"
                          disabled={
                            activeRun?.errorCode === "AI_CANCEL_REQUESTED"
                          }
                          onClick={() => void stopCopilot()}
                        >
                          Stop
                        </Button>
                      </div>
                    ) : null}
                    {aiError ? (
                      <p className="rounded-md bg-destructive/10 p-2 text-sm text-destructive">
                        {aiError}
                      </p>
                    ) : null}
                    {activeRun?.usage.maximumEstimatedCost ? (
                      <div className="rounded-md bg-muted/50 p-2 text-sm text-muted-foreground">
                        Max {activeRun.usage.maximumEstimatedCost} SC · Used{" "}
                        {activeRun.usage.inputTokens +
                          activeRun.usage.outputTokens}{" "}
                        tokens · Settled {activeRun.usage.settledCost} SC
                      </div>
                    ) : null}
                  </div>
                </ScrollArea>
                <div className="space-y-2 border-t p-3">
                  {!isStructured &&
                  format !== "Video" &&
                  format !== "ExternalLink" ? (
                    <Select
                      items={[
                        { value: "ReplaceDocument", label: "Replace document" },
                        { value: "InsertAtCursor", label: "Insert at cursor" },
                      ]}
                      value={proposalMode}
                      onValueChange={(value) => setProposalMode(value as AiProposalKind)}
                    >
                      <SelectTrigger
                        aria-label="Proposal application"
                        className="h-8 w-full text-sm"
                      >
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectGroup>
                          <SelectItem value="ReplaceDocument">Replace document</SelectItem>
                          <SelectItem value="InsertAtCursor">Insert at cursor</SelectItem>
                        </SelectGroup>
                      </SelectContent>
                    </Select>
                  ) : null}
                  {format === "Video" || format === "ExternalLink" ? (
                    <p className="rounded-md bg-muted/50 px-2.5 py-2 text-sm text-muted-foreground">
                      {format === "Video"
                        ? "Video media is protected. Copilot can only propose metadata changes."
                        : "External resources are protected. Copilot can only propose metadata changes."}
                    </p>
                  ) : null}
                  <div className="relative">
                    <Textarea
                      aria-label="Ask Copilot"
                      value={copilotPrompt}
                      onChange={(event) => setCopilotPrompt(event.target.value)}
                      onKeyDown={(event) => {
                        if (event.key === "Enter" && !event.shiftKey) {
                          event.preventDefault();
                          void runCopilot();
                        }
                      }}
                      rows={3}
                      className="resize-none pr-10 text-sm"
                      placeholder="Ask AI to revise this lesson…"
                    />
                    <Button
                      size="icon-sm"
                      className="absolute bottom-2 right-2"
                      aria-label="Send to Copilot"
                      disabled={!copilotPrompt.trim() || isRunning}
                      onClick={() => void runCopilot()}
                    >
                      {isRunning ? (
                        <Loader2 className="animate-spin" />
                      ) : (
                        <Send />
                      )}
                    </Button>
                  </div>
                  <p className="text-sm text-muted-foreground">
                    Maximum cost is reserved first. Only actual token usage is
                    charged to your wallet.
                  </p>
                </div>
              </div>
            )}
          </aside>
        ) : null}
      </main>

      <Dialog open={diffOpen} onOpenChange={setDiffOpen}>
        <DialogContent className="grid h-[88dvh] max-w-[94vw] grid-rows-[auto_minmax(0,1fr)_auto] gap-0 overflow-hidden p-0 sm:max-w-[94vw]">
          <DialogHeader className="border-b px-5 py-4">
            <div className="flex items-start justify-between gap-4 pr-10">
              <div>
                <DialogTitle>Review AI proposal</DialogTitle>
                <DialogDescription>
                  Compare the current draft with the proposed content before
                  applying it.
                </DialogDescription>
              </div>
              <div className="flex rounded-md border p-0.5">
                <Button
                  variant={!unifiedDiff ? "secondary" : "ghost"}
                  size="xs"
                  onClick={() => setUnifiedDiff(false)}
                >
                  <ChevronsRight /> Side by side
                </Button>
                <Button
                  variant={unifiedDiff ? "secondary" : "ghost"}
                  size="xs"
                  onClick={() => setUnifiedDiff(true)}
                >
                  <ChevronsLeft /> Unified
                </Button>
              </div>
            </div>
          </DialogHeader>
          <div className="min-h-0 bg-background">
            {proposal ? (
              <Suspense
                fallback={
                  <div className="flex h-full items-center justify-center">
                    <Loader2 className="animate-spin" />
                  </div>
                }
              >
                <MonacoDiffEditor
                  original={proposal.originalContent}
                  modified={proposal.proposedContent}
                  language={format === "Html" ? "html" : "markdown"}
                  theme={resolvedTheme === "light" ? "vs-light" : "vs-dark"}
                  height="100%"
                  options={{
                    readOnly: true,
                    renderSideBySide: !unifiedDiff,
                    minimap: { enabled: false },
                    wordWrap: "on",
                  }}
                />
              </Suspense>
            ) : null}
          </div>
          <DialogFooter className="border-t px-5 py-3">
            <div className="mr-auto text-sm text-muted-foreground">
              Base draft revision {proposal?.baseDraftRevision}
            </div>
            {proposal ? (
              <>
                <Button
                  variant="outline"
                  onClick={() => void rejectProposal(proposal)}
                >
                  Discard
                </Button>
                <Button onClick={() => void acceptProposal(proposal)}>
                  <Check /> Accept and apply
                </Button>
              </>
            ) : null}
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <AssetsProvider scope={assetScope}>
        <AssetPickerDialog
          open={assetPickerOpen}
          onOpenChange={setAssetPickerOpen}
          onSelect={handleMediaSelected}
          title="Insert media"
          accept="image/*"
          kinds={["image"]}
        />
      </AssetsProvider>
    </div>
  );
}
