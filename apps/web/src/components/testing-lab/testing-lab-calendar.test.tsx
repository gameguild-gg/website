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

    expect(screen.getByRole("combobox", { name: "Calendar view" })).toHaveValue(
      "month",
    );
    expect(
      screen.getByRole("searchbox", { name: "Search events" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Filter events" })).toHaveValue(
      "all",
    );
    expect(
      screen.queryByRole("button", { name: "New event" }),
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Hide weekends" }),
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
      <TestingLabCalendar
        events={[]}
        initialDate={new Date(2030, 7, 10)}
      />,
    );

    expect(screen.queryByText("No events in this period")).not.toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Create event on August 19, 2030" }),
    ).toBeInTheDocument();
  });

  it("offers the Google Calendar view set and can switch to the schedule", () => {
    render(
      <TestingLabCalendar
        events={events}
        eventAnalytics={eventAnalytics}
        initialDate={new Date(2030, 7, 10)}
      />,
    );

    const view = screen.getByRole("combobox", { name: "Calendar view" });
    expect(
      [...view.querySelectorAll("option")].map((option) => option.value),
    ).toEqual(["day", "week", "month", "year", "schedule", "3days"]);

    expect(
      screen.getByRole("button", { name: "Hide weekends" }),
    ).toBeInTheDocument();

    fireEvent.change(view, { target: { value: "schedule" } });

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
    fireEvent.change(screen.getByRole("combobox", { name: "Filter events" }), {
      target: { value: "Scheduled" },
    });

    expect(screen.getByText("Campus playtest")).toBeInTheDocument();
    expect(screen.queryByText("Remote build review")).not.toBeInTheDocument();
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

    expect(screen.getByRole("combobox", { name: "Calendar view" })).toHaveValue("schedule");
    vi.unstubAllGlobals();
  });
});
