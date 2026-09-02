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
import type { TestingLabTestingEventProjection } from "@game-guild/client";
import { Badge } from "@game-guild/ui/components/badge";
import { Button } from "@game-guild/ui/components/button";
import {
  HoverCard,
  HoverCardContent,
  HoverCardTrigger,
} from "@game-guild/ui/components/hover-card";
import { Input } from "@game-guild/ui/components/input";
import { format, isSameMonth, isToday, startOfMonth } from "date-fns";
import {
  Blend,
  CalendarClock,
  ChevronLeft,
  ChevronRight,
  Clock3,
  MapPin,
  MonitorPlay,
  Search,
  UsersRound,
} from "lucide-react";
import { useMemo, useState, useSyncExternalStore } from "react";

import { CreateTestingEventDialog } from "./testing-event-management";

const viewLabels: Record<CalendarView, string> = {
  day: "Day",
  week: "Week",
  month: "Month",
  year: "Year",
  schedule: "Schedule",
  "3days": "3 days",
};

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
  switch (status) {
    case "Active":
    case "ApplicationsOpen":
      return "border-primary bg-primary/12 text-foreground hover:bg-primary/18";
    case "Completed":
      return "border-muted-foreground/45 bg-muted/60 text-muted-foreground hover:bg-muted/80";
    case "Cancelled":
      return "border-destructive bg-destructive/10 text-destructive hover:bg-destructive/15";
    default:
      return "border-muted-foreground/45 bg-muted/45 text-foreground hover:bg-muted/70";
  }
}

function eventStart(event: TestingLabTestingEventProjection) {
  const date = event.startsAt ? new Date(event.startsAt) : null;
  return date && !Number.isNaN(date.valueOf()) ? date : null;
}

function eventEnd(event: TestingLabTestingEventProjection) {
  const date = event.endsAt ? new Date(event.endsAt) : null;
  return date && !Number.isNaN(date.valueOf()) ? date : null;
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
  compact = false,
}: {
  event: TestingLabTestingEventProjection;
  analytics?: TestingLabCalendarEventAnalytics;
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
        <Link
          href={`/console/community/testing-lab/events/${event.id}`}
          className={`block overflow-hidden rounded-sm border-l-2 px-2 py-1.5 text-left text-xs transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${eventStatusClass(event.status)}`}
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
        </Link>
      </HoverCardTrigger>
      <HoverCardContent align="start" sideOffset={8} className="w-80 space-y-3">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="truncate font-semibold">
              {event.name ?? "Untitled event"}
            </p>
            <p className="text-xs text-muted-foreground">
              {modeLabel} testing event
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
  anchor,
}: {
  events: TestingLabTestingEventProjection[];
  analyticsByEvent: Map<string, TestingLabCalendarEventAnalytics>;
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
  anchor,
}: {
  events: TestingLabTestingEventProjection[];
  analyticsByEvent: Map<string, TestingLabCalendarEventAnalytics>;
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
  anchor,
  view,
  showWeekends,
  onCreateEvent,
}: {
  events: TestingLabTestingEventProjection[];
  analyticsByEvent: Map<string, TestingLabCalendarEventAnalytics>;
  anchor: Date;
  view: Exclude<CalendarView, "year" | "schedule">;
  showWeekends: boolean;
  onCreateEvent: (date: Date) => void;
}) {
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
    ? ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"]
    : ["Mon", "Tue", "Wed", "Thu", "Fri"];
  const monthStart = startOfMonth(anchor);

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
          style={{
            gridTemplateColumns: `repeat(${view === "month" || view === "week" ? weekdays.length : range.days.length}, minmax(0, 1fr))`,
          }}
        >
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
          style={{
            gridTemplateColumns: `repeat(${view === "month" || view === "week" ? weekdays.length : range.days.length}, minmax(0, 1fr))`,
          }}
        >
          {range.days.map((day) => {
            const key = format(day, "yyyy-MM-dd");
            const segments = segmentsByDay.get(key) ?? [];
            const outsideMonth =
              view === "month" && !isSameMonth(day, monthStart);
            return (
              <div
                key={key}
                className={`group relative min-h-24 border-b border-r p-2 last:border-r-0 md:min-h-0 ${outsideMonth ? "bg-muted/20 text-muted-foreground" : "bg-background"}`}
              >
                <button
                  type="button"
                  aria-label={`Create event on ${format(day, "MMMM d, yyyy")}`}
                  className="absolute inset-0 z-0 rounded-none transition-colors hover:bg-muted/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring"
                  onClick={() => onCreateEvent(new Date(day))}
                />
                <time
                  dateTime={day.toISOString()}
                  className={`pointer-events-none relative z-10 mb-2 flex size-7 items-center justify-center rounded-full text-xs font-medium tabular-nums ${isToday(day) ? "bg-primary text-primary-foreground" : ""}`}
                >
                  {format(day, "d")}
                </time>
                <div className="relative z-10 space-y-1">
                  {segments.slice(0, 3).map((segment) => (
                    <EventLink
                      key={`${segment.event.id}-${segment.dayKey}`}
                      event={segment.event}
                      analytics={
                        segment.event.id
                          ? analyticsByEvent.get(segment.event.id)
                          : undefined
                      }
                      compact
                    />
                  ))}
                  {segments.length > 3 ? (
                    <p className="text-xs text-muted-foreground">
                      +{segments.length - 3} more
                    </p>
                  ) : null}
                </div>
              </div>
            );
          })}
        </div>
      </section>
      {segmentsByDay.size === 0 ? (
        <div className="pointer-events-none absolute inset-x-0 top-32 flex justify-center px-6 text-center">
          <div className="rounded-md bg-background/90 px-4 py-3 shadow-sm backdrop-blur-sm">
            <p className="text-sm font-medium">No events in this period</p>
            <p className="mt-1 text-xs text-muted-foreground">
              Select any day to create one.
            </p>
          </div>
        </div>
      ) : null}
    </div>
  );
}

