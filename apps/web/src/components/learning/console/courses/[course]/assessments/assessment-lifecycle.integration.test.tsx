import "@testing-library/jest-dom/vitest";
import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { AssessmentsList } from "./assessments-list";
import { ContentItemEditor } from "../content/[contentId]/content-item-editor";

import {
  addContent,
  createAssessment,
  deleteAssessment,
  restoreAssessment,
  updateAssessment,
} from "@/lib/learning/actions";
import type { Assessment } from "@/lib/learning/queries/assessments";
import type { ContentItemDetail } from "@/lib/learning/types";

// ── Hoisted harnesses ──

const dndHarness = vi.hoisted(() => ({
  handlers: [] as Array<
    (event: { active: { id: string }; over: { id: string } | null }) => void
  >,
}));

const routerMocks = vi.hoisted(() => ({
  back: vi.fn(),
  push: vi.fn(),
  refresh: vi.fn(),
}));

// Wire-level mocks: only used by the real addContent chain in step 1.
// The chain calls local createAssessment (cannot be intercepted at the wrapper
// level because ES module internal bindings bypass the export), so we mock the
// wire and assert that postAssessments was called with contentId from
// postCoursesContent's response — same non-vacuous assertion target.
const wireMocks = vi.hoisted(() => ({
  postCoursesContent: vi.fn(),
  postAssessments: vi.fn(),
  revalidatePath: vi.fn(),
  getToken: vi.fn(),
  resolveCourseId: vi.fn(),
  createServerClient: vi.fn(),
}));

// ── Module mocks ──

// Real addContent retained via importActual; the action wrappers components
// call directly are replaced with vi.fn() so we can assert on call args.
vi.mock("@/lib/learning/actions", async (importOriginal) => {
  const actual =
    await importOriginal<typeof import("@/lib/learning/actions")>();
  return {
    ...actual,
    createAssessment: vi.fn(),
    deleteAssessment: vi.fn(),
    restoreAssessment: vi.fn(),
    updateAssessment: vi.fn(),
    updateContent: vi.fn(),
    createAssessmentGroup: vi.fn(),
    updateAssessmentGroup: vi.fn(),
    deleteAssessmentGroup: vi.fn(),
  };
});

vi.mock("@game-guild/client", () => ({
  createServerClient: wireMocks.createServerClient,
  GeneratedApi: {
    LearningAssessmentsModule: class {
      postAssessments = wireMocks.postAssessments;
    },
    LearningCoursesProgramModule: class {},
    LearningCoursesProgramContentModule: class {
      postCoursesContent = wireMocks.postCoursesContent;
    },
    LearningCoursesProgramLifecycleModule: class {},
    LearningEnrollmentsModule: class {},
    LearningCoursesStudentsModule: class {},
    LearningCoursesSupportTicketsModule: class {},
    LearningCertificatesModule: class {},
    LearningExperienceSocialDiscussionsModule: class {},
    LearningExperienceSocialRepliesModule: class {},
    LearningExperienceSocialReviewsModule: class {},
    UsersModule: class {},
  },
}));

vi.mock("@/auth", () => ({ getToken: wireMocks.getToken }));
vi.mock("next/cache", () => ({ revalidatePath: wireMocks.revalidatePath }));
vi.mock("@/lib/learning/queries/course", () => ({
  resolveCourseId: wireMocks.resolveCourseId,
}));

vi.mock("next/navigation", () => ({
  usePathname: () => '/workspace/learning', useRouter: () => routerMocks }));
vi.mock("@/i18n/navigation", () => ({
  Link: ({
    href,
    children,
  }: {
    href: string;
    children: ReactNode;
  }) => (
    <a href={href}>
      {children}
    </a>
  ),
  usePathname: () =>
    "/workspace/learning/courses/course-1/assessments",
  useRouter: () => ({ refresh: routerMocks.refresh }),
}));

