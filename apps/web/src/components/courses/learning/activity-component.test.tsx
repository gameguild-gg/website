import "@testing-library/jest-dom/vitest";

import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ActivityComponent } from "./activity-component";

const mocks = vi.hoisted(() => ({
  submitActivity: vi.fn(),
  getRuntimeSubmission: vi.fn(),
  startContentRuntimeSubmission: vi.fn(),
  submitRuntimeSubmission: vi.fn(),
}));

vi.mock("@/lib/courses/server-actions", () => ({
  submitActivity: mocks.submitActivity,
}));

vi.mock("@/lib/learning/grading-runtime-actions", () => ({
  getRuntimeSubmission: mocks.getRuntimeSubmission,
  startContentRuntimeSubmission: mocks.startContentRuntimeSubmission,
  submitRuntimeSubmission: mocks.submitRuntimeSubmission,
}));

vi.mock("@game-guild/quiz-surface/player", () => ({
  QuizPlayer: ({
    entry,
    answer,
    disabled,
    onAnswerChange,
    submissionResult,
  }: {
    entry: { stem: string };
    answer: unknown;
    disabled?: boolean;
    onAnswerChange: (answer: { type: "TRUE_FALSE"; value: boolean }) => void;
    submissionResult?: { feedback?: string };
  }) => (
    <div>
      <span data-testid="server-player">{entry.stem}</span>
      <span data-testid="server-answer">{JSON.stringify(answer)}</span>
      <span data-testid="server-disabled">{String(disabled)}</span>
      <button
        type="button"
        onClick={() => onAnswerChange({ type: "TRUE_FALSE", value: true })}
      >
        Choose server true
      </button>
      {submissionResult?.feedback && <span>{submissionResult.feedback}</span>}
    </div>
  ),
  QuizPracticePlayer: ({
    entry,
    onAnswerChange,
  }: {
    entry: { stem: string };
    onAnswerChange: (answer: { type: "TRUE_FALSE"; value: boolean }) => void;
  }) => (
    <div>
      <span data-testid="practice-player">{entry.stem}</span>
      <button
        type="button"
        onClick={() => onAnswerChange({ type: "TRUE_FALSE", value: true })}
      >
        Choose true
      </button>
    </div>
  ),
}));

const quizItem = {
  id: "3d85ccca-7428-4fc9-88c7-13670a98d0f1",
  title: "Canonical quiz",
  type: "quiz" as const,
  status: "available" as const,
  order: 1,
  isRequired: true,
  activityType: "quiz" as const,
  content: {
    mode: "local-practice",
    document: {
      schemaVersion: 1,
      order: [["question-1", "quiz"]],
      blocks: {
        "question-1": {
          type: "TRUE_FALSE",
          stem: "The package owns this question",
          correctAnswer: true,
          points: "00000002.0000",
          settings: {
            allowRetry: true,
            showFeedback: true,
            showCorrectAnswer: true,
          },
        },
      },
    },
  },
};