export function TestingLabCalendar({
  events,
  eventAnalytics = [],
  initialDate = new Date(),
}: {
  events: TestingLabTestingEventProjection[];
  eventAnalytics?: TestingLabCalendarEventAnalytics[];
  initialDate?: Date;
}) {
  const isMobileCalendar = useSyncExternalStore(
    subscribeToMobileCalendar,
    getMobileCalendarSnapshot,
    getServerMobileCalendarSnapshot,
  );
  const [selectedView, setSelectedView] = useState<CalendarView | null>(null);
  const view = selectedView ?? (isMobileCalendar ? "schedule" : "month");
  const [anchor, setAnchor] = useState(() => initialDate);
  const [showWeekends, setShowWeekends] = useState(true);
  const [query, setQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState<EventFilter>("all");
  const [createDate, setCreateDate] = useState<Date | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const range = calendarRange(anchor, view, showWeekends);
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
      const matchesQuery =
        normalizedQuery.length === 0 ||
        event.name?.toLocaleLowerCase().includes(normalizedQuery) ||
        event.description?.toLocaleLowerCase().includes(normalizedQuery);
      return matchesStatus && matchesQuery;
    });
  }, [events, query, statusFilter]);

  function openCreateEvent(date: Date | null) {
    setCreateDate(date);
    setCreateOpen(true);
  }

  return (
    <section
      aria-label="Testing Lab calendar"
      className="flex h-full min-h-0 flex-col overflow-hidden bg-background"
    >
      <div className="flex flex-wrap items-center gap-2 border-b p-2.5">
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
        <h2 className="min-w-44 flex-1 text-lg font-semibold sm:text-xl">
          {calendarRangeLabel(anchor, view, range)}
        </h2>
        <div className="order-3 flex w-full flex-wrap items-center gap-2 lg:order-none lg:w-auto lg:flex-nowrap">
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
          <label className="inline-flex min-h-11 items-center rounded-md bg-muted/50 px-3 text-sm font-medium sm:min-h-9">
            <span className="sr-only">Filter events</span>
            <select
              aria-label="Filter events"
              value={statusFilter}
              onChange={(event) =>
                setStatusFilter(event.target.value as EventFilter)
              }
              className="max-w-36 bg-transparent outline-none"
            >
              {eventFilters.map(({ value, label }) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </label>
          <label className="inline-flex min-h-11 items-center rounded-md bg-muted/50 px-3 text-sm font-medium sm:min-h-9">
            <span className="sr-only">Calendar view</span>
            <select
              aria-label="Calendar view"
              value={view}
              onChange={(event) =>
                setSelectedView(event.target.value as CalendarView)
              }
              className="bg-transparent outline-none"
            >
              {calendarViews.map((value) => (
                <option key={value} value={value}>
                  {viewLabels[value]}
                </option>
              ))}
            </select>
          </label>
          {view !== "schedule" && view !== "year" ? (
            <Button
              className="h-11 sm:h-9"
              type="button"
              variant="ghost"
              aria-pressed={showWeekends}
              onClick={() => setShowWeekends((visible) => !visible)}
            >
              {showWeekends ? "Hide weekends" : "Show weekends"}
            </Button>
          ) : null}
        </div>
      </div>

      {view === "schedule" ? (
        <ScheduleView
          events={visibleEvents}
          analyticsByEvent={analyticsByEvent}
          anchor={anchor}
        />
      ) : null}
      {view === "year" ? (
        <YearView
          events={visibleEvents}
          analyticsByEvent={analyticsByEvent}
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
            anchor={anchor}
            view={view}
            showWeekends={showWeekends}
            onCreateEvent={(date) => openCreateEvent(date)}
          />
        </>
      ) : null}

      <CreateTestingEventDialog
        key={createDate?.toISOString() ?? "toolbar"}
        initialDate={createDate ?? undefined}
        open={createOpen}
        onOpenChange={setCreateOpen}
        showTrigger={false}
      />
    </section>
  );
}