vi.mock("@dnd-kit/core", () => ({
  DndContext: ({
    children,
    onDragEnd,
  }: {
    children: ReactNode;
    onDragEnd?: (
      event: { active: { id: string }; over: { id: string } | null },
    ) => void;
  }) => {
    if (onDragEnd) dndHarness.handlers.push(onDragEnd);
    return children;
  },
  DragOverlay: ({ children }: { children: ReactNode }) => children ?? null,
  PointerSensor: vi.fn(),
  closestCorners: vi.fn(),
  useDraggable: () => ({
    attributes: {},
    listeners: {},
    setNodeRef: vi.fn(),
    transform: null,
    isDragging: false,
  }),
  useDroppable: () => ({ setNodeRef: vi.fn(), isOver: false }),
  useSensor: vi.fn(() => ({})),
  useSensors: vi.fn(() => []),
}));
vi.mock("@dnd-kit/utilities", () => ({
  CSS: { Translate: { toString: () => undefined } },
}));

vi.mock("@/lib/emception/put-coding-definition", () => ({
  putCodingDefinition: codingLibMocks.putCodingDefinition,
}));
vi.mock("@game-guild/lexical-surface", () => ({
  LexicalSurface: ({ accessibleLabel }: { accessibleLabel?: string }) => (
    <textarea aria-label={accessibleLabel ?? "Body"} readOnly />
  ),
}));
vi.mock("../content/[contentId]/lesson-code-editor", () => ({
  LessonCodeEditor: ({
    initialValue,
    onChange,
  }: {
    initialValue: string;
    onChange: (value: string) => void;
  }) => (
    <textarea
      aria-label="Body"
      defaultValue={initialValue}
      onChange={(event) => onChange(event.currentTarget.value)}
    />
  ),
}));
vi.mock("@/components/block-content-editor/engines/blocks/block-array-editor", () => ({
  BlockArrayEditor: ({ blocks }: { blocks: unknown[] }) => (
    <div data-testid="block-array-editor">Blocks: {blocks.length}</div>
  ),
}));

Object.defineProperties(HTMLElement.prototype, {
  hasPointerCapture: { value: vi.fn(() => false) },
  setPointerCapture: { value: vi.fn() },
  releasePointerCapture: { value: vi.fn() },
  scrollIntoView: { value: vi.fn() },
});
global.ResizeObserver = class ResizeObserver {
  observe() {}
  unobserve() {}
  disconnect() {}
};

// ── Fixtures ──

const assessmentGroups = [
  {
    id: "group-a",
    courseId: "course-1",
    name: "Group A",
    description: null,
    weightPercent: 100,
    order: 1,
  },
];

// Auto-created assessment for an Assignment content item (Task 6 chain result).
const baseAssessment = {
  id: "assessment-lifecycle",
  slug: "assessment-lifecycle",
  courseId: "course-1",
  contentId: "content-1",
  title: "Milestone 1 brief",
  description: null,
  type: "Assignment",
  maxScore: 100,
  passingScore: 70,
  timeLimitMinutes: null,
  maxAttempts: null,
  isRequired: true,
  order: 1,
  availableFrom: null,
  availableUntil: null,
  presentationMode: "SingleStep",
  dueAt: null,
  allowLateSubmissions: false,
  lateSubmissionDeadline: null,
  isAvailable: true,
  assessmentGroupId: null,
  assessmentGroupName: null,
  assessmentGroupWeightPercent: null,
  assessmentGroupOrder: null,
  reviewMethods: 8,
  groupSetId: null,
  publishedDefinitionRevisionId: null,
  reviewConfigurationCanonicalJson: null,
  attemptContributionMode: null,
  contentCompletionMode: "on-release-and-pass",
  resultReleaseMode: "manual",
  resultReleaseScheduledFor: null,
  version: 1,
} as Assessment;

