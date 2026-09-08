import { fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { TestingLabTestingEventProjection } from "@game-guild/client";
import { forwardRef, type ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";

vi.mock("@/i18n/navigation", () => ({
  Link: forwardRef<HTMLAnchorElement, { children: ReactNode; href: string }>(
    function MockLink({ children, href, ...rest }, ref) {
      return (
        <a ref={ref} href={href} {...rest}>
          {children}
        </a>
      );
    },
  ),
}));

vi.mock("./testing-event-management", () => ({
  CreateTestingEventDialog: ({
    open,
    initialDate,
  }: {
    open?: boolean;
    initialDate?: Date;
  }) =>
    open ? (
      <div role="dialog">
        Creating event for{" "}
        {initialDate?.toISOString().slice(0, 10) ?? "no date"}
      </div>
    ) : null,
}));

import { TestingLabCalendar } from "./testing-lab-calendar";

const events = [
  {
    id: "event-1",
    name: "Campus playtest",
    description: "Hands-on lab for the new combat build.",
    status: "Scheduled",
    mode: "InPerson",
    startsAt: "2030-08-10T18:00:00.000Z",
    endsAt: "2030-08-10T20:00:00.000Z",
  },
  {
    id: "event-2",
    name: "Remote build review",
    description: "Online review for approved community projects.",
    status: "Active",
    mode: "Online",
    startsAt: "2030-08-12T18:00:00.000Z",
    endsAt: "2030-08-12T20:00:00.000Z",
  },
] as TestingLabTestingEventProjection[];

const eventAnalytics = [
  { eventId: "event-1", registeredTesters: 3, capacity: 10, fillRate: 30 },
  { eventId: "event-2", registeredTesters: 8, capacity: 8, fillRate: 100 },
];

describe("TestingLabCalendar", () => {
  it("opens as a month calendar and identifies event mode and capacity", () => {
    render(
      <TestingLabCalendar
        events={events}
        eventAnalytics={eventAnalytics}
        initialDate={new Date(2030, 7, 10)}
      />,
    );

    expect(
      screen.getByRole("combobox", { name: "Calendar view" }),
    ).toHaveTextContent("Month");
    expect(
      screen.getByRole("combobox", { name: "Calendar view" }),
    ).toHaveAttribute("data-slot", "select-trigger");
    expect(
      screen.getByRole("searchbox", { name: "Search events" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("combobox", { name: "Filter events" }),
    ).toHaveTextContent("All statuses");
    expect(
      screen.getByRole("combobox", { name: "Filter events" }),
    ).toHaveAttribute("data-slot", "select-trigger");
    expect(
      screen.queryByRole("button", { name: "New event" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Hide weekends" }),
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole("toolbar", { name: "Testing Lab calendar controls" }),
    ).toBeInTheDocument();
    expect(screen.getByText("Campus playtest")).toBeInTheDocument();
    expect(screen.getByLabelText("In-person event")).toBeInTheDocument();
    expect(screen.getByLabelText("Online event")).toBeInTheDocument();
    expect(screen.getByText("3/10")).toBeInTheDocument();
    expect(screen.getByText("8/8")).toBeInTheDocument();
  });

  it("fills its work area without an outer card frame", () => {
    render(
      <TestingLabCalendar
        events={events}
        eventAnalytics={eventAnalytics}
        initialDate={new Date(2030, 7, 10)}
      />,
    );

    const calendar = screen.getByRole("region", {
      name: "Testing Lab calendar",
    });

    expect(calendar).toHaveClass("h-full");
    expect(calendar).not.toHaveClass("border", "rounded-lg");
  });

  it("keeps an empty month grid unobstructed", () => {
    render(
      <TestingLabCalendar events={[]} initialDate={new Date(2030, 7, 10)} />,
    );

    expect(
      screen.queryByText("No events in this period"),
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Create event on August 19, 2030" }),
    ).toBeInTheDocument();
  });

  it("offers the Google Calendar view set and can switch to the schedule", async () => {
    const user = userEvent.setup();
    render(
      <TestingLabCalendar
        events={events}
        eventAnalytics={eventAnalytics}
        initialDate={new Date(2030, 7, 10)}
      />,
    );

    const view = screen.getByRole("combobox", { name: "Calendar view" });
    await user.click(view);
    expect(
      screen.getAllByRole("option").map((option) => option.textContent),
    ).toEqual(["Day", "Week", "Month", "Year", "Schedule", "3 days"]);
    await user.click(screen.getByRole("option", { name: "Schedule" }));

    expect(
      screen.getByRole("heading", { name: "Schedule" }),
    ).toBeInTheDocument();
    expect(
      within(
        screen.getByRole("region", { name: "Testing Lab schedule" }),
      ).getByText("Scheduled"),
    ).toBeInTheDocument();
  });

  it("opens event creation for the selected calendar day", () => {
    render(
      <TestingLabCalendar
        events={events}
        eventAnalytics={eventAnalytics}
        initialDate={new Date(2030, 7, 10)}
      />,
    );

    fireEvent.click(
      screen.getByRole("button", { name: "Create event on August 19, 2030" }),
    );

    expect(screen.getByRole("dialog")).toHaveTextContent(
      "Creating event for 2030-08-19",
    );
  });

  it("shows operational event details on hover without opening the event page", async () => {
    const user = userEvent.setup();
    render(
      <TestingLabCalendar
        events={events}
        eventAnalytics={eventAnalytics}
        initialDate={new Date(2030, 7, 10)}
      />,
    );

    await user.hover(screen.getByRole("link", { name: /Campus playtest/i }));

    expect(
      await screen.findByText("Hands-on lab for the new combat build."),
    ).toBeInTheDocument();
    expect(screen.getByText("7 tester spots available")).toBeInTheDocument();
  });

  it("filters the visible calendar without changing the event directory", async () => {
    const user = userEvent.setup();
    render(
      <TestingLabCalendar
        events={events}
        eventAnalytics={eventAnalytics}
        initialDate={new Date(2030, 7, 10)}
      />,
    );

    await user.type(
      screen.getByRole("searchbox", { name: "Search events" }),
      "remote",
    );

    expect(screen.queryByText("Campus playtest")).not.toBeInTheDocument();
    expect(screen.getByText("Remote build review")).toBeInTheDocument();

    await user.clear(screen.getByRole("searchbox", { name: "Search events" }));
    await user.click(screen.getByRole("combobox", { name: "Filter events" }));
    await user.click(screen.getByRole("option", { name: "Scheduled" }));

    expect(screen.getByText("Campus playtest")).toBeInTheDocument();
    expect(screen.queryByText("Remote build review")).not.toBeInTheDocument();
  });

  it("adds a synchronized planning sidebar with real period insights", () => {
    render(
      <TestingLabCalendar
        events={events}
        eventAnalytics={eventAnalytics}
        initialDate={new Date(2030, 7, 10)}
      />,
    );

    const sidebar = screen.getByRole("complementary", {
      name: "Testing Lab planning",
    });

    expect(within(sidebar).getByText("August 2030")).toBeInTheDocument();
    expect(within(sidebar).getByText("2 events")).toBeInTheDocument();
    expect(within(sidebar).getByText("4h")).toBeInTheDocument();
    expect(within(sidebar).getByText("Testing time")).toBeInTheDocument();
    expect(within(sidebar).getByText("11 of 18 seats filled")).toBeInTheDocument();
    expect(within(sidebar).getByRole("checkbox", { name: "Online" })).toBeChecked();
    expect(
      within(sidebar).getByRole("checkbox", { name: "In-person" }),
    ).toBeChecked();
  });

  it("filters the calendar by event calendar and can collapse the planning sidebar", async () => {
    const user = userEvent.setup();
    render(
      <TestingLabCalendar
        events={events}
        eventAnalytics={eventAnalytics}
        initialDate={new Date(2030, 7, 10)}
      />,
    );

    const sidebar = screen.getByRole("complementary", {
      name: "Testing Lab planning",
    });
    await user.click(
      within(sidebar).getByRole("checkbox", { name: "Online" }),
    );

    expect(screen.queryByText("Remote build review")).not.toBeInTheDocument();
    expect(screen.getByText("Campus playtest")).toBeInTheDocument();
    expect(within(sidebar).getByText("1 event")).toBeInTheDocument();

    await user.click(
      screen.getByRole("button", { name: "Hide planning sidebar" }),
    );
    expect(
      screen.queryByRole("complementary", { name: "Testing Lab planning" }),
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Show planning sidebar" }),
    ).toBeInTheDocument();
  });

  it("keeps the mini calendar synchronized with the main calendar", async () => {
    const user = userEvent.setup();
    render(
      <TestingLabCalendar events={events} initialDate={new Date(2030, 7, 10)} />,
    );

    const sidebar = screen.getByRole("complementary", {
      name: "Testing Lab planning",
    });
    await user.click(
      within(sidebar).getByRole("button", { name: "Go to the Next Month" }),
    );

    expect(
      screen.getByRole("heading", { name: "September 2030" }),
    ).toBeInTheDocument();
  });

  it("uses the schedule as the mobile default view", () => {
    vi.stubGlobal(
      "matchMedia",
      vi.fn(() => ({
        matches: true,
        media: "(max-width: 767px)",
        onchange: null,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        addListener: vi.fn(),
        removeListener: vi.fn(),
        dispatchEvent: vi.fn(),
      })),
    );

    render(
      <TestingLabCalendar
        events={events}
        eventAnalytics={eventAnalytics}
        initialDate={new Date(2030, 7, 10)}
      />,
    );

    expect(
      screen.getByRole("combobox", { name: "Calendar view" }),
    ).toHaveTextContent("Schedule");
    vi.unstubAllGlobals();
  });
});
