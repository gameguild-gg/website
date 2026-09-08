import type { TestingLabTestingEventProjection } from "@game-guild/client";
import { describe, expect, it } from "vitest";

import {
  calendarEventSegments,
  calendarRange,
  parseCalendarView,
  shiftCalendarAnchor,
} from "./calendar";

describe("Testing Lab calendar helpers", () => {
  it("uses Month as the safe default calendar view", () => {
    expect(parseCalendarView(null)).toBe("month");
    expect(parseCalendarView("week")).toBe("week");
    expect(parseCalendarView("unexpected")).toBe("month");
  });

  it("renders only the Monday-first weeks needed by a month", () => {
    const range = calendarRange(new Date(2026, 8, 10), "month", true);
    const weekdaysOnly = calendarRange(new Date(2026, 8, 10), "month", false);

    expect(range.days).toHaveLength(35);
    expect(range.days[0]).toEqual(new Date(2026, 7, 31));
    expect(range.days.at(-1)).toEqual(new Date(2026, 9, 4));
    expect(range.days[0]?.getDay()).toBe(1);
    expect(weekdaysOnly.days).toHaveLength(25);
    expect(
      weekdaysOnly.days.every(
        (day) => day.getDay() !== 0 && day.getDay() !== 6,
      ),
    ).toBe(true);
  });

  it("moves the anchor by the visible calendar view", () => {
    const anchor = new Date(2026, 7, 10);

    expect(shiftCalendarAnchor(anchor, "month", 1)).toEqual(
      new Date(2026, 8, 10),
    );
    expect(shiftCalendarAnchor(anchor, "week", -1)).toEqual(
      new Date(2026, 7, 3),
    );
    expect(shiftCalendarAnchor(anchor, "3days", 1)).toEqual(
      new Date(2026, 7, 13),
    );
  });

  it("places multi-day Testing Lab events on every visible calendar day they occupy", () => {
    const event = {
      id: "event-1",
      name: "Campus playtest",
      startsAt: "2026-08-10T18:00:00.000Z",
      endsAt: "2026-08-12T20:00:00.000Z",
      status: "Scheduled",
    } as TestingLabTestingEventProjection;
    const range = calendarRange(new Date(2026, 7, 10), "week", true);

    const segments = calendarEventSegments([event], range);

    expect(segments).toHaveLength(3);
    expect(segments.map((segment) => segment.dayKey)).toEqual([
      "2026-08-10",
      "2026-08-11",
      "2026-08-12",
    ]);
  });
});
