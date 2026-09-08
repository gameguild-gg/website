import "@testing-library/jest-dom/vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { LearningAssessmentsGradingQueueItem } from "@game-guild/client";
import { SpeedgraderShell } from "./speedgrader-shell";

const routerMocks = vi.hoisted(() => ({
  push: vi.fn(),
  replace: vi.fn(),
  refresh: vi.fn(),
  back: vi.fn(),
  forward: vi.fn(),
  prefetch: vi.fn(),
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

vi.mock("@/i18n/navigation", () => ({
  Link: ({
    href,
    children,
    ...rest
  }: React.AnchorHTMLAttributes<HTMLAnchorElement> & { href: string }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
  usePathname: () => "/speedgrader/assessments/assessment-1",
  useRouter: () => routerMocks,
}));

vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams("course=prog-1"),
}));

// react-resizable-panels mounts a document-level capture pointerdown listener
// that hit-tests degenerate 0x0 jsdom rects and preventDefaults every pointer
// event, breaking Radix Select interaction. The shell test targets navigation,
// not resizing.
vi.mock("@game-guild/ui/components/resizable", () => ({
  ResizablePanelGroup: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="resizable-stub">{children}</div>
  ),
  ResizablePanel: ({ children }: { children?: React.ReactNode }) => (
    <div>{children}</div>
  ),
  ResizableHandle: () => <div role="separator" />,
}));

const individualItems = [
  {
    submissionId: "sub-1",
    canonicalSubmissionId: "sub-1",
    userId: "user-1",
    displayName: "Ada Lovelace",
    attemptNumber: 1,
    status: "Submitted",
    isGroup: false,
  },
  {
    submissionId: "sub-2",
    canonicalSubmissionId: "sub-2",
    userId: "user-2",
    displayName: "Grace Hopper",
    attemptNumber: 2,
    status: "Graded",
    isGroup: false,
  },
  {
    submissionId: "sub-3",
    canonicalSubmissionId: "sub-3",
    userId: "user-3",
    displayName: "Alan Turing",
    attemptNumber: 1,
    status: "Late",
    isGroup: false,
  },
] satisfies LearningAssessmentsGradingQueueItem[];

const groupItems = [
  {
    submissionId: "sub-g1",
    canonicalSubmissionId: "sub-g1",
    userId: "user-1",
    displayName: "Ada Lovelace",
    groupId: "group-1",
    groupName: "Team Rocket",
    memberNames: ["Ada Lovelace", "Grace Hopper"],
    attemptNumber: 1,
    status: "Submitted",
    isGroup: true,
  },
] satisfies LearningAssessmentsGradingQueueItem[];

const itemsWithAttemptCount = [
  {
    submissionId: "sub-1",
    canonicalSubmissionId: "sub-1",
    userId: "user-1",
    displayName: "Ada Lovelace",
    attemptNumber: 3,
    attemptCount: 3,
    status: "Submitted",
    isGroup: false,
  },
] satisfies LearningAssessmentsGradingQueueItem[];

function renderShell(
  items: LearningAssessmentsGradingQueueItem[],
  props: Partial<React.ComponentProps<typeof SpeedgraderShell>> = {},
) {
  return render(
    <SpeedgraderShell
      assessmentTitle="Final Project"
      assessmentId="assessment-1"
      courseSlug="prog-1"
      items={items}
      needsGrading={2}
      initialIndex={0}
      {...props}
    />,
  );
}

