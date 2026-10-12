import "@testing-library/jest-dom/vitest";
import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { AssessmentSubmissionRuntimeViewV1 } from "@game-guild/grading";
import {
  createQuizAnswerEnvelope,
  QUIZ_ASSESSMENT_TYPE_ADAPTER,
} from "@game-guild/grading-adapter-quiz";
import { QuizEntryType } from "@game-guild/quiz";

const actions = vi.hoisted(() => ({ get: vi.fn() }));
const codeGrader = vi.hoisted(() => vi.fn());

vi.mock('@/components/learning/assessment-grading/assessment-grader', () => ({
  AssessmentGrader: (props: unknown) => {
    codeGrader(props);
    return <div data-testid="code-grader" />;
  },
}));

vi.mock("@/lib/learning/grading-runtime-actions", () => ({
  getRuntimeSubmission: actions.get,
}));

vi.mock("@game-guild/quiz-surface/player", () => ({
  QuizPlayer: ({
    entry,
    answer,
    disabled,
    submissionResult,
  }: {
    entry: { stem?: string };
    answer: unknown;
    disabled?: boolean;
    submissionResult?: { status?: string; feedback?: string };
  }) => (
    <div data-testid="quiz-player">
      <span>{entry.stem}</span>
      <span>{JSON.stringify(answer)}</span>
      <span>{submissionResult?.status}</span>
      <span>{submissionResult?.feedback}</span>
      <span>{disabled ? "disabled" : "enabled"}</span>
    </div>
  ),
}));

import { SubmissionViewer } from "./submission-viewer";

function runtimeSubmission(
  overrides: Partial<AssessmentSubmissionRuntimeViewV1> = {},
): AssessmentSubmissionRuntimeViewV1 {
  return {
    submissionId: "submission-1",
    assessmentId: "assessment-1",
    definitionRevisionId: "revision-1234",
    enrollmentId: "enrollment-1",
    courseGroupId: null,
    attemptNumber: 1,
    status: "graded",
    draftVersion: 0,
    version: 4,
    startedAt: "2026-08-01T09:00:00Z",
    submittedAt: "2026-08-01T10:00:00Z",
    submittedByUserId: "user-1",
    contentCompleted: false,
    execution: {
      executionId: "execution-1",
      definitionRevisionId: "revision-1234",
      context: "official-submission",
      executionSnapshotHash: "a".repeat(64),
      deliveryHash: "b".repeat(64),
      delivery: {
        schemaVersion: 1,
        definitionRevisionId: "revision-1234",
        executionSnapshotHash: "a".repeat(64),
        itemOrder: ["q1"],
        items: {
          q1: {
            adapterKey: QUIZ_ASSESSMENT_TYPE_ADAPTER.key,
            adapterVersion: QUIZ_ASSESSMENT_TYPE_ADAPTER.version,
            learnerPayload: {
              itemId: "q1",
              entry: {
                type: QuizEntryType.TrueFalse,
                stem: "The immutable question",
                settings: { allowRetry: false },
              },
            },
          },
        },
      },
      itemMaxScores: { q1: 100 },
      submittedResponse: createQuizAnswerEnvelope({
        q1: { type: QuizEntryType.TrueFalse, value: true },
      }),
      status: "completed",
      activeRoundId: "round-1",
      instructorVisibleResult: {
        schemaVersion: 1,
        state: "final",
        score: 100,
        maxScore: 100,
        evidenceRefs: [],
        feedback: null,
        items: [
          {
            itemId: "q1",
            state: "graded",
            score: 100,
            maxScore: 100,
            evidenceRefs: [],
            feedback: "Correct answer.",
            reviewMethod: "AutomatedReview",
            handlerKey: "quiz-automated-review",
            handlerVersion: "1",
          },
        ],
      },
      learnerVisibleResult: null,
      requiresInstructorReview: false,
      released: false,
      history: [],
    },
    ...overrides,
  };
}

describe("runtime SubmissionViewer", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders the exact immutable delivery, submitted answer and server result", async () => {
    actions.get.mockResolvedValue({
      success: true,
      data: runtimeSubmission(),
    });

    render(<SubmissionViewer submissionId="submission-1" />);

    expect(
      await screen.findByText("The immutable question"),
    ).toBeInTheDocument();
    expect(screen.getByTestId("quiz-player")).toHaveTextContent('"value":true');
    expect(screen.getByTestId("quiz-player")).toHaveTextContent("correct");
    expect(screen.getByTestId("quiz-player")).toHaveTextContent(
      "Correct answer.",
    );
    expect(screen.getByTestId("quiz-player")).toHaveTextContent("disabled");
    expect(screen.getByText("Delivery bbbbbbbbbbbb")).toBeInTheDocument();
  });

  it("falls back to the generic immutable envelope for a non-quiz adapter", async () => {
    const submission = runtimeSubmission();
    actions.get.mockResolvedValue({
      success: true,
      data: {
        ...submission,
        execution: {
          ...submission.execution,
          delivery: {
            ...submission.execution.delivery,
            items: {
              q1: {
                adapterKey: "coding-assessment-type",
                adapterVersion: "1",
                learnerPayload: { prompt: "Compile this program." },
              },
            },
          },
          submittedResponse: {
            schemaVersion: 1,
            contentType: "coding-assignment",
            payloadSchema: "coding-answer/v1",
            payload: { files: [] },
          },
        },
      },
    });

    render(<SubmissionViewer submissionId="submission-1" />);

    expect(
      await screen.findByTestId("runtime-generic-submission"),
    ).toHaveTextContent("Compile this program.");
    expect(screen.getByTestId("runtime-generic-submission")).toHaveTextContent(
      "coding-answer/v1",
    );
  });

  it("shows a runtime loading failure", async () => {
    actions.get.mockResolvedValue({ success: false, error: "boom" });

    render(<SubmissionViewer submissionId="submission-1" />);

    expect(await screen.findByTestId("viewer-error")).toHaveTextContent("boom");
  });

  it.each([true, false])('restores the Code response using only the private frozen definition (available=%s)', async (available) => {
    const value = runtimeSubmission();
    const privateDefinition = {
      Type: 'coding-assignment', Version: 1, Environment: { Language: 'cpp' },
      Data: { Files: {} }, Tests: { Public: [], Private: [{ Name: 'Frozen private test' }] },
      Grading: { MaxScore: 100 },
    };
    value.execution.delivery.itemOrder = ['code'];
    value.execution.delivery.items = { code: {
      adapterKey: 'code-assessment-type', adapterVersion: '1',
      learnerPayload: { definition: { ...privateDefinition, Tests: { Public: [], Private: [] } } },
    } };
    value.execution.submittedResponse = {
      schemaVersion: 1, contentType: 'coding-assignment', payloadSchema: 'code-files/v1',
      payload: { files: { '/home/user/main.cpp': { content: '// immutable response', encoding: 'text' } } },
    };
    value.execution.instructorVisibleContent = available ? privateDefinition : null;
    actions.get.mockResolvedValue({ success: true, data: value });
    render(<SubmissionViewer submissionId="submission-1" />);
    if (available) {
      await screen.findByTestId('code-grader');
      expect(codeGrader).toHaveBeenLastCalledWith(expect.objectContaining({
        assignment: privateDefinition,
        submittedFiles: [{ path: '/home/user/main.cpp', content: '// immutable response' }],
        maxScore: 100, submissionId: 'submission-1',
      }));
    } else {
      expect(await screen.findByRole('alert')).toHaveTextContent('The frozen Code definition is unavailable');
      expect(codeGrader).not.toHaveBeenCalled();
    }
  });
});
