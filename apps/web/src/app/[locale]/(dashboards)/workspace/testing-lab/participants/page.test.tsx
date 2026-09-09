import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  getTestingParticipantDirectory: vi.fn(),
}));

vi.mock("@/lib/testing-lab/events-queries", () => ({
  getTestingParticipantDirectory: mocks.getTestingParticipantDirectory,
}));

vi.mock("@/components/testing-lab/testing-participant-filters", () => ({
  TestingParticipantFilters: ({
    search,
    status,
  }: {
    search?: string;
    status?: string;
  }) => (
    <div data-testid="participant-filters">
      {search}:{status}
    </div>
  ),
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

import TestingLabParticipantsPage from "./page";

describe("Testing Lab participants page", () => {
  it("renders the paged tenant directory with human identities and event context", async () => {
    mocks.getTestingParticipantDirectory.mockResolvedValue({
      accessIssues: [],
      directory: {
        items: [
          {
            registrationId: "registration-1",
            eventId: "event-1",
            eventName: "Friday campus lab",
            userId: "user-uuid",
            userName: "Ada Player",
            userEmail: "ada@example.com",
            mode: "InPerson",
            campusName: "Downtown campus",
            roomName: "Lab 4",
            startsAt: "2026-08-14T18:00:00.000Z",
            endsAt: "2026-08-14T19:00:00.000Z",
            status: "Registered",
            pendingFeedbackCount: 1,
          },
        ],
        totalCount: 26,
        registeredCount: 15,
        waitlistedCount: 3,
        checkedInCount: 2,
        attendedCount: 4,
        completedCount: 1,
        noShowCount: 1,
      },
    });

    render(
      await TestingLabParticipantsPage({
        searchParams: Promise.resolve({
          q: "Ada",
          status: "Registered",
          page: "2",
        }),
      }),
    );

    expect(
      screen.getByRole("heading", { name: "Testing Lab participants" }),
    ).toBeInTheDocument();
    expect(screen.getAllByText("Ada Player").length).toBeGreaterThan(0);
    expect(screen.getAllByText("ada@example.com").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Friday campus lab").length).toBeGreaterThan(0);
    expect(screen.queryByText("user-uuid")).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: /previous/i })).toHaveAttribute(
      "href",
      "/workspace/testing-lab/participants?q=Ada&status=Registered&page=1",
    );
    expect(mocks.getTestingParticipantDirectory).toHaveBeenCalledWith({
      search: "Ada",
      status: "Registered",
      skip: 25,
      take: 25,
    });
  });

  it("renders a useful empty state when no registrations match", async () => {
    mocks.getTestingParticipantDirectory.mockResolvedValue({
      accessIssues: [],
      directory: { items: [], totalCount: 0 },
    });

    render(
      await TestingLabParticipantsPage({
        searchParams: Promise.resolve({ q: "Nobody" }),
      }),
    );

    expect(
      screen.getByText("No participants match this view"),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/clear the filters or wait for members/i),
    ).toBeInTheDocument();
  });
});