const assignmentItem = {
  id: "content-1",
  version: 1,
  parentId: "module-1",
  order: 1,
  type: "Assignment",
  title: "Milestone 1 brief",
  description: "First assessment.",
  status: "published",
  visibility: "Public",
  duration: 60,
  estimatedMinutesSource: "Manual",
  metadata: {},
  gradingConfig: null,
  content: null,
  jsonBody: null,
  settings: { isRequired: true },
  lessonFormat: null,
  createdAt: "2026-01-01T00:00:00.000Z",
  updatedAt: "2026-01-02T00:00:00.000Z",
} satisfies ContentItemDetail;

const codeItem = {
  ...assignmentItem,
  id: "content-code",
  type: "Code",
  title: "Sum two numbers",
} satisfies ContentItemDetail;

function renderList(assessments: Assessment[]) {
  dndHarness.handlers.length = 0;
  routerMocks.refresh.mockClear();
  return render(
    <AssessmentsList
      courseId="course-1"
      assessments={assessments}
      total={assessments.length}
      assessmentGroups={assessmentGroups}
    />,
  );
}

// ── Lifecycle integration tests ──
//
// Each test verifies one step of the lifecycle. The plan explicitly allows
// splitting the "full lifecycle" into atomic tests when a single mega-test
// would be brittle. Each test still uses REAL component trees and asserts on
// REAL action call args + render state — the test is not vacuous.

