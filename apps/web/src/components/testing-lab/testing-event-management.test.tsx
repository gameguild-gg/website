import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";

global.ResizeObserver = class ResizeObserver {
  observe() {}
  unobserve() {}
  disconnect() {}
};
Element.prototype.scrollIntoView = vi.fn();

const mocks = vi.hoisted(() => ({
  beginReview: vi.fn(),
  refresh: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  usePathname: () => "/workspace/learning",
  useRouter: () => ({ refresh: mocks.refresh }),
}));

vi.mock("@/lib/testing-lab/events-actions", () => ({
  addTestingEventCommitteeMember: vi.fn(),
  assignTestedProjectToRegistration: vi.fn(),
  approveTestingEventApplication: vi.fn(),
  beginTestingEventApplicationReview: mocks.beginReview,
  configureTestingEventLearning: vi.fn(),
  createTestingEvent: vi.fn(),
  createTestingEventSlot: vi.fn(),
  archiveTestingEvent: vi.fn(),
  deleteTestingEvent: vi.fn(),
  deleteTestingEventSlot: vi.fn(),
  rejectTestingEventApplication: vi.fn(),
  removeTestingEventCommitteeMember: vi.fn(),
  restoreTestingEvent: vi.fn(),
  transitionTestingEvent: vi.fn(),
  updateTestingEventAttendance: vi.fn(),
  updateTestingEvent: vi.fn(),
  updateTestingEventSlot: vi.fn(),
  voteOnTestingEventApplication: vi.fn(),
  waitlistTestingEventApplication: vi.fn(),
}));

import {
  CreateTestingEventDialog,
  EditTestingEventDialog,
  ManageTestingEventSlotDialog,
  TestingEventApplications,
  TestingEventLifecycleActions,
} from "./testing-event-management";

