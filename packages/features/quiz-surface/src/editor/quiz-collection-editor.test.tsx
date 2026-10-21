import "@testing-library/jest-dom/vitest";

import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  createTrueFalseEntry,
  type QuizEntry,
} from "@game-guild/quiz";
import type { QuizContentItem } from "@game-guild/quiz-content";
import { QuizCollectionEditor } from "./quiz-collection-editor";

function question(id: string, stem: string): QuizContentItem {
  return {
    id,
    entry: createTrueFalseEntry(stem) as QuizEntry,
  };
}

afterEach(cleanup);

describe("QuizCollectionEditor", () => {
  it("renders complete question blocks and reorders them", () => {
    const items = [question("first", "First question"), question("second", "Second question")];
    const onChange = vi.fn();

    render(
      <QuizCollectionEditor
        items={items}
        onChange={onChange}
        createItemId={() => "third"}
      />,
    );

    expect(screen.getByText("First question")).toBeInTheDocument();
    expect(screen.getByText("Second question")).toBeInTheDocument();
    expect(
      screen.getByText("First question").closest("[data-quiz-block-card]"),
    ).toHaveClass("transition-all", "duration-300", "hover:shadow-md");
    expect(
      screen.getAllByRole("button", { name: "Insert question" })[0],
    ).toHaveClass("hover:scale-110", "opacity-40");

    fireEvent.click(screen.getByRole("button", { name: "Move question 2 up" }));
    expect(onChange).toHaveBeenCalledWith([items[1], items[0]]);
  });

  it("opens the package question-type selector from an insert line", () => {
    render(
      <QuizCollectionEditor
        items={[question("first", "First question")]}
        onChange={vi.fn()}
        createItemId={() => "second"}
      />,
    );

    fireEvent.click(screen.getAllByRole("button", { name: "Insert question" })[0]!);
    expect(
      screen.getByRole("heading", { name: "Choose a Question Type" }),
    ).toBeInTheDocument();
  });

  it("reports hydration and opens an existing question in the focused editor", () => {
    const { container } = render(
      <QuizCollectionEditor
        items={[question("first", "First question")]}
        onChange={vi.fn()}
      />,
    );

    expect(
      container.querySelector("[data-quiz-collection-editor]"),
    ).toHaveAttribute("data-hydrated", "true");

    fireEvent.click(screen.getByRole("button", { name: "Edit question 1" }));

    expect(
      screen.getByRole("dialog", { name: "Quiz Builder" }),
    ).toBeInTheDocument();
    expect(screen.getByLabelText("Question")).toHaveValue("First question");
  });

  it("shows the animated drop target and commits drag-and-drop ordering", async () => {
    const items = [
      question("first", "First question"),
      question("second", "Second question"),
    ];
    const onChange = vi.fn();
    const dataTransfer = {
      dropEffect: "none",
      effectAllowed: "none",
      setDragImage: vi.fn(),
    };
    const { container } = render(
      <QuizCollectionEditor
        items={items}
        onChange={onChange}
        createItemId={() => "third"}
      />,
    );
    const cards = container.querySelectorAll<HTMLElement>(
      "[data-quiz-block-card]",
    );
    fireEvent.dragStart(
      screen.getByRole("button", { name: "Drag question 1" }),
      { dataTransfer },
    );
    await waitFor(() => expect(cards[0]).toHaveClass("opacity-30"));
    await waitFor(() =>
      expect(
        screen.queryAllByRole("button", { name: "Insert question" }),
      ).toHaveLength(0),
    );

    const activeCards = container.querySelectorAll<HTMLElement>(
      "[data-quiz-block-card]",
    );
    const secondCard = activeCards[1]!;
    vi.spyOn(secondCard, "getBoundingClientRect").mockReturnValue({
      top: 0,
      bottom: 100,
      height: 100,
      left: 0,
      right: 100,
      width: 100,
      x: 0,
      y: 0,
      toJSON: () => ({}),
    });

    fireEvent.dragOver(secondCard, { clientY: 90, dataTransfer });
    const dropTarget = await screen.findByText("Move here");
    fireEvent.drop(dropTarget.parentElement!, { dataTransfer });

    expect(onChange).toHaveBeenCalledWith([items[1], items[0]]);
    expect(dataTransfer.setDragImage).toHaveBeenCalled();
  });
});
