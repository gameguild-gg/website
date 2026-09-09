"use client";

import { Link } from "@/i18n/navigation";
import {
  calendarEventSegments,
  calendarRange,
  calendarRangeLabel,
  calendarViews,
  shiftCalendarAnchor,
  type CalendarView,
} from "@/lib/testing-lab/calendar";
import { formatTestingEventStatus } from "@/lib/testing-lab/format";
import type {
  TestingLabTestingEventProjection,
  TestingLabTestingEventTemplateProjection,
} from "@game-guild/client";
import { Badge } from "@game-guild/ui/components/badge";
import { Button } from "@game-guild/ui/components/button";
import { Calendar } from "@game-guild/ui/components/calendar";
import {
  HoverCard,
  HoverCardContent,
  HoverCardTrigger,
} from "@game-guild/ui/components/hover-card";
import { Input } from "@game-guild/ui/components/input";
import { Progress } from "@game-guild/ui/components/progress";
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@game-guild/ui/components/select";
import { Separator } from "@game-guild/ui/components/separator";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@game-guild/ui/components/dialog";
import { cn } from "@game-guild/ui/lib/utils";
import {
  format,
  getISOWeek,
  isSameMonth,
  isToday,
  startOfMonth,
} from "date-fns";
import {
  Blend,
  CalendarClock,
  ChartNoAxesCombined,
  Check,
  ChevronLeft,
  ChevronRight,
  Clock3,
  MapPin,
  MonitorPlay,
  PanelRightClose,
  PanelRightOpen,
  Search,
  Settings2,
  UsersRound,
} from "lucide-react";
import {
  Fragment,
  type ReactNode,
  useMemo,
  useState,
  useSyncExternalStore,
} from "react";

import { CreateTestingEventDialog } from "./testing-event-management";

const viewLabels: Record<CalendarView, string> = {
  day: "Day",
  week: "Week",
  month: "Month",
  year: "Year",
  schedule: "Schedule",
  "3days": "3 days",
};

const calendarViewItems = calendarViews.map((value) => ({
  value,
  label: viewLabels[value],
}));

const eventFilters = [
  { value: "all", label: "All statuses" },
  { value: "Draft", label: "Draft" },
  { value: "ApplicationsOpen", label: "Applications open" },
  { value: "ApplicationsClosed", label: "Applications closed" },
  { value: "Scheduled", label: "Scheduled" },
  { value: "Active", label: "Active" },
  { value: "Completed", label: "Completed" },
  { value: "Cancelled", label: "Cancelled" },
] as const;

type EventFilter = (typeof eventFilters)[number]["value"];

interface TestingLabEventCalendar {
  id: string;
  label: string;
  eventClassName: string;
  dotClassName: string;
}

const defaultEventCalendar: TestingLabEventCalendar = {
  id: "default",
  label: "Testing events",
  eventClassName:
    "border-primary bg-primary/12 text-foreground hover:bg-primary/18",
  dotClassName: "bg-primary",
};

const eventCalendarPalette = [
  {
    eventClassName:
      "border-chart-2 bg-chart-2/12 text-foreground hover:bg-chart-2/18",
    dotClassName: "bg-chart-2",
  },
  {
    eventClassName:
      "border-chart-3 bg-chart-3/12 text-foreground hover:bg-chart-3/18",
    dotClassName: "bg-chart-3",
  },
  {
    eventClassName:
      "border-chart-4 bg-chart-4/12 text-foreground hover:bg-chart-4/18",
    dotClassName: "bg-chart-4",
  },
  {
    eventClassName:
      "border-chart-5 bg-chart-5/12 text-foreground hover:bg-chart-5/18",
    dotClassName: "bg-chart-5",
  },
] as const;

function eventCalendarId(event: TestingLabTestingEventProjection) {
  return event.configuration?.sourceTemplateId ?? defaultEventCalendar.id;
}

function buildEventCalendars(
  events: TestingLabTestingEventProjection[],
  templates: TestingLabTestingEventTemplateProjection[],
) {
  const names = new Map(
    templates
      .filter((template) => template.id)
      .map((template) => [
        template.id!,
        template.name?.trim() || "Untitled calendar",
      ]),
  );
  const templateIds = new Set([
    ...templates
      .map((template) => template.id)
      .filter((id): id is string => Boolean(id)),
    ...events
      .map(eventCalendarId)
      .filter((id) => id !== defaultEventCalendar.id),
  ]);

  return [
    defaultEventCalendar,
    ...[...templateIds].map((id, index) => ({
      id,
      label: names.get(id) ?? "Event template",
      ...eventCalendarPalette[index % eventCalendarPalette.length]!,
    })),
  ];
}

const mobileCalendarQuery = "(max-width: 767px)";

function subscribeToMobileCalendar(callback: () => void) {
  if (typeof window.matchMedia !== "function") return () => undefined;
  const mediaQuery = window.matchMedia(mobileCalendarQuery);
  mediaQuery.addEventListener("change", callback);
  return () => mediaQuery.removeEventListener("change", callback);
}

