import "@testing-library/jest-dom/vitest";

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  FillBlankInputType,
  QuizEntryType,
  type CategorizationEntry,
  type FillInTheBlankEntry,
  type OrderingEntry,
} from "@game-guild/quiz";
import type { RendererAnswerState } from "../player/renderer-answer-adapter";
import { CategorizationRenderer } from "./categorization/player";
import { FillBlankRenderer } from "./fill-blank/player";
import { OrderingRenderer } from "./ordering/player";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

const EMPTY_ANSWER_STATE: RendererAnswerState = {
  selectedOptionIds: [],
  textAnswers: {},
  categorizations: {},
  ordering: [],
};

describe("quiz question interactions", () => {
  it("keeps word-bank renderer identity out of the learner answer", () => {
    const random = vi.spyOn(Math, "random");
    const entry: FillInTheBlankEntry = {
      type: QuizEntryType.FillInTheBlank,
      stem: "Ownership is a ___ concept.",
      blanks: [
        {
          id: "blank",
          position: 15,
          input: {
            type: FillBlankInputType.WordBank,
            words: ["Rust", "Java", "Python"],
          },
        },
      ],
      settings: { allowRetry: false },
    };
    const onAnswerChange = vi.fn();
    const dataTransfer = createDataTransfer();

    render(
      <FillBlankRenderer
        entry={entry}
        answerState={EMPTY_ANSWER_STATE}
        onAnswerChange={onAnswerChange}
      />,
    );

    fireEvent.dragStart(screen.getByTestId("word-bank-word-0"), {
      dataTransfer,
    });
    fireEvent.drop(screen.getByTestId("word-bank-blank-blank"), {
      dataTransfer,
    });

    expect(onAnswerChange).toHaveBeenLastCalledWith({
      textAnswers: { blank: "Rust" },
    });
    expect(random).not.toHaveBeenCalled();
  });

  it("reads categorization drag identity from the transfer payload", () => {
    const entry: CategorizationEntry = {
      type: QuizEntryType.Categorization,
      stem: "Categorize the technology.",
      categories: [{ id: "language", name: "Language" }],
      items: [{ id: "rust", text: "Rust", correctCategoryIds: ["language"] }],
      settings: { allowRetry: false },
    };
    const onAnswerChange = vi.fn();
    const dataTransfer = createDataTransfer();

    render(
      <CategorizationRenderer
        entry={entry}
        answerState={EMPTY_ANSWER_STATE}
        onAnswerChange={onAnswerChange}
      />,
    );

    fireEvent.dragStart(screen.getByTestId("categorization-item-rust"), {
      dataTransfer,
    });
    fireEvent.drop(screen.getByTestId("categorization-drop-language"), {
      dataTransfer,
    });

    expect(onAnswerChange).toHaveBeenLastCalledWith({
      categorizations: { rust: ["language"] },
    });
  });

  it("exposes deterministic ordering controls", () => {
    const entry: OrderingEntry = {
      type: QuizEntryType.Ordering,
      stem: "Order the release steps.",
      items: [
        { id: "submit", text: "Submit", correctPosition: 0 },
        { id: "review", text: "Review", correctPosition: 1 },
        { id: "release", text: "Release", correctPosition: 2 },
      ],
      settings: { allowRetry: false },
    };
    const onAnswerChange = vi.fn();

    render(
      <OrderingRenderer
        entry={entry}
        answerState={{
          ...EMPTY_ANSWER_STATE,
          ordering: ["submit", "release", "review"],
        }}
        onAnswerChange={onAnswerChange}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Move Review up" }));

    expect(onAnswerChange).toHaveBeenLastCalledWith({
      ordering: ["submit", "review", "release"],
    });
  });
});

function createDataTransfer(): DataTransfer {
  const values = new Map<string, string>();
  return {
    effectAllowed: "none",
    getData: (type: string) => values.get(type) ?? "",
    setData: (type: string, value: string) => {
      values.set(type, value);
    },
  } as unknown as DataTransfer;
}
