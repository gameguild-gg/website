import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  getTestingFeedbackDirectory: vi.fn(),
}));

vi.mock("@/lib/testing-lab", () => ({
  getTestingFeedbackDirectory: mocks.getTestingFeedbackDirectory,
}));

vi.mock("@/lib/testing-lab/actions", () => ({
  rateTestingFeedback: vi.fn(),
  reportTestingFeedback: vi.fn(),
}));

import TestingLabFeedbackPage from "./page";

describe("Testing Lab feedback directory", () => {
  it("exposes accessible names for every directory filter", async () => {
    mocks.getTestingFeedbackDirectory.mockResolvedValue({
      items: [],
      totalCount: 0,
      skip: 0,
      take: 20,
      accessIssues: [],
    });

    render(await TestingLabFeedbackPage({ searchParams: Promise.resolve({}) }));

    expect(
      screen.getByRole("textbox", { name: "Search feedback" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("combobox", { name: "Filter by source" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("combobox", { name: "Filter by report status" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("combobox", { name: "Filter by quality" }),
    ).toBeInTheDocument();
  });

  it("renders the overall rating on the Testing Lab ten-point scale", async () => {
    mocks.getTestingFeedbackDirectory.mockResolvedValue({
      items: [
        {
          id: "feedback-1",
          source: "Event",
          eventName: "Friday playtest",
          testingContext: "Online",
          userName: "Ana Tester",
          userId: "tester-1",
          overallRating: 9,
          feedbackData: "Clear controls",
          isReported: false,
          qualityRating: null,
        },
      ],
      totalCount: 1,
      skip: 0,
      take: 20,
      accessIssues: [],
    });

    render(await TestingLabFeedbackPage({ searchParams: Promise.resolve({}) }));

    expect(screen.getByText("9 / 10")).toBeInTheDocument();
  });
});