function getMobileCalendarSnapshot() {
  return (
    typeof window.matchMedia === "function" &&
    window.matchMedia(mobileCalendarQuery).matches
  );
}

function getServerMobileCalendarSnapshot() {
  return false;
}

export interface TestingLabCalendarEventAnalytics {
  eventId: string;
  registeredTesters: number;
  capacity: number;
  fillRate: number;
}

function eventStatusClass(status?: string | null) {
  if (status === "Cancelled") return "opacity-55 line-through";
  if (status === "Completed") return "opacity-70";
  return "";
}

function eventStart(event: TestingLabTestingEventProjection) {
  const date = event.startsAt ? new Date(event.startsAt) : null;
  return date && !Number.isNaN(date.valueOf()) ? date : null;
}

function eventEnd(event: TestingLabTestingEventProjection) {
  const date = event.endsAt ? new Date(event.endsAt) : null;
  return date && !Number.isNaN(date.valueOf()) ? date : null;
}

function eventsInsideRange(
  events: TestingLabTestingEventProjection[],
  range: ReturnType<typeof calendarRange>,
) {
  return events.filter((event) => {
    const startsAt = eventStart(event);
    if (!startsAt) return false;
    const endsAt = eventEnd(event) ?? startsAt;
    return startsAt <= range.end && endsAt >= range.start;
  });
}

function formatTestingHours(hours: number) {
  const roundedHours = Math.round(hours * 10) / 10;
  return `${roundedHours.toLocaleString(undefined, {
    maximumFractionDigits: 1,
  })}h testing time`;
}

function eventMode(mode?: TestingLabTestingEventProjection["mode"]) {
  switch (mode) {
    case "InPerson":
      return { label: "In-person", Icon: MapPin };
    case "Hybrid":
      return { label: "Hybrid", Icon: Blend };
    default:
      return { label: "Online", Icon: MonitorPlay };
  }
}

function capacityState(analytics?: TestingLabCalendarEventAnalytics) {
  if (!analytics) {
    return {
      label: "Capacity pending",
      detail: "Capacity information is not available yet.",
      isFull: false,
    };
  }

  const isFull =
    analytics.capacity > 0 && analytics.registeredTesters >= analytics.capacity;
  if (analytics.capacity <= 0) {
    return {
      label: `${analytics.registeredTesters} registered`,
      detail: "Tester capacity is unlimited.",
      isFull: false,
    };
  }

  const available = Math.max(
    0,
    analytics.capacity - analytics.registeredTesters,
  );
  return {
    label: `${analytics.registeredTesters}/${analytics.capacity}`,
    detail: isFull
      ? `${analytics.registeredTesters} of ${analytics.capacity} tester spots filled`
      : `${available} tester spot${available === 1 ? "" : "s"} available`,
    isFull,
  };
}

function EventLink({
  event,
  analytics,
  eventCalendar,
  onSelect,
  compact = false,
}: {
  event: TestingLabTestingEventProjection;
  analytics?: TestingLabCalendarEventAnalytics;
  eventCalendar: TestingLabEventCalendar;
  onSelect: (event: TestingLabTestingEventProjection) => void;
  compact?: boolean;
}) {
  if (!event.id) return null;
  const startsAt = eventStart(event);
  const endsAt = eventEnd(event);
  const { label: modeLabel, Icon: ModeIcon } = eventMode(event.mode);
  const capacity = capacityState(analytics);

  return (
    <HoverCard openDelay={0} closeDelay={100}>
      <HoverCardTrigger asChild>
        <button
          type="button"
          onClick={() => onSelect(event)}
          className={cn(
            "block w-full overflow-hidden rounded-sm border-l-2 px-2 py-1.5 text-left text-xs transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
            eventCalendar.eventClassName,
            eventStatusClass(event.status),
          )}
          aria-label={`${event.name ?? "Untitled event"}${startsAt ? `, ${format(startsAt, "PPp")}` : ""}`}
        >
          <span className="flex min-w-0 items-baseline gap-1.5">
            {startsAt ? (
              <span className="shrink-0 tabular-nums opacity-70">
                {format(startsAt, "p")}
              </span>
            ) : null}
            <span className="min-w-0 flex-1 truncate font-medium">
              {event.name ?? "Untitled event"}
            </span>
          </span>
          <span className="mt-0.5 flex min-w-0 items-center gap-1 truncate text-[11px] opacity-70">
            <ModeIcon
              className="size-3 shrink-0"
              aria-label={`${modeLabel} event`}
            />
            <span className="truncate">{modeLabel}</span>
            <span aria-hidden="true">·</span>
            <span
              className={capacity.isFull ? "font-medium text-destructive" : ""}
            >
              {capacity.label}
            </span>
            {compact ? null : (
              <>
                <span aria-hidden="true">·</span>
                <span className="truncate">
                  {formatTestingEventStatus(event.status)}
                </span>
              </>
            )}
          </span>
        </button>
      </HoverCardTrigger>
      <HoverCardContent align="start" sideOffset={8} className="w-80 space-y-3">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="truncate font-semibold">
              {event.name ?? "Untitled event"}
            </p>
            <p className="text-xs text-muted-foreground">
              {eventCalendar.label} · {modeLabel}
            </p>
          </div>
          <Badge variant="outline">
            {formatTestingEventStatus(event.status)}
          </Badge>
        </div>
        {event.description ? (
          <p className="text-sm text-muted-foreground">{event.description}</p>
        ) : null}
        <dl className="space-y-2 text-sm">
          <div className="flex items-start gap-2">
            <Clock3
              className="mt-0.5 size-4 shrink-0 text-muted-foreground"
              aria-hidden="true"
            />
            <div>
              <dt className="sr-only">Schedule</dt>
              <dd>
                {startsAt ? format(startsAt, "PPp") : "Schedule pending"}
                {endsAt ? ` - ${format(endsAt, "p")}` : ""}
              </dd>
            </div>
          </div>
          <div className="flex items-start gap-2">
            <UsersRound
              className="mt-0.5 size-4 shrink-0 text-muted-foreground"
              aria-hidden="true"
            />
            <div>
              <dt className="sr-only">Capacity</dt>
              <dd>{capacity.detail}</dd>
            </div>
          </div>
        </dl>
        <p className="text-xs text-muted-foreground">
          Open event workspace for applications, slots, attendance, and
          feedback.
        </p>
      </HoverCardContent>
    </HoverCard>
  );
}