describe("SpeedgraderShell", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders all items in the picker with name — attempt — status labels", async () => {
    const user = userEvent.setup();
    renderShell(individualItems);

    expect(screen.getByTestId("item-counter")).toHaveTextContent("1 of 3");
    expect(screen.getByTestId("needs-grading-badge")).toHaveTextContent("2");

    await user.click(screen.getByRole("combobox", { name: /submission/i }));
    expect(
      await screen.findByRole("option", { name: "Ada Lovelace — attempt 1 — Submitted" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("option", { name: "Grace Hopper — attempt 2 — Graded" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("option", { name: "Alan Turing — attempt 1 — Late" }),
    ).toBeInTheDocument();
  });

  it("prev from the first item wraps to the last and writes ?nav=", async () => {
    const user = userEvent.setup();
    renderShell(individualItems);

    await user.click(screen.getByRole("button", { name: /previous submission/i }));

    expect(screen.getByTestId("item-counter")).toHaveTextContent("3 of 3");
    expect(screen.getByTestId("viewer-slot")).toHaveTextContent("Alan Turing");
    expect(routerMocks.replace).toHaveBeenCalledWith(
      "/speedgrader/assessments/assessment-1?course=prog-1&nav=2",
      { scroll: false },
    );
  });

  it("next from the last item wraps to the first and writes ?nav=", async () => {
    const user = userEvent.setup();
    renderShell(individualItems, { initialIndex: 2 });

    expect(screen.getByTestId("item-counter")).toHaveTextContent("3 of 3");

    await user.click(screen.getByRole("button", { name: /next submission/i }));

    expect(screen.getByTestId("item-counter")).toHaveTextContent("1 of 3");
    expect(screen.getByTestId("viewer-slot")).toHaveTextContent("Ada Lovelace");
    expect(routerMocks.replace).toHaveBeenCalledWith(
      "/speedgrader/assessments/assessment-1?course=prog-1&nav=0",
      { scroll: false },
    );
  });

  it("next advances without wrapping in the middle", async () => {
    const user = userEvent.setup();
    renderShell(individualItems);

    await user.click(screen.getByRole("button", { name: /next submission/i }));

    expect(screen.getByTestId("item-counter")).toHaveTextContent("2 of 3");
    expect(screen.getByTestId("viewer-slot")).toHaveTextContent("Grace Hopper");
    expect(routerMocks.replace).toHaveBeenCalledWith(
      "/speedgrader/assessments/assessment-1?course=prog-1&nav=1",
      { scroll: false },
    );
  });

  it("picker selection switches the current item", async () => {
    const user = userEvent.setup();
    renderShell(individualItems);

    await user.click(screen.getByRole("combobox", { name: /submission/i }));
    await user.click(await screen.findByRole("option", { name: /Grace Hopper/i }));

    await waitFor(() => {
      expect(screen.getByTestId("item-counter")).toHaveTextContent("2 of 3");
    });
    expect(screen.getByTestId("viewer-slot")).toHaveTextContent("Grace Hopper");
    expect(routerMocks.replace).toHaveBeenCalledWith(
      "/speedgrader/assessments/assessment-1?course=prog-1&nav=1",
      { scroll: false },
    );
  });

  it("defaults a non-numeric initial index to the first item", () => {
    renderShell(individualItems, { initialIndex: Number.NaN });
    expect(screen.getByTestId("item-counter")).toHaveTextContent("1 of 3");
  });

  it("clamps an out-of-range initial index into range", () => {
    renderShell(individualItems, { initialIndex: 99 });
    expect(screen.getByTestId("item-counter")).toHaveTextContent("3 of 3");
  });

  it("labels group items with group name and member names", () => {
    renderShell(groupItems);

    expect(screen.getByTestId("viewer-slot")).toHaveTextContent(
      "Group: Team Rocket (Ada Lovelace, Grace Hopper)",
    );
  });

  it("shows attempt X of Y when attemptCount is present", async () => {
    const user = userEvent.setup();
    renderShell(itemsWithAttemptCount);

    await user.click(screen.getByRole("combobox", { name: /submission/i }));
    expect(
      await screen.findByRole("option", {
        name: "Ada Lovelace — attempt 3 of 3 — Submitted",
      }),
    ).toBeInTheDocument();
  });

  it("renders the empty state with a back link when there are no items", () => {
    renderShell([], { needsGrading: 0 });

    expect(screen.getByTestId("speedgrader-empty")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /back to submissions/i })).toHaveAttribute(
      "href",
      "/dashboard/learning/courses/prog-1/assessments/assessment-1/submissions",
    );
    expect(screen.queryByRole("combobox", { name: /submission/i })).not.toBeInTheDocument();
  });

  it("links back to the assessment submissions page", () => {
    renderShell(individualItems);

    expect(screen.getByRole("link", { name: /back/i })).toHaveAttribute(
      "href",
      "/dashboard/learning/courses/prog-1/assessments/assessment-1/submissions",
    );
  });

  it("renders viewer and grading panel slots inside the resizable body", () => {
    renderShell(individualItems);

    expect(screen.getByTestId("viewer-slot")).toBeInTheDocument();
    expect(screen.getByTestId("grading-slot")).toBeInTheDocument();
  });
});
