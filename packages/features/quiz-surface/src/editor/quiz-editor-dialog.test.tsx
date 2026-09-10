import "@testing-library/jest-dom/vitest";

import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { createTrueFalseEntry } from "@game-guild/quiz";
import { QuizEditorDialog } from "./quiz-editor-dialog";

class ResizeObserverMock {
  observe() {}
  unobserve() {}
  disconnect() {}
}

class PointerEventMock extends MouseEvent {}

beforeAll(() => {
  vi.stubGlobal("ResizeObserver", ResizeObserverMock);
  vi.stubGlobal("PointerEvent", PointerEventMock);
});

afterAll(() => {
  vi.unstubAllGlobals();
});

afterEach(cleanup);

describe("QuizEditorDialog", () => {
  it("owns the full editor workspace and mounts it through a portal", () => {
    const onOpenChange = vi.fn();
    const { container } = render(
      <QuizEditorDialog
        open={true}
        value={createTrueFalseEntry("The Earth is round.")}
        onOpenChange={onOpenChange}
        onCommit={vi.fn()}
      />,
    );

    const dialog = screen.getByRole("dialog", { name: "Quiz Builder" });
    expect(container).toBeEmptyDOMElement();
    expect(document.body).toContainElement(dialog);
    // jsdom 30 resolves vw against its 1024px viewport in computed styles, so assert authored inline styles.
    expect(dialog.style.width).toBe("100vw");
    expect(dialog.style.height).toBe("100dvh");
    expect(screen.getByText("Configuration")).toBeInTheDocument();
    expect(screen.getByText("Live preview")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Quiz editor settings" }),
    ).toBeInTheDocument();

    fireEvent.keyDown(document, { key: "Escape" });
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it("starts each open session from the current question", () => {
    const onOpenChange = vi.fn();
    const first = createTrueFalseEntry("First question");
    const second = createTrueFalseEntry("Second question");
    const { rerender } = render(
      <QuizEditorDialog
        open={true}
        value={first}
        onOpenChange={onOpenChange}
        onCommit={vi.fn()}
      />,
    );

    expect(screen.getByLabelText("Question")).toHaveValue("First question");
    rerender(
      <QuizEditorDialog
        open={false}
        value={second}
        onOpenChange={onOpenChange}
        onCommit={vi.fn()}
      />,
    );
    rerender(
      <QuizEditorDialog
        open={true}
        value={second}
        onOpenChange={onOpenChange}
        onCommit={vi.fn()}
      />,
    );

    expect(screen.getByLabelText("Question")).toHaveValue("Second question");
  });

  it("uses the server-graded preview inside the editor", () => {
    render(
      <QuizEditorDialog
        open={true}
        value={createTrueFalseEntry("The Earth is round.")}
        submissionMode="server-graded"
        onOpenChange={vi.fn()}
        onCommit={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Submit answer" }));

    const status = screen.getByRole("status");
    expect(status).toHaveTextContent("Answer submitted.");
    expect(status).toHaveClass(
      "border-l-4",
      "border-blue-500",
      "bg-blue-50",
    );
  });

  it("resets the preview attempt when question settings change", async () => {
    render(
      <QuizEditorDialog
        open={true}
        value={createTrueFalseEntry("The Earth is round.")}
        submissionMode="server-graded"
        onOpenChange={vi.fn()}
        onCommit={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Submit answer" }));
    expect(screen.getByRole("status")).toBeInTheDocument();

    fireEvent.click(screen.getAllByRole("switch")[0]!);

    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Submit answer" }),
      ).toBeInTheDocument(),
    );
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });
});