function ScheduleView({
  events,
  analyticsByEvent,
  calendarsById,
  onSelectEvent,
  anchor,
}: {
  events: TestingLabTestingEventProjection[];
  analyticsByEvent: Map<string, TestingLabCalendarEventAnalytics>;
  calendarsById: Map<string, TestingLabEventCalendar>;
  onSelectEvent: (event: TestingLabTestingEventProjection) => void;
  anchor: Date;
}) {
  const range = calendarRange(anchor, "schedule", true);
  const scheduled = events
    .filter((event) => {
      const startsAt = eventStart(event);
      return startsAt && startsAt >= range.start && startsAt <= range.end;
    })
    .sort(
      (left, right) =>
        (eventStart(left)?.valueOf() ?? 0) -
        (eventStart(right)?.valueOf() ?? 0),
    );
  const scheduledByDay = scheduled.reduce<
    Map<string, TestingLabTestingEventProjection[]>
  >((groups, event) => {
    const startsAt = eventStart(event);
    if (!startsAt) return groups;
    const dayKey = format(startsAt, "yyyy-MM-dd");
    groups.set(dayKey, [...(groups.get(dayKey) ?? []), event]);
    return groups;
  }, new Map());

  return (
    <section aria-label="Testing Lab schedule" className="min-h-[32rem]">
      <div className="border-b px-4 py-3">
        <h2 className="text-lg font-semibold">Schedule</h2>
        <p className="text-sm text-muted-foreground">
          The next 90 days of Testing Lab events.
        </p>
      </div>
      {scheduled.length === 0 ? (
        <div className="flex min-h-80 items-center justify-center px-6 text-center">
          <div>
            <CalendarClock
              className="mx-auto size-6 text-muted-foreground"
              aria-hidden="true"
            />
            <p className="mt-3 text-sm font-medium">No events in this period</p>
            <p className="mt-1 text-sm text-muted-foreground">
              Choose another period or create a Testing Lab event.
            </p>
          </div>
        </div>
      ) : (
        <ol className="divide-y">
          {[...scheduledByDay.entries()].map(([dayKey, dayEvents]) => {
            const day = eventStart(dayEvents[0]);
            return (
              <li
                key={dayKey}
                className="grid grid-cols-[4.5rem_minmax(0,1fr)] gap-3 px-4 py-4 sm:grid-cols-[6rem_minmax(0,1fr)] sm:gap-6"
              >
                <time className="text-center" dateTime={day?.toISOString()}>
                  <span className="block text-[11px] font-medium uppercase tracking-wide text-muted-foreground">
                    {day ? format(day, "EEE") : ""}
                  </span>
                  <span className="mt-1 block text-2xl font-medium tabular-nums">
                    {day ? format(day, "d") : ""}
                  </span>
                  <span className="block text-xs text-muted-foreground">
                    {day ? format(day, "MMM") : ""}
                  </span>
                </time>
                <div className="space-y-2">
                  {dayEvents.map((event) => (
                    <div
                      key={event.id}
                      className="flex min-w-0 items-center gap-3"
                    >
                      <div className="min-w-0 flex-1">
                        <EventLink
                          event={event}
                          eventCalendar={
                            calendarsById.get(eventCalendarId(event)) ??
                            defaultEventCalendar
                          }
                          onSelect={onSelectEvent}
                          analytics={
                            event.id
                              ? analyticsByEvent.get(event.id)
                              : undefined
                          }
                          compact
                        />
                      </div>
                      <span className="hidden shrink-0 text-xs text-muted-foreground sm:block">
                        {formatTestingEventStatus(event.status)}
                      </span>
                    </div>
                  ))}
                </div>
              </li>
            );
          })}
        </ol>
      )}
    </section>
  );
}

