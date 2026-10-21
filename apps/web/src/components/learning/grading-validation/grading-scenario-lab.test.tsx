import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { GradingScenarioLabModel } from "@/lib/grading-scenarios/contracts";

vi.mock("@/lib/grading-scenarios/actions", () => ({
  inspectGradingScenarioAction: vi.fn(),
  prepareGradingScenarioAction: vi.fn(),
  resetGradingScenarioAction: vi.fn(),
  verifyGradingScenarioAction: vi.fn(),
}));

import { GradingScenarioLab } from "./grading-scenario-lab";

const NON_SECRET_TEST_PASSWORD = ["scenario", "fixture"].join("-");

const model: GradingScenarioLabModel = {
  adapters: [
    {
      key: "code",
      title: "Code grading",
      targetLabel: "assignment",
      targetLabelPlural: "assignments",
      defaultScenarioKey: "compiler-review",
      checkpoints: ["authoring-ready", "learner-ready", "released"],
      scenarios: [
        {
          key: "compiler-review",
          title: "Compiler and instructor review",
          subject: "individual",
          targets: [
            {
              key: "rust-exercise",
              title: "Rust exercise",
              kind: "code",
              workflow: ["AutomatedReview", "InstructorReview"],
              capability: "code-answer/v1",
              validationCaseId: "CODE-REVIEW-001",
              maxCheckpoint: "released",
            },
          ],
        },
      ],
      capabilityCatalog: [
        {
          id: "CODE-REVIEW-001",
          suite: "review",
          capability: "Compiler and instructor review",
          automation: "browser",
          requirement: "required",
          status: "partial",
          owner: "code",
          reason: null,
          tracking: null,
          targetKey: "rust-exercise",
        },
      ],
    },
  ],
  states: [
    {
      adapterKey: "code",
      scenarioKey: "compiler-review",
      health: "ready",
      prepared: true,
      runId: "run-code-1",
      marker: "scenario-code-1",
      declaredCheckpoint: "learner-ready",
      confirmedCheckpoint: "learner-ready",
      nextCheckpoint: "released",
      updatedAt: "2026-10-19T12:00:00.000Z",
      diagnostic: null,
      targets: [
        {
          key: "rust-exercise",
          title: "Rust exercise",
          declaredCheckpoint: "learner-ready",
          confirmedCheckpoint: "learner-ready",
        },
      ],
      personas: [
        {
          key: "instructor",
          label: "Instructor",
          email: "instructor1@scenario.test",
          username: "instructor-1",
          password: NON_SECRET_TEST_PASSWORD,
          userId: "user-1",
        },
      ],
      links: [
        {
          key: "workspace",
          label: "Assignment workspace",
          href: "http://localhost:3000/workspace/code",
          audience: "Instructor",
          targetKey: "rust-exercise",
        },
      ],
      checks: [
        {
          name: "Assignment persisted",
          ok: true,
          detail: "The adapter confirmed its persisted state.",
        },
      ],
    },
  ],
};

describe("GradingScenarioLab", () => {
  it("renders a non-quiz adapter through the generic grading contracts", () => {
    render(<GradingScenarioLab initialModel={model} />);

    expect(screen.getByLabelText("Grading adapter")).toHaveValue("code");
    expect(screen.getByRole("heading", { name: /assignments/i })).toBeVisible();
    expect(screen.getAllByText("Rust exercise").length).toBeGreaterThan(0);
    expect(
      screen.getByRole("link", { name: /Assignment workspace/ }),
    ).toHaveAttribute("href", "http://localhost:3000/workspace/code");
    expect(screen.getByText("Instructor")).toBeVisible();
    expect(screen.getByText("Assignment persisted")).toBeVisible();
    expect(screen.getByText("CODE-REVIEW-001")).toBeVisible();
    expect(screen.queryByText(/quiz/i)).not.toBeInTheDocument();
  });
});
