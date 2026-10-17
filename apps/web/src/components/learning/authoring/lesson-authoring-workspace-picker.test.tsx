import "@testing-library/jest-dom/vitest";
import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  assetsProviderProps: undefined as
    | { repository?: unknown; scope?: unknown }
    | undefined,
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
    refresh: vi.fn(),
  }),
  useParams: () => ({ locale: "en-US" }),
}));
vi.mock("@/lib/learning/use-learning-base", () => ({
  useLearningBase: () => "/workspace/learning",
}));
vi.mock("@/lib/learning/authoring", () => ({
  saveAuthoringDraft: vi.fn(),
  getAuthoringDraft: vi.fn(),
  publishAuthoringDraft: vi.fn(),
  getAiEntitlement: vi.fn(),
  getAiConversations: vi.fn(),
  createAiAuthoringRun: vi.fn(),
  getAiAuthoringRun: vi.fn(),
  cancelAiAuthoringRun: vi.fn(),
  applyAiProposal: vi.fn(),
  discardAiProposal: vi.fn(),
}));
vi.mock("@/lib/learning/assets/learning-asset-repository", () => ({
  getLearningAssetRepository: () => ({}),
}));
vi.mock("@/lib/learning/assets/prepare-authoring-assets", () => ({
  prepareAuthoringAssets: vi.fn(),
}));
vi.mock("@/lib/learning/actions", () => ({
  createAssessment: vi.fn(),
  deleteAssessment: vi.fn(),
  restoreAssessment: vi.fn(),
}));
vi.mock("@/components/learning/learner-lesson-renderer", () => ({
  LearnerLessonRenderer: () => <div data-testid="learner-renderer" />,
}));
vi.mock("@game-guild/ui/components/scroll-area", () => ({
  ScrollArea: ({ children }: { children: ReactNode }) => <div>{children}</div>,
}));
vi.mock(
  "@/components/learning/console/courses/[course]/content/[contentId]/lesson-code-editor",
  () => ({
    LessonCodeEditor: () => <div />,
  }),
);
vi.mock(
  "@/components/learning/console/courses/[course]/content/[contentId]/lesson-content-editor",
  () => ({
    LessonContentEditor: () => <div />,
  }),
);
vi.mock(
  "@/components/learning/console/courses/[course]/content/[contentId]/lesson-video-editor",
  () => ({
    LessonVideoEditor: () => <div />,
  }),
);
vi.mock(
  "@/components/learning/console/courses/[course]/content/[contentId]/lesson-external-link-editor",
  () => ({
    LessonExternalLinkEditor: () => <div />,
  }),
);
vi.mock(
  "@/components/learning/console/courses/[course]/content/[contentId]/quiz-content-editor",
  () => ({
    QuizContentEditor: () => <div />,
  }),
);
vi.mock(
  "@/components/learning/console/courses/[course]/assessments/[assessmentId]/coding-definition/coding-definition-editor",
  () => ({
    CodingDefinitionEditor: () => <div />,
  }),
);
vi.mock("@/components/learning/authoring/coding-assignment-preview", () => ({
  CodingAssignmentPreview: () => <div />,
}));
vi.mock("@monaco-editor/react", () => ({
  DiffEditor: () => <div />,
}));
vi.mock("next-themes", () => ({
  useTheme: () => ({ resolvedTheme: "dark" }),
}));
vi.mock("@game-guild/assets/react", () => ({
  AssetsProvider: ({
    children,
    ...props
  }: {
    children: ReactNode;
    repository?: unknown;
    scope?: unknown;
  }) => {
    mocks.assetsProviderProps = props;
    return <>{children}</>;
  },
  AssetPickerDialog: (props: { open: boolean; title?: string }) => (
    <div
      data-testid="asset-picker-dialog"
      data-open={String(props.open)}
      data-title={props.title}
    />
  ),
}));

import { LessonAuthoringWorkspace } from "./lesson-authoring-workspace";

const initialDraft = {
  id: "draft-1",
  programId: "course-1",
  contentId: "quiz-1",
  payload: {
    title: "Original quiz",
    slug: "original-quiz",
    description: null,
    type: "Questionnaire",
    body: null,
    jsonBody: { schemaVersion: 1, order: [], blocks: {} },
    lessonFormat: null,
    activitySettings: null,
    isRequired: true,
    estimatedMinutes: 5,
    estimatedMinutesSource: "Manual",
    visibility: "Public",
  },
  basePublishedVersion: 0,
  revision: 1,
  eTag: '"draft-1-1"',
  lastEditedBy: "author-1",
  lastEditedAt: "2026-09-10T12:00:00Z",
} as never;

const quizItem = {
  id: "quiz-1",
  title: "Original quiz",
  slug: "original-quiz",
  type: "Questionnaire",
  status: "draft",
  order: 1,
} as never;

describe("LessonAuthoringWorkspace asset picker", () => {
  it("scopes the asset picker to the ProgramContent being authored", () => {
    render(
      <LessonAuthoringWorkspace
        courseId="course-1"
        courseSlug="course-slug"
        courseTitle="Course title"
        item={quizItem}
        curriculum={[quizItem]}
        initialDraft={initialDraft}
      />,
    );

    expect(mocks.assetsProviderProps?.scope).toEqual({
      type: "ProgramContent",
      id: "quiz-1",
    });
    expect(screen.getByTestId("asset-picker-dialog")).toHaveAttribute(
      "data-open",
      "false",
    );
  });
});