function YearView({
  events,
  analyticsByEvent,
  calendarsById,
  onSelectEvent,
  anchor,
}: {
  events: TestingLabTestingEventProjection[];
  analyticsByEvent: Map<string, TestingLabCalendarEventAnalytics>;
  calendarsById: Map<string, TestingLabEventCalendar>;
  onSelectEvent: (event: TestingLabTestingEventProjection) => void;
  anchor: Date;
}) {
  const months = Array.from(
    { length: 12 },
    (_, index) => new Date(anchor.getFullYear(), index, 1),
  );

  return (
    <section
      aria-label="Testing Lab year"
      className="grid sm:grid-cols-2 xl:grid-cols-3"
    >
      {months.map((month) => {
        const monthEvents = events.filter((event) => {
          const startsAt = eventStart(event);
          return startsAt && isSameMonth(startsAt, month);
        });
        return (
          <div
            key={month.toISOString()}
            className="min-h-44 border-b border-r p-4"
          >
            <h2 className="text-sm font-semibold">{format(month, "MMMM")}</h2>
            {monthEvents.length === 0 ? (
              <p className="mt-3 text-sm text-muted-foreground">No events</p>
            ) : (
              <div className="mt-3 space-y-2">
                {monthEvents.slice(0, 4).map((event) => (
                  <EventLink
                    key={event.id}
                    event={event}
                    eventCalendar={
                      calendarsById.get(eventCalendarId(event)) ??
                      defaultEventCalendar
                    }
                    onSelect={onSelectEvent}
                    analytics={
                      event.id ? analyticsByEvent.get(event.id) : undefined
                    }
                  />
                ))}
                {monthEvents.length > 4 ? (
                  <p className="text-xs text-muted-foreground">
                    +{monthEvents.length - 4} more events
                  </p>
                ) : null}
              </div>
            )}
          </div>
        );
      })}
    </section>
  );
}

