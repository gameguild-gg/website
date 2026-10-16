import "@testing-library/jest-dom/vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  disableGrading: vi.fn(),
  enableGrading: vi.fn(),
  isPastDue: vi.fn(),
  isValidRules: vi.fn(),
  itemsToDocument: vi.fn(),
  parseDocument: vi.fn(),
  readGrading: vi.fn(),
  readRules: vi.fn(),
  serializeDocument: vi.fn(),
  sumPoints: vi.fn(),
  toItems: vi.fn(),
  toGradingItems: vi.fn(),
  updateRules: vi.fn(),
}));

vi.mock("@game-guild/grading-adapter-quiz", () => ({
  sumQuizItemPoints: mocks.sumPoints,
}));

vi.mock("@game-guild/quiz-content", () => ({
  disableQuizContentGrading: mocks.disableGrading,
  enableQuizContentGrading: mocks.enableGrading,
  isQuizPastDue: mocks.isPastDue,
  isValidQuizRules: mocks.isValidRules,
  parseQuizContentDocument: mocks.parseDocument,
  quizContentItemsToDocument: mocks.itemsToDocument,
  quizDocumentToContentItems: mocks.toItems,
  quizDocumentToGradingItems: mocks.toGradingItems,
  readQuizContentGrading: mocks.readGrading,
  readQuizContentRules: mocks.readRules,
  serializeQuizContentDocument: mocks.serializeDocument,
  updateQuizContentRules: mocks.updateRules,
}));

vi.mock("@game-guild/quiz-surface/editor", () => ({
  QuizCollectionEditor: ({
    items,
    onChange,
    readOnly = false,
    submissionMode,
  }: {
    items: unknown[];
    onChange: (items: unknown[]) => void;
    readOnly?: boolean;
    submissionMode: string;
  }) => (
    <div
      data-testid="quiz-collection-editor"
      data-read-only={String(readOnly)}
      data-submission-mode={submissionMode}
    >
      <span>{items.length} quiz items</span>
      <button type="button" onClick={() => onChange([{ id: "question-2" }])}>
        Replace quiz items
      </button>
    </div>
  ),
}));

import { QuizContentEditor } from "./quiz-content-editor";

type TestDocument = {
  items: Array<{ id: string }>;
  rules?: {
    dueAt?: string;
    timeLimitMinutes?: number;
    maxAttempts?: number;
  };
  grading?: {
    items: Record<string, unknown>;
    score: { maxScore: number; passingScore?: number };
  };
};

const disabledDocument: TestDocument = {
  items: [{ id: "question-1" }],
};

function renderEditor(
  document: TestDocument = disabledDocument,
  mode?: "edit" | "preview",
) {
  const onChange = vi.fn();
  render(
    <QuizContentEditor
      initialContent={document as never}
      onChange={onChange}
      mode={mode}
    />,
  );
  return { onChange };
}