describe("ActivityComponent quiz integration", () => {
  beforeEach(() => {
    mocks.submitActivity.mockReset();
    mocks.submitActivity.mockResolvedValue({ success: true });
    mocks.getRuntimeSubmission.mockReset();
    mocks.startContentRuntimeSubmission.mockReset();
    mocks.submitRuntimeSubmission.mockReset();
  });

  it("renders canonical quiz storage with the package player and submits typed answers", async () => {
    const user = userEvent.setup();
    const onComplete = vi.fn();

    render(
      <ActivityComponent
        item={quizItem}
        courseId="course-1"
        onComplete={onComplete}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Start Activity" }));
    expect(screen.getByTestId("practice-player")).toHaveTextContent(
      "The package owns this question",
    );

    await user.click(screen.getByRole("button", { name: "Choose true" }));
    await user.click(screen.getByRole("button", { name: "Submit Quiz" }));

    expect(mocks.submitActivity).toHaveBeenCalledWith(
      expect.objectContaining({
        content: {
          schemaVersion: 1,
          contentType: "quiz",
          payloadSchema: "quiz-answer/v1",
          payload: {
            answers: {
              "question-1": { type: "TRUE_FALSE", value: true },
            },
          },
        },
        isGraded: false,
      }),
    );
    expect(onComplete).toHaveBeenCalledWith(100);
  });

  it("keeps the explicit server-graded mode after learner redaction", async () => {
    const user = userEvent.setup();
    const onComplete = vi.fn();
    const serverItem = {
      ...quizItem,
      content: {
        mode: "server-graded" as const,
        document: {
          schemaVersion: 1 as const,
          order: [["question-1", "quiz"]] as const,
          blocks: {
            "question-1": {
              type: "TRUE_FALSE" as const,
              stem: "The answer key was redacted",
              points: "00000002.0000",
              settings: { allowRetry: false },
            },
          },
        },
      },
    };
    const runtimeSubmission = {
      submissionId: "submission-1",
      assessmentId: "assessment-1",
      definitionRevisionId: "revision-1",
      enrollmentId: "enrollment-1",
      courseGroupId: null,
      attemptNumber: 1,
      status: "inProgress",
      draftVersion: 0,
      version: 0,
      startedAt: "2026-09-15T10:00:00Z",
      submittedAt: null,
      submittedByUserId: null,
      contentCompleted: false,
      execution: {
        executionId: "execution-1",
        definitionRevisionId: "revision-1",
        context: "official-submission",
        executionSnapshotHash: "snapshot-hash",
        deliveryHash: "delivery-hash",
        delivery: {
          schemaVersion: 1,
          definitionRevisionId: "revision-1",
          executionSnapshotHash: "snapshot-hash",
          itemOrder: ["question-1"],
          items: {
            "question-1": {
              adapterKey: "quiz-assessment-type",
              adapterVersion: "1",
              learnerPayload: {
                itemId: "question-1",
                entry: {
                  type: "TRUE_FALSE",
                  stem: "Server-owned challenge",
                  points: "00000002.0000",
                  settings: { allowRetry: false },
                },
              },
            },
          },
        },
        itemMaxScores: { "question-1": 200 },
        submittedResponse: null,
        status: "pending",
        activeRoundId: null,
        instructorVisibleResult: null,
        learnerVisibleResult: null,
        requiresInstructorReview: false,
        released: false,
        history: [],
      },
    };
    mocks.startContentRuntimeSubmission.mockResolvedValue({
      success: true,
      data: runtimeSubmission,
    });
    mocks.submitRuntimeSubmission.mockResolvedValue({
      success: true,
      data: {
        ...runtimeSubmission,
        status: "submitted",
        submittedAt: "2026-09-15T10:05:00Z",
        submittedByUserId: "user-1",
        execution: {
          ...runtimeSubmission.execution,
          status: "awaitingReview",
          requiresInstructorReview: true,
        },
      },
    });

    render(
      <ActivityComponent
        item={serverItem}
        courseId="course-1"
        onComplete={onComplete}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Start Activity" }));
    expect(screen.getByTestId("server-player")).toHaveTextContent(
      "Server-owned challenge",
    );
    await user.click(screen.getByRole("button", { name: "Choose server true" }));
    await user.click(screen.getByRole("button", { name: "Submit Quiz" }));
    expect(mocks.startContentRuntimeSubmission).toHaveBeenCalledWith(
      serverItem.id,
      expect.any(String),
    );
    expect(mocks.submitRuntimeSubmission).toHaveBeenCalledWith(
      "submission-1",
      expect.objectContaining({
        contentType: "quiz",
        payload: {
          answers: {
            "question-1": { type: "TRUE_FALSE", value: true },
          },
        },
      }),
      expect.any(String),
    );
    expect(mocks.submitActivity).not.toHaveBeenCalled();
    expect(onComplete).not.toHaveBeenCalled();
    expect(screen.getByText("Submitted for instructor review.")).toBeInTheDocument();
  });

  it("restores a released submission and its correction after reload", async () => {
    const onComplete = vi.fn();
    const serverItem = {
      ...quizItem,
      content: {
        mode: "server-graded" as const,
        document: {
          schemaVersion: 1 as const,
          order: [["question-1", "quiz"]] as const,
          blocks: {
            "question-1": {
              type: "TRUE_FALSE" as const,
              stem: "The answer key was redacted",
              points: "00000002.0000",
              settings: { allowRetry: false },
            },
          },
        },
      },
    };
    const releasedSubmission = {
      submissionId: "submission-1",
      assessmentId: "assessment-1",
      definitionRevisionId: "revision-1",
      enrollmentId: "enrollment-1",
      courseGroupId: null,
      attemptNumber: 1,
      status: "graded",
      draftVersion: 0,
      version: 3,
      startedAt: "2026-09-15T10:00:00Z",
      submittedAt: "2026-09-15T10:05:00Z",
      submittedByUserId: "user-1",
      contentCompleted: false,
      execution: {
        executionId: "execution-1",
        definitionRevisionId: "revision-1",
        context: "official-submission",
        executionSnapshotHash: "snapshot-hash",
        deliveryHash: "delivery-hash",
        delivery: {
          schemaVersion: 1,
          definitionRevisionId: "revision-1",
          executionSnapshotHash: "snapshot-hash",
          itemOrder: ["question-1"],
          items: {
            "question-1": {
              adapterKey: "quiz-assessment-type",
              adapterVersion: "1",
              learnerPayload: {
                itemId: "question-1",
                entry: {
                  type: "TRUE_FALSE",
                  stem: "Server-owned challenge",
                  points: "00000002.0000",
                  settings: { allowRetry: false },
                },
              },
            },
          },
        },
        itemMaxScores: { "question-1": 200 },
        submittedResponse: {
          schemaVersion: 1,
          contentType: "quiz",
          payloadSchema: "quiz-answer/v1",
          payload: {
            answers: {
              "question-1": { type: "TRUE_FALSE", value: true },
            },
          },
        },
        status: "completed",
        activeRoundId: "round-1",
        instructorVisibleResult: null,
        learnerVisibleResult: {
          schemaVersion: 1,
          state: "final",
          score: 150,
          maxScore: 200,
          passed: false,
          feedback: "Reviewed by the instructor.",
          items: [
            {
              itemId: "question-1",
              state: "graded",
              score: 150,
              maxScore: 200,
              feedback: "Partially correct after review.",
            },
          ],
        },
        requiresInstructorReview: false,
        released: true,
        history: [],
      },
    };
    mocks.getRuntimeSubmission.mockResolvedValue({
      success: true,
      data: releasedSubmission,
    });

    render(
      <ActivityComponent
        item={serverItem}
        courseId="course-1"
        existingSubmissionId="submission-1"
        onComplete={onComplete}
      />,
    );

    expect(await screen.findByTestId("server-player")).toHaveTextContent(
      "Server-owned challenge",
    );
    expect(mocks.getRuntimeSubmission).toHaveBeenCalledWith("submission-1");
    expect(screen.getByTestId("server-answer")).toHaveTextContent(
      '"value":true',
    );
    expect(screen.getByTestId("server-disabled")).toHaveTextContent("true");
    expect(
      screen.getByText("Partially correct after review."),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Start Activity" }),
    ).not.toBeInTheDocument();
  });
});