function GridView({
  events,
  analyticsByEvent,
  calendarsById,
  anchor,
  view,
  showWeekends,
  onCreateEvent,
  onSelectEvent,
}: {
  events: TestingLabTestingEventProjection[];
  analyticsByEvent: Map<string, TestingLabCalendarEventAnalytics>;
  calendarsById: Map<string, TestingLabEventCalendar>;
  anchor: Date;
  view: Exclude<CalendarView, "year" | "schedule">;
  showWeekends: boolean;
  onCreateEvent: (date: Date) => void;
  onSelectEvent: (event: TestingLabTestingEventProjection) => void;
}) {
  const [expandedDays, setExpandedDays] = useState<Set<string>>(
    () => new Set(),
  );
  const range = calendarRange(anchor, view, showWeekends);
  const segmentsByDay = useMemo(() => {
    const segments = calendarEventSegments(events, range);
    return segments.reduce<Map<string, typeof segments>>((byDay, segment) => {
      const values = byDay.get(segment.dayKey) ?? [];
      values.push(segment);
      byDay.set(segment.dayKey, values);
      return byDay;
    }, new Map());
  }, [events, range]);
  const weekdays = showWeekends
    ? ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]
    : ["Mon", "Tue", "Wed", "Thu", "Fri"];
  const monthStart = startOfMonth(anchor);
  const showWeekNumbers = view === "month";
  const gridTemplateColumns = showWeekNumbers
    ? `2.25rem repeat(${weekdays.length}, minmax(0, 1fr))`
    : `repeat(${
        view === "week" ? weekdays.length : range.days.length
      }, minmax(0, 1fr))`;

  return (
    <div className="relative min-h-0 flex-1 overflow-auto">
      <section
        aria-label={`${viewLabels[view]} Testing Lab calendar`}
        className={
          view === "month" || view === "week"
            ? "flex h-full min-w-[760px] flex-col overflow-hidden"
            : "flex h-full min-w-[420px] flex-col overflow-hidden"
        }
      >
        <div
          className="grid border-b text-center text-xs font-medium text-muted-foreground"
          style={{ gridTemplateColumns }}
        >
          {showWeekNumbers ? (
            <div
              aria-label="Week numbers"
              className="border-r px-1 py-2.5 text-[10px] uppercase tracking-wide"
            >
              Wk
            </div>
          ) : null}
          {(view === "month" || view === "week"
            ? weekdays.map((weekday) => ({ key: weekday, label: weekday }))
            : range.days.map((day) => ({
                key: day.toISOString(),
                label: format(day, "EEE d"),
              }))
          ).map(({ key, label }) => (
            <div key={key} className="px-2 py-2.5 uppercase tracking-wide">
              {label}
            </div>
          ))}
        </div>
        <div
          className="grid flex-1 auto-rows-fr"
          style={{ gridTemplateColumns }}
        >
          {range.days.map((day, dayIndex) => {
            const key = format(day, "yyyy-MM-dd");
            const segments = segmentsByDay.get(key) ?? [];
            const outsideMonth =
              view === "month" && !isSameMonth(day, monthStart);
            return (
              <Fragment key={key}>
                {showWeekNumbers && dayIndex % weekdays.length === 0 ? (
                  <div
                    aria-label={`Week ${getISOWeek(day)}`}
                    className="flex min-h-24 items-start justify-center border-b border-r bg-muted/20 px-1 py-3 text-[11px] font-medium tabular-nums text-muted-foreground md:min-h-0"
                  >
                    {getISOWeek(day)}
                  </div>
                ) : null}
                <div
                  className={`group relative min-h-24 border-b border-r p-2 last:border-r-0 md:min-h-0 ${outsideMonth ? "bg-muted/20 text-muted-foreground" : "bg-background"}`}
                >
                  <button
                    type="button"
                    aria-label={`Create event on ${format(day, "MMMM d, yyyy")}. Double-click to open.`}
                    className="absolute inset-0 z-0 rounded-none transition-colors hover:bg-muted/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring"
                    onDoubleClick={() => onCreateEvent(new Date(day))}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" || event.key === " ") {
                        event.preventDefault();
                        onCreateEvent(new Date(day));
                      }
                    }}
                  />
                  <time
                    dateTime={day.toISOString()}
                    className={`pointer-events-none relative z-10 mb-2 flex size-7 items-center justify-center rounded-full text-xs font-medium tabular-nums ${isToday(day) ? "bg-primary text-primary-foreground" : ""}`}
                  >
                    {format(day, "d")}
                  </time>
                  <div className="relative z-10 space-y-1">
                    {segments
                      .slice(0, expandedDays.has(key) ? segments.length : 3)
                      .map((segment) => (
                      <EventLink
                        key={`${segment.event.id}-${segment.dayKey}`}
                        event={segment.event}
                        eventCalendar={
                          calendarsById.get(eventCalendarId(segment.event)) ??
                          defaultEventCalendar
                        }
                        onSelect={onSelectEvent}
                        analytics={
                          segment.event.id
                            ? analyticsByEvent.get(segment.event.id)
                            : undefined
                        }
                        compact
                      />
                    ))}
                    {segments.length > 3 && !expandedDays.has(key) ? (
                      <button
                        type="button"
                        className="rounded-sm px-1 text-xs font-medium text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                        onClick={() =>
                          setExpandedDays((current) =>
                            new Set(current).add(key),
                          )
                        }
                      >
                        +{segments.length - 3} more
                      </button>
                    ) : null}
                  </div>
                </div>
              </Fragment>
            );
          })}
        </div>
      </section>
    </div>
  );
}

