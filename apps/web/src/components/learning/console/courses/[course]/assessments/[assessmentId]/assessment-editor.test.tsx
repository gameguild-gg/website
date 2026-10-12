import "@testing-library/jest-dom/vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type React from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AssessmentEditor } from "./assessment-editor";
import { deleteAssessment, updateAssessment, prepareAssessmentRevision, publishAssessmentRevision } from "@/lib/learning/actions";
import type {
  Assessment,
  AssessmentGroup,
} from "@/lib/learning/queries/assessments";
import type { CourseContentItemViewModel } from "@/lib/learning/queries/course";
import { createReviewMethods } from "@game-guild/grading";

const routerMocks = vi.hoisted(() => ({
  back: vi.fn(),
  push: vi.fn(),
  refresh: vi.fn(),
  replace: vi.fn(),
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

vi.mock("next/navigation", () => ({
  usePathname: () => "/workspace/learning",
  useRouter: () => routerMocks,
}));

vi.mock("@/i18n/navigation", () => ({
  Link: ({
    href,
    children,
    ...props
  }: {
    href: string;
    children: React.ReactNode;
  }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock("@/lib/learning/actions", () => ({
  updateAssessment: vi.fn(),
  deleteAssessment: vi.fn(),
  prepareAssessmentRevision: vi.fn(),
  publishAssessmentRevision: vi.fn(),
}));

const assessment = {
  id: "assessment-1",
  slug: "assessment-1",
  courseId: "course-1",
  contentId: null,
  assessmentGroupId: "group-quizzes",
  assessmentGroupName: "Weekly quizzes",
  assessmentGroupWeightPercent: 30,
  assessmentGroupOrder: 1,
  title: "Schema Patterns Quiz",
  description: "Check understanding of schema patterns.",
  type: "Quiz",
  maxScore: 10,
  passingScore: 7,
  timeLimitMinutes: 30,
  maxAttempts: 1,
  isRequired: true,
  order: 1,
  availableFrom: "2026-07-01T10:00:00.000Z",
  availableUntil: "2026-07-05T10:00:00.000Z",
  presentationMode: "Continuous",
  dueAt: null,
  allowLateSubmissions: false,
  lateSubmissionDeadline: null,
  isAvailable: true,
  reviewMethods: createReviewMethods("InstructorReview"),
  groupSetId: null,
  publishedDefinitionRevisionId: null,
  reviewConfigurationCanonicalJson: null,
  attemptContributionMode: null,
  contentCompletionMode: "on-release-and-pass",
  resultReleaseMode: "manual",
  resultReleaseScheduledFor: null,
  version: 1,
} satisfies Assessment;

const groups = [
  {
    id: "group-quizzes",
    courseId: "course-1",
    name: "Weekly quizzes",
    description: null,
    weightPercent: 30,
    order: 1,
  },
] satisfies AssessmentGroup[];

const courseContent = [
  {
    id: "content-assignment-1",
    parentId: null,
    order: 0,
    type: "Assignment",
    title: "Module 1 Homework",
    description: null,
    status: "published",
    visibility: "Public",
    duration: null,
    metadata: {},
    gradingConfig: null,
    createdAt: "2026-07-01T00:00:00.000Z",
    updatedAt: "2026-07-01T00:00:00.000Z",
  },
  {
    id: "content-lesson-1",
    parentId: null,
    order: 1,
    type: "Lesson",
    title: "Intro Lesson",
    description: null,
    status: "published",
    visibility: "Public",
    duration: null,
    metadata: {},
    gradingConfig: null,
    createdAt: "2026-07-01T00:00:00.000Z",
    updatedAt: "2026-07-01T00:00:00.000Z",
  },
] satisfies CourseContentItemViewModel[];

describe("AssessmentEditor", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(updateAssessment).mockResolvedValue({
      success: true,
      data: { version: 2 },
    });
    vi.mocked(deleteAssessment).mockResolvedValue({
      success: true,
      data: null,
    });
    vi.stubGlobal(
      "confirm",
      vi.fn(() => true),
    );
  });

  it("prepares and publishes linked Code through the existing revision controls", async () => {
    const user = userEvent.setup();
    vi.mocked(prepareAssessmentRevision).mockResolvedValue({ success: true, data: {
      revisionId: "code-revision", revisionNumber: 1, authoringSourceHash: "a".repeat(64), executionSnapshotHash: "b".repeat(64),
    } });
    vi.mocked(publishAssessmentRevision).mockResolvedValue({ success: true, data: {
      revisionId: "code-revision", revisionNumber: 1, authoringSourceHash: "a".repeat(64), executionSnapshotHash: "b".repeat(64),
    } });
    render(<AssessmentEditor courseId="course-1" assessment={{ ...assessment, type: "Assignment", contentId: "code-content" }}
      courseContent={[{ ...courseContent[0]!, id: "code-content", type: "Code" }]}
      authoringState={{ assessmentId: assessment.id, assessmentVersion: 1, lifecycle: "candidate",
        currentAuthoringSourceHash: "a".repeat(64), candidate: { revisionId: "code-revision", revisionNumber: 1,
          authoringSourceHash: "a".repeat(64), executionSnapshotHash: "b".repeat(64), createdAt: "2026-10-06T00:00:00Z" },
        published: null, candidateMatchesDraft: true, publishedMatchesDraft: false,
        prepare: { available: true, code: null, message: null }, publish: { available: true, code: null, message: null },
      }} canManage />);
    expect(screen.getByTestId("assessment-publication-state")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Prepare" }));
    await waitFor(() => expect(prepareAssessmentRevision).toHaveBeenCalledWith("course-1", "assessment-1", 1));
    await waitFor(() => expect(screen.getByRole("button", { name: "Publish" })).toBeEnabled());
    await user.click(screen.getByRole("button", { name: "Publish" }));
    await waitFor(() => expect(publishAssessmentRevision).toHaveBeenCalledWith("course-1", "assessment-1", "code-revision", 1));
  });

  it("validates the title before updating assessment settings", async () => {
    const user = userEvent.setup();
    render(
      <AssessmentEditor
        courseId="course-1"
        assessment={assessment}
        assessmentGroups={groups}
      />,
    );

    await user.clear(screen.getByLabelText(/^title$/i));
    await user.click(screen.getByRole("button", { name: /save changes/i }));

    expect(await screen.findByText("Title is required.")).toBeInTheDocument();
    expect(updateAssessment).not.toHaveBeenCalled();
  });

  it("saves details, scoring, availability, and weighted group assignment", async () => {
    const user = userEvent.setup();
    render(
      <AssessmentEditor
        courseId="course-1"
        assessment={assessment}
        assessmentGroups={groups}
      />,
    );

    await user.clear(screen.getByLabelText(/^title$/i));
    await user.type(screen.getByLabelText(/^title$/i), "Updated Quiz");
    fireEvent.change(screen.getByLabelText(/description/i), {
      target: { value: "Updated instructions." },
    });
    fireEvent.change(screen.getByLabelText(/max score/i), {
      target: { value: "20" },
    });
    fireEvent.change(screen.getByLabelText(/passing score/i), {
      target: { value: "14" },
    });
    fireEvent.change(screen.getByLabelText(/available from/i), {
      target: { value: "2026-08-01T10:00" },
    });
    fireEvent.change(screen.getByLabelText(/available until/i), {
      target: { value: "2026-08-05T10:00" },
    });
    fireEvent.change(screen.getByLabelText(/time limit/i), {
      target: { value: "45" },
    });
    await user.click(screen.getByRole("button", { name: /save changes/i }));

    await waitFor(() => {
      expect(updateAssessment).toHaveBeenCalledWith(
        expect.objectContaining({
          courseId: "course-1",
          assessmentId: "assessment-1",
          expectedVersion: 1,
          title: "Updated Quiz",
          slug: "updated-quiz",
          description: "Updated instructions.",
          maxScore: 20,
          passingScore: 14,
          timeLimitMinutes: 45,
          maxAttempts: 1,
          isRequired: true,
          availableFrom: "2026-08-01T10:00",
          availableUntil: "2026-08-05T10:00",
          assessmentGroupId: "group-quizzes",
          clearAssessmentGroupId: false,
          presentationMode: "Continuous",
          reviewMethods: 8,
          contentCompletionMode: "on-release-and-pass",
          resultReleaseMode: "manual",
        }),
      );
    });
    expect(routerMocks.replace).toHaveBeenCalledWith(
      "/workspace/learning/courses/course-1/assessments/updated-quiz",
    );
    expect(routerMocks.refresh).not.toHaveBeenCalled();
    expect(screen.getByText("Saved successfully.")).toBeInTheDocument();
  });

  it("regenerates the slug from title edits and detaches only after a direct slug edit", async () => {
    const user = userEvent.setup();
    render(
      <AssessmentEditor
        courseId="course-1"
        assessment={assessment}
        assessmentGroups={groups}
      />,
    );

    const slugInput = screen.getByLabelText(/url slug/i);
    expect(slugInput).toHaveValue("assessment-1");

    await user.clear(screen.getByLabelText(/^title$/i));
    await user.type(screen.getByLabelText(/^title$/i), "New Title");
    expect(slugInput).toHaveValue("new-title");

    await user.clear(slugInput);
    await user.type(slugInput, "Final EXAM v2!");
    expect(slugInput).toHaveValue("final-exam-v2");

    await user.clear(screen.getByLabelText(/^title$/i));
    await user.type(screen.getByLabelText(/^title$/i), "Another Title");
    expect(slugInput).toHaveValue("final-exam-v2");

    await user.click(screen.getByRole("button", { name: /save changes/i }));

    await waitFor(() => {
      expect(updateAssessment).toHaveBeenCalledWith(
        expect.objectContaining({
          title: "Another Title",
          slug: "final-exam-v2",
        }),
      );
    });
    expect(routerMocks.replace).toHaveBeenCalledWith(
      "/workspace/learning/courses/course-1/assessments/final-exam-v2",
    );
  });

  it("strips the slug trailing dash on blur", async () => {
    const user = userEvent.setup();
    render(
      <AssessmentEditor
        courseId="course-1"
        assessment={assessment}
        assessmentGroups={groups}
      />,
    );

    const slugInput = screen.getByLabelText(/url slug/i);
    await user.clear(slugInput);
    await user.type(slugInput, "final exam ");
    expect(slugInput).toHaveValue("final-exam-");

    fireEvent.blur(slugInput);
    expect(slugInput).toHaveValue("final-exam");
  });

  it("auto-syncs the slug from the title while it mirrors the sluggified title", async () => {
    const user = userEvent.setup();
    const mirrored = { ...assessment, slug: "schema-patterns-quiz" };
    render(
      <AssessmentEditor
        courseId="course-1"
        assessment={mirrored}
        assessmentGroups={groups}
      />,
    );

    const slugInput = screen.getByLabelText(/url slug/i);
    expect(slugInput).toHaveValue("schema-patterns-quiz");

    await user.clear(screen.getByLabelText(/^title$/i));
    await user.type(screen.getByLabelText(/^title$/i), "Updated Quiz");
    expect(slugInput).toHaveValue("updated-quiz");
  });

  it("shows API errors and deletes after explicit confirmation", async () => {
    const user = userEvent.setup();
    vi.mocked(updateAssessment).mockResolvedValueOnce({
      success: false,
      error: "Bad Request",
    });

    render(
      <AssessmentEditor
        courseId="course-1"
        assessment={assessment}
        assessmentGroups={groups}
      />,
    );

    await user.click(screen.getByRole("button", { name: /save changes/i }));
    expect(await screen.findByText("Bad Request")).toBeInTheDocument();

    await user.click(
      screen.getByRole("button", { name: /delete assessment/i }),
    );

    await waitFor(() => {
      expect(deleteAssessment).toHaveBeenCalledWith("course-1", "assessment-1");
    });
    expect(routerMocks.push).toHaveBeenCalledWith(
      "/workspace/learning/courses/course-1/assessments",
    );
    expect(routerMocks.back).not.toHaveBeenCalled();
  });

  it("shows a permanent link to the parent content when linked (no dropdown)", async () => {
    const linked = { ...assessment, contentId: "content-assignment-1" };
    render(
      <AssessmentEditor
        courseId="course-1"
        assessment={linked}
        assessmentGroups={groups}
        courseContent={courseContent}
      />,
    );

    expect(
      screen.queryByRole("combobox", { name: /linked content/i }),
    ).not.toBeInTheDocument();

    const contentLink = screen.getByTestId("linked-content-link");
    expect(contentLink).toHaveAttribute(
      "href",
      "/workspace/learning/courses/course-1/content/content-assignment-1",
    );
    expect(contentLink).toHaveTextContent("Module 1 Homework");
    expect(screen.getByText(/cannot\s+be\s+unlinked/i)).toBeInTheDocument();
  });

  it("shows standalone text when no content is linked", async () => {
    render(
      <AssessmentEditor
        courseId="course-1"
        assessment={assessment}
        assessmentGroups={groups}
        courseContent={courseContent}
      />,
    );

    expect(
      screen.queryByRole("combobox", { name: /linked content/i }),
    ).not.toBeInTheDocument();
    expect(screen.getByTestId("linked-content-none")).toBeInTheDocument();
  });

  it("persists a primary peer review followed by final instructor review", async () => {
    const user = userEvent.setup();
    render(
      <AssessmentEditor
        courseId="course-1"
        assessment={assessment}
        assessmentGroups={groups}
        courseContent={courseContent}
      />,
    );

    const primaryReview = screen.getByRole("combobox", {
      name: /primary review/i,
    });
    expect(primaryReview).toHaveTextContent("Instructor review");
    await user.click(primaryReview);
    await user.click(
      await screen.findByRole("option", { name: "Peer review" }),
    );
    await user.click(
      screen.getByRole("switch", { name: /final instructor review/i }),
    );
    await user.click(screen.getByRole("button", { name: /save changes/i }));

    await waitFor(() => {
      expect(updateAssessment).toHaveBeenCalledWith(
        expect.objectContaining({
          reviewMethods: 9,
        }),
      );
    });
    const configuration = JSON.parse(
      vi.mocked(updateAssessment).mock.calls.at(-1)![0]
        .reviewConfigurationCanonicalJson!,
    );
    expect(configuration.peer.reviewsRequiredPerSubmission).toBe(3);
    expect(configuration.instructor).toEqual({ requireOverrideReason: false });
  });

  it("removes final instructor review while preserving the primary review", async () => {
    const user = userEvent.setup();
    const multi = {
      ...assessment,
      reviewMethods: createReviewMethods("AutomatedReview", true),
    };
    render(
      <AssessmentEditor
        courseId="course-1"
        assessment={multi}
        assessmentGroups={groups}
        courseContent={courseContent}
      />,
    );

    expect(
      screen.getByRole("combobox", { name: /primary review/i }),
    ).toHaveTextContent("Automated review");
    await user.click(
      screen.getByRole("switch", { name: /final instructor review/i }),
    );
    await user.click(screen.getByRole("button", { name: /save changes/i }));

    await waitFor(() => {
      expect(updateAssessment).toHaveBeenCalledWith(
        expect.objectContaining({
          reviewMethods: 4,
        }),
      );
    });
  });

  it("keeps the type selector disabled for existing assessments", () => {
    render(
      <AssessmentEditor
        courseId="course-1"
        assessment={assessment}
        assessmentGroups={groups}
        courseContent={courseContent}
      />,
    );

    const typeTrigger = screen
      .getByText("Type cannot be changed after creation.")
      .closest("div")
      ?.querySelector('[role="combobox"]');
    expect(typeTrigger).toBeDisabled();
  });

  it("renders the grade submissions link in the header for instructors", () => {
    render(
      <AssessmentEditor
        courseId="course-1"
        assessment={assessment}
        assessmentGroups={groups}
        courseContent={courseContent}
        canManage
      />,
    );

    const gradeButton = screen.getByTestId("grade-submissions-button");
    expect(gradeButton).toHaveAttribute(
      "href",
      "/workspace/learning/courses/course-1/assessments/assessment-1/submissions",
    );
    expect(gradeButton).toHaveTextContent(/grade submissions/i);
  });

  it("renders the SpeedGrader link in the header for instructors", () => {
    render(
      <AssessmentEditor
        courseId="course-1"
        assessment={assessment}
        assessmentGroups={groups}
        courseContent={courseContent}
        canManage
      />,
    );

    const speedgraderButton = screen.getByTestId("start-speedgrader-button");
    expect(speedgraderButton).toHaveAttribute(
      "href",
      "/speedgrader/assessments/assessment-1?course=course-1",
    );
    expect(speedgraderButton).toHaveTextContent(/speedgrader/i);
  });

  it("hides the grade submissions link from non-instructors", () => {
    render(
      <AssessmentEditor
        courseId="course-1"
        assessment={assessment}
        assessmentGroups={groups}
        courseContent={courseContent}
      />,
    );

    expect(
      screen.queryByTestId("grade-submissions-button"),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByTestId("start-speedgrader-button"),
    ).not.toBeInTheDocument();
  });

  it("saves cleared optional fields, quiz presentation, and no grading group", async () => {
    const user = userEvent.setup();
    render(
      <AssessmentEditor
        courseId="course-1"
        assessment={assessment}
        assessmentGroups={[
          ...groups,
          {
            ...groups[0]!,
            id: "practice",
            name: "Practice",
            weightPercent: 12.5,
          },
        ]}
      />,
    );

    await user.clear(screen.getByLabelText(/description/i));
    await user.clear(screen.getByLabelText(/max score/i));
    await user.clear(screen.getByLabelText(/passing score/i));
    await user.clear(screen.getByLabelText(/time limit/i));
    fireEvent.change(screen.getByLabelText(/available from/i), {
      target: { value: "" },
    });
    fireEvent.change(screen.getByLabelText(/available until/i), {
      target: { value: "" },
    });
    await user.click(screen.getByRole("switch", { name: /^required$/i }));
    await user.click(screen.getByRole("combobox", { name: /grading group/i }));
    expect(
      await screen.findByRole("option", { name: /12.5% of Total/ }),
    ).toBeInTheDocument();
    await user.click(await screen.findByRole("option", { name: "No group" }));
    await user.click(screen.getByRole("combobox", { name: /presentation/i }));
    await user.click(
      await screen.findByRole("option", { name: "One at a time" }),
    );
    await user.click(screen.getByRole("button", { name: /save changes/i }));

    await waitFor(() => {
      expect(updateAssessment).toHaveBeenCalledWith(
        expect.objectContaining({
          courseId: "course-1",
          assessmentId: "assessment-1",
          expectedVersion: 1,
          title: assessment.title,
          slug: assessment.slug,
          description: null,
          maxScore: undefined,
          passingScore: undefined,
          timeLimitMinutes: null,
          maxAttempts: 1,
          isRequired: false,
          availableFrom: null,
          availableUntil: null,
          assessmentGroupId: null,
          clearAssessmentGroupId: true,
          presentationMode: "SingleStep",
        }),
      );
    });
    expect(routerMocks.refresh).toHaveBeenCalled();
    expect(routerMocks.replace).not.toHaveBeenCalled();
  });

  it("navigates back, respects delete cancellation, and reports delete failures", async () => {
    const user = userEvent.setup();
    render(
      <AssessmentEditor
        courseId="course-1"
        assessment={assessment}
        assessmentGroups={groups}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Back" }));
    expect(routerMocks.push).toHaveBeenCalledWith(
      "/workspace/learning/courses/course-1/assessments",
    );

    vi.mocked(confirm).mockReturnValueOnce(false);
    await user.click(
      screen.getByRole("button", { name: /delete assessment/i }),
    );
    expect(deleteAssessment).not.toHaveBeenCalled();

    vi.mocked(deleteAssessment).mockResolvedValueOnce({
      success: false,
      error: "Delete failed",
    });
    await user.click(
      screen.getByRole("button", { name: /delete assessment/i }),
    );
    expect(await screen.findByText("Delete failed")).toBeInTheDocument();
  });

  it("reports a review workflow persistence failure and keeps the draft", async () => {
    const user = userEvent.setup();
    vi.mocked(updateAssessment).mockResolvedValueOnce({
      success: false,
      error: "Grading update failed",
    });
    render(
      <AssessmentEditor
        courseId="course-1"
        assessment={assessment}
        assessmentGroups={groups}
      />,
    );

    const primaryReview = screen.getByRole("combobox", {
      name: /primary review/i,
    });
    await user.click(primaryReview);
    await user.click(
      await screen.findByRole("option", { name: "Peer review" }),
    );
    await user.click(screen.getByRole("button", { name: /save changes/i }));

    expect(
      await screen.findByText("Grading update failed"),
    ).toBeInTheDocument();
    expect(primaryReview).toHaveTextContent("Peer review");
    expect(updateAssessment).toHaveBeenCalledWith(
      expect.objectContaining({ reviewMethods: 1 }),
    );
  });

  it("renders legacy, practice, unavailable, and missing linked-content states", async () => {
    const user = userEvent.setup();
    const legacy = {
      ...assessment,
      type: "LegacyAssessment",
      contentId: "missing-content",
      assessmentGroupId: "practice",
      isAvailable: false,
    } as unknown as Assessment;
    render(
      <AssessmentEditor
        courseId="course id"
        assessment={legacy}
        assessmentGroups={[
          { ...groups[0]!, id: "practice", name: "Practice", weightPercent: 0 },
        ]}
        courseContent={courseContent}
      />,
    );

    expect(screen.getAllByText("LegacyAssessment").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Practice").length).toBeGreaterThan(0);
    expect(screen.getByText("No")).toBeInTheDocument();
    expect(screen.getByTestId("linked-content-link")).toHaveTextContent(
      "missing-content",
    );
    await user.click(
      screen.getByRole("button", { name: /edit coding definition/i }),
    );
    expect(routerMocks.push).toHaveBeenCalledWith(
      "/workspace/learning/courses/course%20id/assessments/assessment-1/coding-definition",
    );
  });

  it("falls back to the title slug and zero passing score for empty fields", async () => {
    const user = userEvent.setup();
    render(
      <AssessmentEditor
        courseId="course-1"
        assessment={{ ...assessment, description: null }}
        assessmentGroups={groups}
      />,
    );

    expect(screen.getByLabelText(/description/i)).toHaveValue("");
    await user.clear(screen.getByLabelText(/url slug/i));
    await user.clear(screen.getByLabelText(/passing score/i));
    expect(
      screen.getByText(/0 out of 10 points to pass \(0%\)/i),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: /save changes/i }));

    await waitFor(() => {
      expect(updateAssessment).toHaveBeenCalledWith(
        expect.objectContaining({
          slug: "schema-patterns-quiz",
          passingScore: undefined,
        }),
      );
    });
    await waitFor(() => {
      expect(routerMocks.replace).toHaveBeenCalledWith(
        "/workspace/learning/courses/course-1/assessments/schema-patterns-quiz",
      );
    });
  });
});
