import "@testing-library/jest-dom/vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  push: vi.fn(),
  refresh: vi.fn(),
  saveDraft: vi.fn(),
  publishDraft: vi.fn(),
  getEntitlement: vi.fn(),
  getConversations: vi.fn(),
  createRun: vi.fn(),
  getRun: vi.fn(),
  applyProposal: vi.fn(),
  discardProposal: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: mocks.push, refresh: mocks.refresh }),
  useParams: () => ({ locale: "en-US" }),
}));
vi.mock("@/lib/learning/use-learning-base", () => ({
  useLearningBase: () => "/workspace/learning",
}));
vi.mock("@/lib/learning/authoring", () => ({
  saveAuthoringDraft: mocks.saveDraft,
  publishAuthoringDraft: mocks.publishDraft,
  getAiEntitlement: mocks.getEntitlement,
  getAiConversations: mocks.getConversations,
  createAiAuthoringRun: mocks.createRun,
  getAiAuthoringRun: mocks.getRun,
  applyAiProposal: mocks.applyProposal,
  discardAiProposal: mocks.discardProposal,
}));
vi.mock("@/components/learning/learner-lesson-renderer", () => ({
  LearnerLessonRenderer: ({ content }: { content: string }) => (
    <div data-testid="learner-renderer">{content}</div>
  ),
}));
vi.mock("@/components/learning/console/courses/[course]/content/[contentId]/lesson-code-editor", () => ({
  LessonCodeEditor: ({ initialValue, onChange }: { initialValue: string; onChange: (value: string) => void }) => (
    <textarea aria-label="Lesson body" defaultValue={initialValue} onChange={(event) => onChange(event.target.value)} />
  ),
}));
vi.mock("@/components/learning/console/courses/[course]/content/[contentId]/lesson-content-editor", () => ({
  LessonContentEditor: () => <div>Lexical editor</div>,
}));
vi.mock("@/components/learning/console/courses/[course]/content/[contentId]/lesson-video-editor", () => ({
  LessonVideoEditor: () => <div>Video editor</div>,
}));
vi.mock("@/components/learning/console/courses/[course]/content/[contentId]/quiz-content-editor", () => ({
  QuizContentEditor: () => <div>Quiz editor</div>,
}));
vi.mock("@monaco-editor/react", () => ({ DiffEditor: () => <div>Diff editor</div> }));

import { LessonAuthoringWorkspace } from "./lesson-authoring-workspace";

const initialDraft = {
  id: "draft-1",
  programId: "course-1",
  contentId: "lesson-1",
  payload: {
    title: "Original lesson",
    slug: "original-lesson",
    description: "Description",
    type: "Lesson",
    body: "Original body",
    jsonBody: null,
    lessonFormat: "Markdown",
    activitySettings: null,
    isRequired: true,
    estimatedMinutes: 5,
    estimatedMinutesSource: "Manual",
    visibility: "Public",
  },
  basePublishedVersion: 3,
  revision: 1,
  eTag: '"draft-1-1"',
  lastEditedBy: "author-1",
  lastEditedAt: "2026-09-10T12:00:00Z",
} as const;

const item = {
  id: "lesson-1",
  title: "Original lesson",
  slug: "original-lesson",
  type: "Lesson",
  status: "draft",
  order: 1,
} as never;

describe("LessonAuthoringWorkspace", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.clearAllMocks();
    window.sessionStorage.clear();
    Object.defineProperty(window, "matchMedia", {
      configurable: true,
      value: vi.fn().mockReturnValue({ matches: true }),
    });
    mocks.saveDraft.mockImplementation(async (_courseId, _contentId, revision, payload) => ({
      success: true,
      data: { ...initialDraft, payload, revision: revision + 1 },
    }));
    mocks.publishDraft.mockImplementation(async (_courseId, _contentId, revision) => ({
      success: true,
      data: { draft: { ...initialDraft, revision: revision + 1, basePublishedVersion: 4 } },
    }));
    mocks.getEntitlement.mockResolvedValue({
      success: true,
      data: { availableSoftCredits: 100, reservedSoftCredits: 0, settledSoftCredits: 0, currency: "SoftCoin" },
    });
    mocks.getConversations.mockResolvedValue({ success: true, data: [] });
  });

  afterEach(() => {
    cleanup();
    vi.useRealTimers();
  });

  function renderWorkspace(draft = initialDraft) {
    return render(
      <LessonAuthoringWorkspace
        courseId="course-1"
        courseSlug="course-slug"
        courseTitle="Course title"
        item={item}
        curriculum={[item]}
        initialDraft={draft as never}
      />,
    );
  }

  it("autosaves edits with the current draft revision", async () => {
    renderWorkspace();

    fireEvent.change(screen.getByRole("textbox", { name: "Lesson title" }), {
      target: { value: "Updated lesson" },
    });
    await act(async () => vi.advanceTimersByTimeAsync(1200));

    expect(mocks.saveDraft).toHaveBeenCalledWith(
      "course-1",
      "lesson-1",
      1,
      expect.objectContaining({ title: "Updated lesson", slug: "updated-lesson" }),
    );
    expect(screen.getByText("Saved")).toBeInTheDocument();
  });

  it("uses the learner renderer for preview and publishes the saved revision", async () => {
    renderWorkspace();

    fireEvent.click(screen.getByRole("button", { name: /preview/i }));
    expect(screen.getByTestId("learner-renderer")).toHaveTextContent("Original body");
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: /publish changes/i }));
      await Promise.resolve();
    });

    expect(mocks.publishDraft).toHaveBeenCalledWith("course-1", "lesson-1", 1);
    expect(mocks.refresh).toHaveBeenCalled();
  });

  it("opens the Copilot with the authenticated user's SoftCoin balance", async () => {
    renderWorkspace();

    fireEvent.click(screen.getAllByRole("button", { name: /copilot/i })[0]!);
    await act(async () => {
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(mocks.getEntitlement).toHaveBeenCalledWith("course-1", "lesson-1");
    expect(screen.getByText("100 SC")).toBeInTheDocument();
    expect(screen.getByText(/only actual token usage is charged/i)).toBeInTheDocument();
  });

  it("limits video lessons to metadata-only Copilot proposals", async () => {
    const videoDraft = {
      ...initialDraft,
      payload: {
        ...initialDraft.payload,
        body: "https://cdn.example.test/original.mp4",
        lessonFormat: "Video",
      },
    } as const;
    mocks.createRun.mockResolvedValue({
      success: false,
      error: "Stopped after request capture",
      status: 400,
    });
    renderWorkspace(videoDraft);

    fireEvent.click(screen.getAllByRole("button", { name: /copilot/i })[0]!);
    fireEvent.change(screen.getByRole("textbox", { name: "Ask Copilot" }), {
      target: { value: "Improve the video description" },
    });
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Send to Copilot" }));
      await Promise.resolve();
    });

    expect(screen.queryByText("Replace document")).not.toBeInTheDocument();
    expect(screen.getByText(/video media is protected/i)).toBeInTheDocument();
    expect(mocks.createRun).toHaveBeenCalledWith(
      "course-1",
      "lesson-1",
      expect.objectContaining({ proposalKind: "MetadataPatch" }),
    );
  });
});