describe("QuizContentEditor", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    mocks.parseDocument.mockImplementation((content) => ({
      document: content,
    }));
    mocks.toItems.mockImplementation(
      (document: TestDocument) => document.items,
    );
    mocks.readGrading.mockImplementation(
      (document: TestDocument) => document.grading ?? null,
    );
    mocks.toGradingItems.mockImplementation(
      (document: TestDocument) => document.items,
    );
    mocks.sumPoints.mockReturnValue(7);
    mocks.isPastDue.mockReturnValue(false);
    mocks.isValidRules.mockReturnValue(true);
    mocks.readRules.mockImplementation(
      (document: TestDocument) => document.rules,
    );
    mocks.updateRules.mockImplementation(
      (document: TestDocument, rules: unknown) => ({ ...document, rules }),
    );
    mocks.serializeDocument.mockImplementation((document) => document);
    mocks.itemsToDocument.mockImplementation(({ items, grading }) => ({
      items,
      grading,
    }));
    mocks.enableGrading.mockImplementation((document: TestDocument) => ({
      ...document,
      grading: { items: {}, score: { maxScore: 10 } },
    }));
    mocks.disableGrading.mockImplementation((document: TestDocument) => {
      const content = { ...document };
      delete content.grading;
      return content;
    });
  });

  it("renders an ungraded quiz in local-practice edit mode", () => {
    renderEditor();

    expect(screen.getByText("Off")).toBeInTheDocument();
    expect(screen.queryByLabelText("Max score")).not.toBeInTheDocument();
    expect(screen.getByTestId("quiz-collection-editor")).toHaveAttribute(
      "data-submission-mode",
      "local-practice",
    );
    expect(screen.getByTestId("quiz-collection-editor")).toHaveAttribute(
      "data-read-only",
      "false",
    );
  });

  it("renders both graded and practice previews as read-only", () => {
    const { unmount } = render(
      <QuizContentEditor
        initialContent={disabledDocument as never}
        onChange={vi.fn()}
        mode="preview"
      />,
    );
    expect(screen.getByTestId("quiz-collection-editor")).toHaveAttribute(
      "data-submission-mode",
      "local-practice",
    );
    expect(screen.getByTestId("quiz-collection-editor")).toHaveAttribute(
      "data-read-only",
      "true",
    );

    unmount();
    renderEditor(
      {
        ...disabledDocument,
        grading: { items: {}, score: { maxScore: 10 } },
      },
      "preview",
    );
    expect(screen.getByTestId("quiz-collection-editor")).toHaveAttribute(
      "data-submission-mode",
      "server-graded",
    );
  });

  it("updates a mounted preview when the draft content changes", () => {
    const { rerender } = render(
      <QuizContentEditor
        initialContent={disabledDocument as never}
        onChange={vi.fn()}
        mode="preview"
      />,
    );

    expect(screen.getByText("1 quiz items")).toBeInTheDocument();

    rerender(
      <QuizContentEditor
        initialContent={{
          ...disabledDocument,
          items: [{ id: "question-1" }, { id: "question-2" }],
        } as never}
        onChange={vi.fn()}
        mode="preview"
      />,
    );

    expect(screen.getByText("2 quiz items")).toBeInTheDocument();
  });

  it("enables and disables server grading", () => {
    const { onChange } = renderEditor();
    const gradingSwitch = screen.getByRole("switch", { name: "Grading" });

    fireEvent.click(gradingSwitch);
    expect(mocks.enableGrading).toHaveBeenCalledWith(disabledDocument);
    expect(onChange).toHaveBeenLastCalledWith(
      expect.objectContaining({
        grading: expect.objectContaining({
          items: {},
          score: { maxScore: 10 },
        }),
      }),
    );
    expect(screen.getByText("0 items")).toBeInTheDocument();
    expect(screen.getByText("7 configured pts")).toBeInTheDocument();
    expect(screen.getByText("Assessment")).toBeInTheDocument();

    fireEvent.click(gradingSwitch);
    expect(mocks.disableGrading).toHaveBeenCalled();
    expect(screen.getByText("Off")).toBeInTheDocument();
  });

  it("rebuilds the document when quiz items change", () => {
    const { onChange } = renderEditor();

    fireEvent.click(screen.getByRole("button", { name: "Replace quiz items" }));

    expect(mocks.itemsToDocument).toHaveBeenCalledWith({
      items: [{ id: "question-2" }],
      rules: disabledDocument.rules,
      grading: disabledDocument.grading,
    });
    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({ items: [{ id: "question-2" }] }),
    );
  });

  it("edits quiz rules (due date, time limit, attempts) and commits them", () => {
    const { onChange } = renderEditor();

    // Empty state copy renders when no rules are configured.
    expect(
      screen.getByText("No rules configured — the quiz is always available."),
    ).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Time limit (minutes)"), {
      target: { value: "30" },
    });

    expect(mocks.isValidRules).toHaveBeenCalledWith({ timeLimitMinutes: 30 });
    expect(mocks.updateRules).toHaveBeenCalledWith(disabledDocument, {
      timeLimitMinutes: 30,
    });
    expect(onChange).toHaveBeenCalled();

    fireEvent.change(screen.getByLabelText("Max attempts"), {
      target: { value: "3" },
    });
    expect(mocks.updateRules).toHaveBeenLastCalledWith(
      expect.objectContaining({ items: disabledDocument.items }),
      {
        timeLimitMinutes: 30,
        maxAttempts: 3,
      },
    );
  });

  it("renders configured rules as badges and past-due due dates as destructive", () => {
    mocks.isPastDue.mockReturnValue(true);
    renderEditor({
      ...disabledDocument,
      rules: {
        dueAt: "2026-01-01T00:00:00.000Z",
        timeLimitMinutes: 20,
        maxAttempts: 2,
      },
    });

    expect(screen.getByText("20 min limit")).toBeInTheDocument();
    expect(screen.getByText("2 attempts")).toBeInTheDocument();
    const dueBadge = screen
      .getAllByText(/Due /)
      .find((element) => element.getAttribute("data-slot") === "badge");
    expect(dueBadge).toBeDefined();
    expect(mocks.isPastDue).toHaveBeenCalledWith({
      dueAt: "2026-01-01T00:00:00.000Z",
      timeLimitMinutes: 20,
      maxAttempts: 2,
    });
  });

  it("summarizes configured grading items and their points", () => {
    renderEditor({
      ...disabledDocument,
      grading: {
        items: { one: {}, two: {} },
        score: { maxScore: 10, passingScore: 6 },
      },
    });

    expect(screen.getByText("2 items")).toBeInTheDocument();
    expect(screen.getByText("7 configured pts")).toBeInTheDocument();
    expect(screen.getByText("Assessment")).toBeInTheDocument();
  });
});
