import type { TestingLabTestingEventProjection } from "@game-guild/client";
import {
  addDays,
  addMonths,
  addWeeks,
  addYears,
  eachDayOfInterval,
  endOfDay,
  endOfMonth,
  endOfWeek,
  endOfYear,
  format,
  isValid,
  startOfDay,
  startOfMonth,
  startOfWeek,
  startOfYear,
} from "date-fns";

export const calendarViews = [
  "day",
  "week",
  "month",
  "year",
  "schedule",
  "3days",
] as const;
export type CalendarView = (typeof calendarViews)[number];

export interface CalendarRange {
  start: Date;
  end: Date;
  days: Date[];
}

export interface CalendarEventSegment {
  event: TestingLabTestingEventProjection;
  day: Date;
  dayKey: string;
  startsOnDay: boolean;
  endsOnDay: boolean;
}

export function parseCalendarView(
  value: string | null | undefined,
): CalendarView {
  return calendarViews.includes(value as CalendarView)
    ? (value as CalendarView)
    : "month";
}

function visibleDays(start: Date, end: Date, showWeekends: boolean) {
  const days = eachDayOfInterval({ start, end });
  return showWeekends
    ? days
    : days.filter((day) => day.getDay() !== 0 && day.getDay() !== 6);
}

export function calendarRange(
  anchor: Date,
  view: CalendarView,
  showWeekends: boolean,
): CalendarRange {
  const day = startOfDay(anchor);
  let start = day;
  let end = day;

  switch (view) {
    case "3days":
      end = addDays(day, 2);
      break;
    case "week":
      start = startOfWeek(day, { weekStartsOn: 1 });
      end = endOfWeek(day, { weekStartsOn: 1 });
      break;
    case "month":
      start = startOfWeek(startOfMonth(day), { weekStartsOn: 1 });
      end = endOfWeek(endOfMonth(day), { weekStartsOn: 1 });
      break;
    case "year":
      start = startOfYear(day);
      end = endOfYear(day);
      break;
    case "schedule":
      end = addDays(day, 89);
      break;
  }

  return { start, end, days: visibleDays(start, end, showWeekends) };
}

export function shiftCalendarAnchor(
  anchor: Date,
  view: CalendarView,
  direction: -1 | 1,
): Date {
  switch (view) {
    case "day":
      return addDays(anchor, direction);
    case "3days":
      return addDays(anchor, 3 * direction);
    case "week":
      return addWeeks(anchor, direction);
    case "month":
      return addMonths(anchor, direction);
    case "year":
      return addYears(anchor, direction);
    case "schedule":
      return addDays(anchor, 30 * direction);
  }
}

export function calendarRangeLabel(
  anchor: Date,
  view: CalendarView,
  range: CalendarRange,
) {
  if (view === "month") return format(anchor, "MMMM yyyy");
  if (view === "year") return format(anchor, "yyyy");
  if (view === "day") return format(anchor, "EEEE, MMMM d, yyyy");
  if (format(range.start, "yyyy") === format(range.end, "yyyy")) {
    return `${format(range.start, "MMM d")} – ${format(range.end, "MMM d, yyyy")}`;
  }
  return `${format(range.start, "MMM d, yyyy")} – ${format(range.end, "MMM d, yyyy")}`;
}

function eventDate(value?: string | null) {
  if (!value) return null;
  const date = new Date(value);
  return isValid(date) ? date : null;
}

export function calendarEventSegments(
  events: TestingLabTestingEventProjection[],
  range: CalendarRange,
): CalendarEventSegment[] {
  return events
    .flatMap((event) => {
      const eventStart = eventDate(event.startsAt);
      if (!eventStart) return [];
      const eventEnd = eventDate(event.endsAt) ?? eventStart;
      const normalizedEnd = eventEnd >= eventStart ? eventEnd : eventStart;

      return range.days
        .filter(
          (day) =>
            eventStart <= endOfDay(day) && normalizedEnd >= startOfDay(day),
        )
        .map((day) => ({
          event,
          day,
          dayKey: format(day, "yyyy-MM-dd"),
          startsOnDay:
            format(eventStart, "yyyy-MM-dd") === format(day, "yyyy-MM-dd"),
          endsOnDay:
            format(normalizedEnd, "yyyy-MM-dd") === format(day, "yyyy-MM-dd"),
        }));
    })
    .sort((left, right) => {
      const leftStart = eventDate(left.event.startsAt)?.valueOf() ?? 0;
      const rightStart = eventDate(right.event.startsAt)?.valueOf() ?? 0;
      return left.day.valueOf() - right.day.valueOf() || leftStart - rightStart;
    });
}