function TestingLabPlanningSidebar({
  anchor,
  onAnchorChange,
  events,
  analyticsByEvent,
  eventCalendars,
  visibleCalendarIds,
  onCalendarVisibilityChange,
}: {
  anchor: Date;
  onAnchorChange: (date: Date) => void;
  events: TestingLabTestingEventProjection[];
  analyticsByEvent: Map<string, TestingLabCalendarEventAnalytics>;
  eventCalendars: TestingLabEventCalendar[];
  visibleCalendarIds: Set<string>;
  onCalendarVisibilityChange: (calendarId: string, visible: boolean) => void;
}) {
  const eventCount = events.length;
  const testingHours = events.reduce((total, event) => {
    const startsAt = eventStart(event);
    const endsAt = eventEnd(event);
    if (!startsAt || !endsAt || endsAt <= startsAt) return total;
    return total + (endsAt.valueOf() - startsAt.valueOf()) / 3_600_000;
  }, 0);
  const capacity = events.reduce(
    (totals, event) => {
      const analytics = event.id ? analyticsByEvent.get(event.id) : undefined;
      if (!analytics || analytics.capacity <= 0) return totals;
      totals.registered += Math.min(
        analytics.registeredTesters,
        analytics.capacity,
      );
      totals.available += analytics.capacity;
      return totals;
    },
    { registered: 0, available: 0 },
  );
  const fillRate = capacity.available
    ? Math.round((capacity.registered / capacity.available) * 100)
    : 0;
  return (
    <aside
      aria-label="Testing Lab planning"
      className="absolute inset-y-0 right-0 flex w-72 shrink-0 flex-col overflow-y-auto border-l bg-background shadow-lg md:static md:shadow-none"
    >
      <section aria-labelledby="mini-calendar-title" className="p-4">
        <h2 id="mini-calendar-title" className="sr-only">
          Mini calendar
        </h2>
        <Calendar
          mode="single"
          month={anchor}
          selected={anchor}
          onMonthChange={onAnchorChange}
          onSelect={(date) => {
            if (date) onAnchorChange(date);
          }}
          showOutsideDays={false}
          showWeekNumber
          className="w-full bg-transparent p-0 [--cell-size:--spacing(8)]"
          classNames={{
            root: "w-full",
            months: "relative w-full",
            month: "w-full",
          }}
        />
      </section>

      <Separator />

      <section aria-labelledby="event-calendars-title" className="p-4">
        <div className="flex items-center justify-between gap-3">
          <div>
            <h2 id="event-calendars-title" className="text-sm font-semibold">
              Event calendars
            </h2>
            <p className="mt-0.5 text-xs text-muted-foreground">
              Grouped by event template.
            </p>
          </div>
          <Button asChild variant="ghost" size="icon-sm">
            <Link
              href="/workspace/testing-lab/settings/templates"
              aria-label="Manage event calendars"
              title="Manage event calendars"
            >
              <Settings2 aria-hidden="true" />
            </Link>
          </Button>
        </div>
        <div className="mt-3 space-y-1">
          {eventCalendars.map((eventCalendar) => {
            const checked = visibleCalendarIds.has(eventCalendar.id);
            return (
              <label
                key={eventCalendar.id}
                className="flex min-h-9 cursor-pointer items-center gap-2 rounded-sm px-1.5 text-sm hover:bg-muted/40"
              >
                <span
                  className={cn(
                    "flex size-4 items-center justify-center rounded-[4px] border",
                    checked
                      ? "border-primary bg-primary text-primary-foreground"
                      : "border-input",
                  )}
                >
                  {checked ? <Check className="size-3" aria-hidden="true" /> : null}
                </span>
                <input
                  type="checkbox"
                  className="sr-only"
                  checked={checked}
                  onChange={(event) =>
                    onCalendarVisibilityChange(
                      eventCalendar.id,
                      event.currentTarget.checked,
                    )
                  }
                />
                <span
                  className={cn("size-2.5 rounded-full", eventCalendar.dotClassName)}
                  aria-hidden="true"
                />
                <span className="min-w-0 flex-1 truncate">
                  {eventCalendar.label}
                </span>
              </label>
            );
          })}
        </div>
      </section>

      <Separator />

      <section aria-labelledby="period-summary-title" className="p-4">
        <div className="flex items-center gap-2">
          <ChartNoAxesCombined
            className="size-4 text-muted-foreground"
            aria-hidden="true"
          />
          <h2 id="period-summary-title" className="text-sm font-semibold">
            {format(anchor, "MMMM")} summary
          </h2>
        </div>
        {eventCount === 0 ? (
          <div className="mt-3 flex flex-col gap-1">
            <p className="text-sm font-medium">No scheduled activity</p>
            <p className="text-xs leading-relaxed text-muted-foreground">
              Choose another month or create an event to see testing time and
              capacity.
            </p>
          </div>
        ) : (
          <div className="mt-3 flex flex-col gap-4">
            <div className="flex items-baseline justify-between gap-3">
              <p className="text-sm font-medium tabular-nums">
                {eventCount} {eventCount === 1 ? "event" : "events"}
              </p>
              <p className="text-sm tabular-nums text-muted-foreground">
                {formatTestingHours(testingHours).replace(
                  " testing time",
                  " scheduled",
                )}
              </p>
            </div>
            {capacity.available ? (
              <div className="flex flex-col gap-2">
                <div className="flex items-center justify-between gap-3 text-xs">
                  <span className="text-muted-foreground">Tester seats</span>
                  <span className="font-medium tabular-nums">
                    {capacity.registered} of {capacity.available} seats filled
                  </span>
                </div>
                <Progress
                  value={fillRate}
                  aria-label="Tester capacity filled"
                  className="gap-0"
                />
              </div>
            ) : (
              <p className="text-xs text-muted-foreground">
                Tester capacity has not been set for these events.
              </p>
            )}
          </div>
        )}
        <Button asChild variant="link" size="sm" className="mt-3 h-auto px-0">
          <Link href="/workspace/testing-lab/settings/analytics">
            View analytics
          </Link>
        </Button>
      </section>
    </aside>
  );
}

