import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  getTestingLabAnalytics: vi.fn(),
}));

vi.mock("@/lib/testing-lab", () => ({
  getTestingLabAnalytics: mocks.getTestingLabAnalytics,
}));

vi.mock("@/i18n/navigation", () => ({
  Link: ({
    children,
    href,
    ...rest
  }: {
    children: ReactNode;
    href: string;
  }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

import TestingLabAnalyticsPage from "./page";

describe("Testing Lab analytics page", () => {
  it("renders comparison, trend, event drill-down, and the selected export period", async () => {
    mocks.getTestingLabAnalytics.mockResolvedValue({
      fromDate: "2026-07-01T00:00:00.000Z",
      toDate: "2026-08-01T00:00:00.000Z",
      generatedAt: "2026-08-01T12:00:00.000Z",
      current: {
        events: 6,
        completedEvents: 4,
        applications: 12,
        approvedProjects: 8,
        registeredTesters: 20,
        attendedTesters: 16,
        feedback: 14,
        averageRating: 8.5,
        recommendationRate: 86,
        capacity: 40,
        fillRate: 50,
      },
      previous: {
        events: 2,
        completedEvents: 1,
        applications: 7,
        approvedProjects: 3,
        registeredTesters: 10,
        attendedTesters: 7,
        feedback: 5,
        averageRating: 7.2,
        recommendationRate: 60,
        capacity: 20,
        fillRate: 50,
      },
      locations: { total: 3, active: 2 },
      trend: [
        {
          date: "2026-07-10T00:00:00.000Z",
          events: 1,
          applications: 4,
          registrations: 8,
          attendance: 6,
          feedback: 5,
        },
        {
          date: "2026-07-17",
          events: 2,
          applications: 8,
          registrations: 12,
          attendance: 10,
          feedback: 9,
        },
      ],
      events: [
        {
          eventId: "event-1",
          name: "July campus playtest",
          status: "Published",
          mode: "InPerson",
          startsAt: "2026-07-17T18:00:00.000Z",
          applications: 8,
          approvedProjects: 5,
          registeredTesters: 12,
          attendedTesters: 10,
          feedback: 9,
          averageRating: 8.5,
          capacity: 20,
          fillRate: 60,
        },
      ],
      accessIssues: [],
    });

    render(
      await TestingLabAnalyticsPage({
        searchParams: Promise.resolve({ from: "2026-07-01", to: "2026-07-31" }),
      }),
    );

    expect(mocks.getTestingLabAnalytics).toHaveBeenCalledWith({
      fromDate: "2026-07-01T00:00:00.000Z",
      toDate: "2026-08-01T00:00:00.000Z",
      includeComparison: true,
    });
    expect(
      screen.getByRole("heading", { name: "Testing Lab analytics" }),
    ).toBeInTheDocument();
    expect(screen.getByText("+4 vs previous period")).toBeInTheDocument();
    expect(screen.getByText("Activity trend")).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: /July campus playtest/i }),
    ).toHaveAttribute("href", "/workspace/testing-lab/events/event-1/overview");
    expect(screen.getByRole("link", { name: /Export CSV/i })).toHaveAttribute(
      "href",
      "/api/testing-lab/analytics/export?from=2026-07-01&to=2026-07-31",
    );
  });

  it("renders a useful empty state when the selected period has no activity", async () => {
    mocks.getTestingLabAnalytics.mockResolvedValue({
      fromDate: "2026-07-01T00:00:00.000Z",
      toDate: "2026-08-01T00:00:00.000Z",
      generatedAt: null,
      current: {
        events: 0,
        completedEvents: 0,
        applications: 0,
        approvedProjects: 0,
        registeredTesters: 0,
        attendedTesters: 0,
        feedback: 0,
        averageRating: null,
        recommendationRate: null,
        capacity: 0,
        fillRate: 0,
      },
      previous: null,
      locations: { total: 0, active: 0 },
      trend: [],
      events: [],
      accessIssues: [],
    });

    render(
      await TestingLabAnalyticsPage({ searchParams: Promise.resolve({}) }),
    );

    expect(
      screen.getByText("No Testing Lab activity in this period"),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Manage events" })).toHaveAttribute(
      "href",
      "/workspace/testing-lab/events",
    );
  });
});