describe("TestingEventApplications", () => {
  it("shows human labels and refreshes the SSR view after review starts", async () => {
    mocks.beginReview.mockResolvedValue({
      success: true,
      data: { id: "application-1" },
      message: "Application review started.",
    });

    render(
      <TestingEventApplications
        eventId="event-1"
        access={{ canManageApplications: true, canVote: false }}
        applications={[
          {
            id: "application-1",
            projectId: "project-1",
            submittedByUserId: "user-1",
            status: "Pending",
          },
        ]}
        slots={[]}
        projectLabels={{ "project-1": "Orbit Tactics" }}
        memberLabels={{ "user-1": "Ana Reviewer / ana@example.test" }}
      />,
    );

    expect(screen.getByText("Orbit Tactics")).toBeInTheDocument();
    expect(
      screen.getByText("Submitted by Ana Reviewer / ana@example.test"),
    ).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Review" }));

    await waitFor(() => {
      expect(mocks.beginReview).toHaveBeenCalledOnce();
      expect(mocks.refresh).toHaveBeenCalledOnce();
    });
    expect(screen.getByText("Application review started.")).toBeInTheDocument();
  });

  it("exposes the approval slot selector with an accessible name", async () => {
    render(
      <TestingEventApplications
        eventId="event-1"
        access={{ canManageApplications: true, canVote: false }}
        applications={[
          {
            id: "application-1",
            projectId: "project-1",
            submittedByUserId: "user-1",
            status: "UnderReview",
          },
        ]}
        slots={[
          {
            id: "slot-1",
            startsAt: "2026-08-02T14:00:00.000Z",
            campusName: "GameGuild Campus",
          },
        ]}
        projectLabels={{ "project-1": "Orbit Tactics" }}
        memberLabels={{ "user-1": "Ana Reviewer / ana@example.test" }}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Approve" }));

    expect(
      await screen.findByRole("combobox", { name: "Testing slot" }),
    ).toBeInTheDocument();
  });

  it("gives committee reviewers only the vote action", () => {
    render(
      <TestingEventApplications
        eventId="event-1"
        access={{ canManageApplications: false, canVote: true }}
        applications={[
          {
            id: "application-1",
            projectId: "project-1",
            submittedByUserId: "user-1",
            status: "UnderReview",
          },
        ]}
        slots={[]}
        projectLabels={{ "project-1": "Orbit Tactics" }}
        memberLabels={{ "user-1": "Ana Reviewer / ana@example.test" }}
      />,
    );

    expect(screen.getByRole("button", { name: "Vote" })).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Approve" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Waitlist" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Reject" }),
    ).not.toBeInTheDocument();
  });

  it("does not give an event manager a committee vote action", () => {
    render(
      <TestingEventApplications
        eventId="event-1"
        access={{ canManageApplications: true, canVote: false }}
        applications={[{ id: "application-1", status: "UnderReview" }]}
        slots={[]}
      />,
    );

    expect(screen.getByRole("button", { name: "Approve" })).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Vote" }),
    ).not.toBeInTheDocument();
  });

  it("uses range calendars and keeps the event schedule chronological", () => {
    render(<CreateTestingEventDialog defaultTimeZone="America/Sao_Paulo" />);

    fireEvent.click(screen.getByRole("button", { name: "New event" }));
    const field = (name: string) =>
      document.querySelector<HTMLInputElement>(`input[name="${name}"]`)!;
    const applicationsOpenAt = field("applicationsOpenAt");
    const applicationsCloseAt = field("applicationsCloseAt");
    const startsAt = field("startsAt");
    const endsAt = field("endsAt");

    expect(
      document.querySelector('input[type="datetime-local"]'),
    ).not.toBeInTheDocument();
    expect(applicationsOpenAt.value).not.toBe("");
    expect(applicationsCloseAt.value).not.toBe("");
    expect(startsAt.value).not.toBe("");
    expect(endsAt.value).not.toBe("");
    expect(new Date(applicationsCloseAt.value).valueOf()).toBeGreaterThan(
      new Date(applicationsOpenAt.value).valueOf(),
    );
    expect(new Date(startsAt.value).valueOf()).toBeGreaterThanOrEqual(
      new Date(applicationsCloseAt.value).valueOf(),
    );
    expect(new Date(endsAt.value).valueOf()).toBeGreaterThan(
      new Date(startsAt.value).valueOf(),
    );

    fireEvent.click(screen.getByRole("combobox", { name: "Time zone" }));
    fireEvent.change(screen.getByPlaceholderText("Search time zones…"), {
      target: { value: "America/New_York" },
    });
    fireEvent.click(screen.getByText("America/New_York"));
    expect(field("timeZoneId").value).toBe("America/New_York");

    fireEvent.click(screen.getByRole("button", { name: "Event schedule" }));
    const nextMinute = String(
      new Date(startsAt.value).getMinutes() + 1,
    ).padStart(2, "0");
    fireEvent.change(screen.getByLabelText("Start time"), {
      target: { value: `22:${nextMinute}` },
    });
    fireEvent.click(
      screen.getByRole("button", { name: "Apply event schedule" }),
    );

    expect(
      new Date(endsAt.value).valueOf() - new Date(startsAt.value).valueOf(),
    ).toBe(2 * 60 * 60 * 1000);

    fireEvent.change(screen.getByLabelText("Event name"), {
      target: { value: "Community playtest" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));

    expect(screen.getByRole("alertdialog")).toHaveTextContent(
      "Discard testing event draft?",
    );

    fireEvent.click(screen.getByRole("button", { name: "Keep editing" }));

    expect(screen.getByText("New testing event")).toBeInTheDocument();
  });

  it("uses the user's browser timezone when the lab still uses UTC", () => {
    const resolvedOptions = Intl.DateTimeFormat.prototype.resolvedOptions;
    const timeZone = vi
      .spyOn(Intl.DateTimeFormat.prototype, "resolvedOptions")
      .mockImplementation(function (this: Intl.DateTimeFormat) {
        return {
          ...resolvedOptions.call(this),
          timeZone: "America/Sao_Paulo",
        };
      });

    try {
      render(<CreateTestingEventDialog defaultTimeZone="UTC" />);
      fireEvent.click(screen.getByRole("button", { name: "New event" }));

      expect(
        screen.getByRole("combobox", { name: "Time zone" }),
      ).toHaveTextContent("America/Sao_Paulo");
      expect(
        document.querySelector<HTMLInputElement>('input[name="timeZoneId"]')
          ?.value,
      ).toBe("America/Sao_Paulo");
    } finally {
      timeZone.mockRestore();
    }
  });

  it("keeps the quick-create dialog focused on event identity and schedule", () => {
    render(<CreateTestingEventDialog />);

    fireEvent.click(screen.getByRole("button", { name: "New event" }));

    expect(screen.getByRole("textbox", { name: "Event name" })).toBeRequired();
    expect(
      screen.queryByText("Rules and instructions"),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("textbox", { name: "Purpose and tester brief" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByText("Require tester feedback"),
    ).not.toBeInTheDocument();
    expect(
      document.querySelector<HTMLInputElement>('input[name="requiresFeedback"]')
        ?.value,
    ).toBe("true");
  });

  it("presents the event decisions first and groups its two time windows", () => {
    render(<CreateTestingEventDialog defaultTimeZone="America/Sao_Paulo" />);

    fireEvent.click(screen.getByRole("button", { name: "New event" }));

    const eventName = screen.getByRole("textbox", { name: "Event name" });
    const eventFormat = screen.getByRole("combobox", {
      name: "Event format",
    });
    const projectReview = screen.getByRole("combobox", {
      name: "Project review",
    });
    expect(
      eventName.compareDocumentPosition(eventFormat) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    expect(
      eventFormat.compareDocumentPosition(projectReview) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    expect(eventFormat.closest("div")).toHaveClass(
      "sm:grid-cols-[7rem_minmax(0,1fr)]",
    );
    expect(projectReview.closest("div")).toHaveClass(
      "sm:grid-cols-[7rem_minmax(0,1fr)]",
    );

    const timeline = screen.getByRole("region", { name: "Schedule" });
    expect(
      within(timeline).getByRole("button", { name: "Application window" }),
    ).toHaveTextContent(
      /\d{2}\/\d{2}\/\d{4} · \d{2}:\d{2}(?:–\d{2}:\d{2}| → \d{2}\/\d{2}\/\d{4} · \d{2}:\d{2})/,
    );
    expect(
      within(timeline).getByRole("button", { name: "Event schedule" }),
    ).toHaveTextContent(
      /\d{2}\/\d{2}\/\d{4} · \d{2}:\d{2}(?:–\d{2}:\d{2}| → \d{2}\/\d{2}\/\d{4} · \d{2}:\d{2})/,
    );
    expect(within(timeline).getByText("Applications")).toBeInTheDocument();
    expect(within(timeline).getByText("Testing session")).toBeInTheDocument();
    expect(
      within(timeline).getByRole("combobox", { name: "Time zone" }),
    ).toHaveTextContent("America/Sao_Paulo");

    expect(
      screen.getByRole("combobox", { name: "Repeats" }),
    ).toBeInTheDocument();
    expect(screen.queryByLabelText("Start from")).not.toBeInTheDocument();
    expect(screen.getByRole("dialog")).toHaveClass("sm:max-w-lg");
    expect(screen.getByRole("dialog")).toHaveAttribute(
      "data-slot",
      "dialog-content",
    );
  });

  it("uses calendar-style repeat presets and keeps custom controls collapsed", async () => {
    const user = userEvent.setup();
    render(<CreateTestingEventDialog initialDate={new Date(2030, 7, 19)} />);

    await user.click(screen.getByRole("button", { name: "New event" }));

    const repeats = screen.getByRole("combobox", { name: "Repeats" });
    expect(repeats).toHaveTextContent("Does not repeat");
    expect(screen.queryByLabelText("Repeat every")).not.toBeInTheDocument();

    await user.click(repeats);
    expect(
      screen.getByRole("option", { name: "Weekly on Monday" }),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("option", { name: "Custom…" }));

    expect(screen.getByLabelText("Repeat every")).toBeInTheDocument();
    expect(
      screen.getByRole("combobox", { name: "Repeat unit" }),
    ).toHaveTextContent("Week(s)");
    expect(screen.getByLabelText("Number of events")).toHaveValue(4);
  });

  it("replaces the open action with a setup link while a draft is incomplete", () => {
    render(
      <TestingEventLifecycleActions
        event={{ id: "event-1", status: "Draft", configuration: undefined }}
      />,
    );

    expect(
      screen.getByRole("link", { name: "Complete setup" }),
    ).toHaveAttribute(
      "href",
      "/console/community/testing-lab/events/event-1/overview#event-configuration-heading",
    );
    expect(
      screen.queryByRole("button", { name: "Open applications" }),
    ).not.toBeInTheDocument();
  });
  it("opens as a controlled sheet with the calendar day prefilled", () => {
    render(
      <CreateTestingEventDialog
        open
        showTrigger={false}
        initialDate={new Date(2030, 7, 19)}
        onOpenChange={vi.fn()}
      />,
    );

    expect(
      screen.queryByRole("button", { name: "New event" }),
    ).not.toBeInTheDocument();
    expect(
      document.querySelector<HTMLInputElement>('input[name="startsAt"]')?.value,
    ).toMatch(/^2030-08-19T/);
  });

  it("shows API instants in the event timezone when editing an event", () => {
    render(
      <EditTestingEventDialog
        event={{
          id: "event-1",
          name: "Timezone-safe playtest",
          applicationsOpenAt: "2026-08-11T17:00:00Z",
          applicationsCloseAt: "2026-08-13T16:00:00Z",
          startsAt: "2026-08-13T17:00:00Z",
          endsAt: "2026-08-13T19:00:00Z",
          mode: "Online",
          approvalMode: "ManagerOnly",
          status: "Draft",
          requiresFeedback: true,
          timeZoneId: "America/Sao_Paulo",
        }}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Edit" }));

    expect(
      screen.getByRole("textbox", { name: "Purpose and tester brief" }),
    ).toBeInTheDocument();
    expect(screen.getByText("Require tester feedback")).toBeInTheDocument();
    expect(
      document.querySelector<HTMLInputElement>(
        'input[name="applicationsOpenAt"]',
      )?.value,
    ).toBe("2026-08-11T14:00");
    expect(
      document.querySelector<HTMLInputElement>(
        'input[name="applicationsCloseAt"]',
      )?.value,
    ).toBe("2026-08-13T13:00");
    expect(
      document.querySelector<HTMLInputElement>('input[name="startsAt"]')?.value,
    ).toBe("2026-08-13T14:00");
    expect(
      document.querySelector<HTMLInputElement>('input[name="endsAt"]')?.value,
    ).toBe("2026-08-13T16:00");
    expect(
      document.querySelector<HTMLInputElement>('input[name="timeZoneId"]')
        ?.value,
    ).toBe("America/Sao_Paulo");
  });

  it("preserves API wall-clock values when editing a slot", () => {
    const timezoneOffset = vi
      .spyOn(Date.prototype, "getTimezoneOffset")
      .mockReturnValue(180);
    render(
      <ManageTestingEventSlotDialog
        eventId="event-1"
        slot={{
          id: "slot-1",
          eventId: "event-1",
          mode: "InPerson",
          startsAt: "2026-08-13T17:30:00Z",
          endsAt: "2026-08-13T18:30:00Z",
          campusName: "QA Campus",
          roomName: "QA Room 101",
        }}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Edit slot" }));

    expect(
      document.querySelector<HTMLInputElement>('input[name="startsAt"]')?.value,
    ).toBe("2026-08-13T17:30");
    expect(
      document.querySelector<HTMLInputElement>('input[name="endsAt"]')?.value,
    ).toBe("2026-08-13T18:30");
    timezoneOffset.mockRestore();
  });
});