export function TestingLabCalendar({
  events,
  eventAnalytics = [],
  templates = [],
  initialDate = new Date(),
  defaultTimeZone = "UTC",
  toolbarStart,
  toolbarEnd,
}: {
  events: TestingLabTestingEventProjection[];
  eventAnalytics?: TestingLabCalendarEventAnalytics[];
  templates?: TestingLabTestingEventTemplateProjection[];
  initialDate?: Date;
  defaultTimeZone?: string;
  toolbarStart?: ReactNode;
  toolbarEnd?: ReactNode;
}) {
  const isMobileCalendar = useSyncExternalStore(
    subscribeToMobileCalendar,
    getMobileCalendarSnapshot,
    getServerMobileCalendarSnapshot,
  );
  const [selectedView, setSelectedView] = useState<CalendarView | null>(null);
  const view = selectedView ?? (isMobileCalendar ? "schedule" : "month");
  const [anchor, setAnchor] = useState(() => initialDate);
  const [query, setQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState<EventFilter>("all");
  const [planningPreference, setPlanningPreference] = useState<boolean | null>(
    null,
  );
  const [createDate, setCreateDate] = useState<Date | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [selectedEvent, setSelectedEvent] =
    useState<TestingLabTestingEventProjection | null>(null);
  const eventCalendars = useMemo(
    () => buildEventCalendars(events, templates),
    [events, templates],
  );
  const calendarsById = useMemo(
    () => new Map(eventCalendars.map((calendar) => [calendar.id, calendar])),
    [eventCalendars],
  );
  const [calendarVisibility, setCalendarVisibility] = useState<
    Record<string, boolean>
  >({});
  const visibleCalendarIds = useMemo(
    () =>
      new Set(
        eventCalendars
          .filter((calendar) => calendarVisibility[calendar.id] !== false)
          .map((calendar) => calendar.id),
      ),
    [calendarVisibility, eventCalendars],
  );
  const planningOpen = planningPreference ?? !isMobileCalendar;
  const range = calendarRange(anchor, view, true);
  const analyticsByEvent = useMemo(
    () =>
      new Map(
        eventAnalytics.map((analytics) => [analytics.eventId, analytics]),
      ),
    [eventAnalytics],
  );
  const visibleEvents = useMemo(() => {
    const normalizedQuery = query.trim().toLocaleLowerCase();
    return events.filter((event) => {
      const matchesStatus =
        statusFilter === "all" || event.status === statusFilter;
      const matchesCalendar = visibleCalendarIds.has(eventCalendarId(event));
      const matchesQuery =
        normalizedQuery.length === 0 ||
        event.name?.toLocaleLowerCase().includes(normalizedQuery) ||
        event.description?.toLocaleLowerCase().includes(normalizedQuery);
      return matchesStatus && matchesCalendar && matchesQuery;
    });
  }, [events, query, statusFilter, visibleCalendarIds]);
  const periodEvents = useMemo(
    () => eventsInsideRange(visibleEvents, range),
    [range, visibleEvents],
  );

  function openCreateEvent(date: Date | null) {
    setCreateDate(date);
    setCreateOpen(true);
  }

  return (
    <section
      aria-label="Testing Lab calendar"
      className="flex h-full min-h-0 flex-col overflow-hidden bg-background"
    >
      <div className="overflow-x-auto border-b">
        <div
          role="toolbar"
          aria-label="Testing Lab calendar controls"
          className="flex w-full min-w-max items-center gap-2 p-2.5"
        >
          {toolbarStart ? (
            <div className="mr-2 flex shrink-0 items-center">
              {toolbarStart}
            </div>
          ) : null}
          <div className="flex items-center gap-1">
            <Button
              className="h-11 px-3 sm:h-9"
              type="button"
              variant="outline"
              onClick={() => setAnchor(new Date())}
            >
              Today
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              className="size-11 sm:size-9"
              aria-label="Previous period"
              onClick={() =>
                setAnchor((date) => shiftCalendarAnchor(date, view, -1))
              }
            >
              <ChevronLeft className="size-4" />
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              className="size-11 sm:size-9"
              aria-label="Next period"
              onClick={() =>
                setAnchor((date) => shiftCalendarAnchor(date, view, 1))
              }
            >
              <ChevronRight className="size-4" />
            </Button>
          </div>
          <h2 className="min-w-44 text-lg font-semibold sm:text-xl">
            {calendarRangeLabel(anchor, view, range)}
          </h2>
          <div className="ml-auto flex items-center gap-2">
            <label className="relative min-w-44 flex-1 lg:w-48 lg:flex-none">
              <span className="sr-only">Search events</span>
              <Search
                className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
                aria-hidden="true"
              />
              <Input
                type="search"
                aria-label="Search events"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                placeholder="Search events"
                className="h-11 border-transparent bg-muted/50 pl-9 shadow-none focus-visible:bg-background sm:h-9"
              />
            </label>
            <Select
              items={eventFilters}
              value={statusFilter}
              onValueChange={(value) => setStatusFilter(value as EventFilter)}
            >
              <SelectTrigger
                size="sm"
                aria-label="Filter events"
                className="w-40"
              >
                <SelectValue />
              </SelectTrigger>
              <SelectContent alignItemWithTrigger={false}>
                <SelectGroup>
                  {eventFilters.map(({ value, label }) => (
                    <SelectItem key={value} value={value}>
                      {label}
                    </SelectItem>
                  ))}
                </SelectGroup>
              </SelectContent>
            </Select>
            <Select
              items={calendarViewItems}
              value={view}
              onValueChange={(value) => setSelectedView(value as CalendarView)}
            >
              <SelectTrigger
                size="sm"
                aria-label="Calendar view"
                className="w-28"
              >
                <SelectValue />
              </SelectTrigger>
              <SelectContent alignItemWithTrigger={false}>
                <SelectGroup>
                  {calendarViews.map((value) => (
                    <SelectItem key={value} value={value}>
                      {viewLabels[value]}
                    </SelectItem>
                  ))}
                </SelectGroup>
              </SelectContent>
            </Select>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              aria-label={
                planningOpen ? "Hide details panel" : "Show details panel"
              }
              title={planningOpen ? "Hide details panel" : "Show details panel"}
              aria-pressed={planningOpen}
              onClick={() => setPlanningPreference(!planningOpen)}
            >
              {planningOpen ? (
                <PanelRightClose data-icon="inline-start" />
              ) : (
                <PanelRightOpen data-icon="inline-start" />
              )}
            </Button>
            {toolbarEnd}
          </div>
        </div>
      </div>

      <div className="relative flex min-h-0 flex-1">
        <div className="flex min-w-0 flex-1 flex-col">
          {view === "schedule" ? (
            <ScheduleView
              events={visibleEvents}
              analyticsByEvent={analyticsByEvent}
              calendarsById={calendarsById}
              onSelectEvent={setSelectedEvent}
              anchor={anchor}
            />
          ) : null}
          {view === "year" ? (
            <YearView
              events={visibleEvents}
              analyticsByEvent={analyticsByEvent}
              calendarsById={calendarsById}
              onSelectEvent={setSelectedEvent}
              anchor={anchor}
            />
          ) : null}
          {view !== "schedule" && view !== "year" ? (
            <>
              <p className="text-xs text-muted-foreground md:hidden">
                Swipe horizontally to view every day.
              </p>
              <GridView
                events={visibleEvents}
                analyticsByEvent={analyticsByEvent}
                calendarsById={calendarsById}
                anchor={anchor}
                view={view}
                showWeekends
                onCreateEvent={(date) => openCreateEvent(date)}
                onSelectEvent={setSelectedEvent}
              />
            </>
          ) : null}
        </div>
        {planningOpen ? (
          <TestingLabPlanningSidebar
            anchor={anchor}
            onAnchorChange={setAnchor}
            events={periodEvents}
            analyticsByEvent={analyticsByEvent}
            eventCalendars={eventCalendars}
            visibleCalendarIds={visibleCalendarIds}
            onCalendarVisibilityChange={(calendarId, visible) =>
              setCalendarVisibility((current) => ({
                ...current,
                [calendarId]: visible,
              }))
            }
          />
        ) : null}
      </div>

      <CreateTestingEventDialog
        key={createDate?.toISOString() ?? "toolbar"}
        initialDate={createDate ?? undefined}
        open={createOpen}
        onOpenChange={setCreateOpen}
        showTrigger={false}
        defaultTimeZone={defaultTimeZone}
        templates={templates}
      />

      <Dialog
        open={Boolean(selectedEvent)}
        onOpenChange={(open) => {
          if (!open) setSelectedEvent(null);
        }}
      >
        <DialogContent className="sm:max-w-md">
          {selectedEvent ? (
            <>
              <DialogHeader>
                <div className="flex items-center gap-2">
                  <span
                    className={cn(
                      "size-2.5 rounded-full",
                      (calendarsById.get(eventCalendarId(selectedEvent)) ??
                        defaultEventCalendar).dotClassName,
                    )}
                    aria-hidden="true"
                  />
                  <DialogTitle>
                    {selectedEvent.name ?? "Untitled event"}
                  </DialogTitle>
                </div>
                <DialogDescription>
                  {(calendarsById.get(eventCalendarId(selectedEvent)) ??
                    defaultEventCalendar).label}
                </DialogDescription>
              </DialogHeader>
              <dl className="space-y-3 text-sm">
                <div className="flex items-start gap-3">
                  <Clock3 className="mt-0.5 size-4 text-muted-foreground" aria-hidden="true" />
                  <div>
                    <dt className="sr-only">Event schedule</dt>
                    <dd>
                      {eventStart(selectedEvent)
                        ? format(eventStart(selectedEvent)!, "PPp")
                        : "Schedule pending"}
                      {eventEnd(selectedEvent)
                        ? ` to ${format(eventEnd(selectedEvent)!, "p")}`
                        : ""}
                    </dd>
                  </div>
                </div>
                <div className="flex items-start gap-3">
                  <UsersRound className="mt-0.5 size-4 text-muted-foreground" aria-hidden="true" />
                  <div>
                    <dt className="sr-only">Capacity</dt>
                    <dd>
                      {capacityState(
                        selectedEvent.id
                          ? analyticsByEvent.get(selectedEvent.id)
                          : undefined,
                      ).detail}
                    </dd>
                  </div>
                </div>
              </dl>
              {selectedEvent.description ? (
                <p className="text-sm text-muted-foreground">
                  {selectedEvent.description}
                </p>
              ) : null}
              <DialogFooter>
                <Button asChild>
                  <Link
                    href={`/workspace/testing-lab/events/${selectedEvent.id}`}
                  >
                    Open event
                  </Link>
                </Button>
              </DialogFooter>
            </>
          ) : null}
        </DialogContent>
      </Dialog>
    </section>
  );
}