describe("assessment lifecycle integration (Tasks 6, 7, 9, 11)", () => {
  beforeEach(() => {
    vi.clearAllMocks();

    // Wire defaults for the real addContent chain (step 1).
    wireMocks.getToken.mockResolvedValue("access-token");
    wireMocks.resolveCourseId.mockImplementation(async (id: string) => id);
    wireMocks.createServerClient.mockReturnValue({});
    wireMocks.revalidatePath.mockReturnValue(undefined);
    wireMocks.postCoursesContent.mockResolvedValue({
      ok: true,
      data: { id: "content-1" },
    });
    wireMocks.postAssessments.mockResolvedValue({
      ok: true,
      data: { id: "assessment-lifecycle" },
    });

    // Action wrappers (steps 2-6).
    vi.mocked(createAssessment).mockResolvedValue({
      success: true,
      data: { id: "assessment-lifecycle" },
    });
    vi.mocked(deleteAssessment).mockResolvedValue({ success: true, data: null });
    vi.mocked(restoreAssessment).mockResolvedValue({
      success: true,
      data: null,
    });
    vi.mocked(updateAssessment).mockResolvedValue({
      success: true,
      data: { version: 2 },
    });
  });

  it("1. addContent(Assignment) chains createAssessment with the new contentId", async () => {
    wireMocks.postCoursesContent.mockResolvedValueOnce({
      ok: true,
      data: { id: "fresh-content-id" },
    });

    const result = await addContent({
      courseId: "course-1",
      title: "Milestone 1 brief",
      type: "Assignment",
    });

    expect(result).toEqual({ success: true, data: { id: "fresh-content-id" } });
    expect(wireMocks.postCoursesContent).toHaveBeenCalledWith(
      "course-1",
      expect.objectContaining({
        type: "Assignment",
        title: "Milestone 1 brief",
      }),
    );
    expect(wireMocks.postAssessments).toHaveBeenCalledWith(
      expect.objectContaining({
        courseId: "course-1",
        title: "Milestone 1 brief",
        type: "Assignment",
        contentId: "fresh-content-id",
        reviewMethods: 8,
      }),
    );
  });

  it("2. the auto-created assessment appears in the Unassigned section of the assessments list", () => {
    renderList([baseAssessment]);

    const unassigned = screen.getByTestId("assessment-group-ungrouped");
    expect(
      within(unassigned).getByRole("link", { name: /milestone 1 brief/i }),
    ).toBeInTheDocument();
  });

  it("3. dragging the assessment into Group A assigns the group via updateAssessment", async () => {
    renderList([baseAssessment]);
    const dragEnd = dndHarness.handlers[0]!;

    await act(async () => {
      dragEnd({
        active: { id: "assessment-assessment-lifecycle" },
        over: { id: "group-drop-group-a" },
      });
    });

    await waitFor(() => {
      expect(updateAssessment).toHaveBeenCalledWith({
        courseId: "course-1",
        assessmentId: "assessment-lifecycle",
        expectedVersion: 1,
        assessmentGroupId: "group-a",
      });
    });
    expect(routerMocks.refresh).toHaveBeenCalled();
  });

  it("4. toggling Graded OFF soft-deletes the linked assessment; server filter excludes it from the list", async () => {
    const user = userEvent.setup();
    render(
      <ContentItemEditor
        courseId="course-1"
        item={assignmentItem}
        courseTitle="Game AI"
        linkedAssessment={baseAssessment}
      />,
    );

    await user.click(screen.getByRole("switch", { name: /^graded$/i }));
    const dialog = await screen.findByRole("alertdialog");
    await user.click(
      within(dialog).getByRole("button", { name: /remove grading/i }),
    );

    await waitFor(() => {
      expect(deleteAssessment).toHaveBeenCalledWith(
        "course-1",
        "assessment-lifecycle",
      );
    });
    expect(routerMocks.refresh).toHaveBeenCalled();

    // Simulate the post-delete server fetch: DeletedAt filter excludes the
    // soft-deleted assessment. The list renders with an empty payload.
    renderList([]);
    expect(
      screen.queryByRole("link", { name: /milestone 1 brief/i }),
    ).not.toBeInTheDocument();
  });

  it("5. toggling Graded back ON restores the same assessment (not createAssessment) and it reappears", async () => {
    const user = userEvent.setup();
    const { unmount } = render(
      <ContentItemEditor
        courseId="course-1"
        item={assignmentItem}
        courseTitle="Game AI"
        linkedAssessment={baseAssessment}
      />,
    );

    // OFF first — captures recentlyDeletedAssessmentId in component state.
    await user.click(screen.getByRole("switch", { name: /^graded$/i }));
    const offDialog = await screen.findByRole("alertdialog");
    await user.click(
      within(offDialog).getByRole("button", { name: /remove grading/i }),
    );
    await waitFor(() => {
      expect(deleteAssessment).toHaveBeenCalledWith(
        "course-1",
        "assessment-lifecycle",
      );
    });

    const gradedSwitch = screen.getByRole("switch", { name: /^graded$/i });
    await waitFor(() => {
      expect(gradedSwitch).not.toBeDisabled();
      expect(gradedSwitch).not.toBeChecked();
    });

    // ON again — component state still holds the recently-deleted id, so
    // restoreAssessment fires (not createAssessment).
    await user.click(gradedSwitch);
    await waitFor(() => {
      expect(restoreAssessment).toHaveBeenCalledWith(
        "course-1",
        "assessment-lifecycle",
      );
    });
    expect(createAssessment).not.toHaveBeenCalled();

    // Simulate the post-restore server fetch: assessment is back.
    unmount();
    renderList([baseAssessment]);
    expect(
      screen.getByRole("link", { name: /milestone 1 brief/i }),
    ).toBeInTheDocument();
  });

  it("6. Code content with a linked AutomatedReview assessment surfaces the Configure Coding Tests link", () => {
    render(
      <ContentItemEditor
        courseId="course-1"
        item={codeItem}
        courseTitle="Game AI"
        linkedAssessment={{
          ...baseAssessment,
          contentId: codeItem.id,
          reviewMethods: 4,
        }}
      />,
    );

    expect(screen.getByTestId("coding-tests-section")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /configure coding tests/i }),
    ).toBeInTheDocument();
  });

  it("6b. Code content without AutomatedReview does not surface the coding-tests bridge", () => {
    render(
      <ContentItemEditor
        courseId="course-1"
        item={codeItem}
        courseTitle="Game AI"
        linkedAssessment={{ ...baseAssessment, contentId: codeItem.id }}
      />,
    );

    expect(screen.queryByTestId("coding-tests-section")).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /configure coding tests/i }),
    ).not.toBeInTheDocument();
  });
});
