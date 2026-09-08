import { render, screen } from "@testing-library/react";
import type { ComponentProps } from "react";
import { describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  getTestingEventWorkspaceData: vi.fn(),
  getMembers: vi.fn(),
  getTestingProjectOptions: vi.fn(),
  applications: vi.fn(),
}));

vi.mock("@/lib/testing-lab/events-queries", () => ({
  getTestingEventWorkspaceData: mocks.getTestingEventWorkspaceData,
}));
vi.mock("@/lib/community/queries/members", () => ({
  getMembers: mocks.getMembers,
}));
vi.mock("@/lib/testing-lab/queries", () => ({
  getTestingProjectOptions: mocks.getTestingProjectOptions,
}));
vi.mock("@/components/testing-lab/testing-event-management", () => ({
  TestingEventApplications: (props: {
    projectLabels: Record<string, string>;
    memberLabels: Record<string, string>;
  }) => {
    mocks.applications(props);
    return (
      <div>
        <span>{props.projectLabels["project-1"]}</span>
        <span>{props.memberLabels["user-1"]}</span>
      </div>
    );
  },
}));
vi.mock("@/i18n/navigation", () => ({
  Link: ({ href, ...props }: ComponentProps<"a">) => (
    <a href={String(href)} {...props} />
  ),
}));

import TestingEventApplicationsPage from "./page";

describe("Testing Event applications page", () => {
  it("resolves project and member labels with bulk SSR queries", async () => {
    mocks.getTestingEventWorkspaceData.mockResolvedValue({
      event: { id: "event-1", status: "Draft" },
      applications: [
        {
          id: "application-1",
          projectId: "project-1",
          submittedByUserId: "user-1",
          status: "Pending",
        },
      ],
      slots: [],
      applicationAccess: { canManageApplications: true, canVote: false },
      accessIssues: [],
    });
    mocks.getTestingProjectOptions.mockResolvedValue([
      { id: "project-1", title: "Orbit Tactics" },
    ]);
    mocks.getMembers.mockResolvedValue({
      members: [
        {
          id: "user-1",
          displayName: "Ana Reviewer",
          email: "ana@example.test",
        },
      ],
      total: 1,
    });

    render(
      await TestingEventApplicationsPage({
        params: Promise.resolve({ eventId: "event-1" }),
        searchParams: Promise.resolve({}),
      }),
    );

    expect(mocks.getMembers).toHaveBeenCalledWith({ page: 1, limit: 100 });
    expect(mocks.getTestingProjectOptions).toHaveBeenCalledOnce();
    expect(screen.getByText("Orbit Tactics")).toBeInTheDocument();
    expect(
      screen.getByText("Ana Reviewer / ana@example.test"),
    ).toBeInTheDocument();
    expect(mocks.applications).toHaveBeenCalledWith(
      expect.objectContaining({
        projectLabels: { "project-1": "Orbit Tactics" },
        memberLabels: { "user-1": "Ana Reviewer / ana@example.test" },
        access: { canManageApplications: true, canVote: false },
      }),
    );
  });

  it("surfaces application loading failures instead of presenting an empty list", async () => {
    mocks.getTestingEventWorkspaceData.mockResolvedValue({
      event: { id: "event-1", status: "ApplicationsOpen" },
      applications: [],
      slots: [],
      applicationAccess: null,
      accessIssues: [
        "Applications returned 403: Event manager or committee access is required.",
      ],
    });
    mocks.getTestingProjectOptions.mockResolvedValue([]);
    mocks.getMembers.mockResolvedValue({ members: [], total: 0 });

    render(
      await TestingEventApplicationsPage({
        params: Promise.resolve({ eventId: "event-1" }),
        searchParams: Promise.resolve({}),
      }),
    );

    expect(
      screen.getByText("Some data could not be loaded"),
    ).toBeInTheDocument();
    expect(mocks.applications).toHaveBeenLastCalledWith(
      expect.objectContaining({ access: null }),
    );
  });
});
