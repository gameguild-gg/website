'use client';

import { Link } from '@/i18n/navigation';
import {
  calendarEventSegments,
  calendarRange,
  calendarRangeLabel,
  calendarViews,
  shiftCalendarAnchor,
  type CalendarView,
} from '@/lib/testing-lab/calendar';
import { formatTestingEventStatus } from '@/lib/testing-lab/format';
import type { TestingLabTestingEventProjection } from '@game-guild/client';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { HoverCard, HoverCardContent, HoverCardTrigger } from '@game-guild/ui/components/hover-card';
import { format, isSameMonth, startOfMonth } from 'date-fns';
import { Blend, ChevronLeft, ChevronRight, Clock3, MapPin, MonitorPlay, UsersRound } from 'lucide-react';
import { useMemo, useState } from 'react';

import { CreateTestingEventDialog } from './testing-event-management';

const viewLabels: Record<CalendarView, string> = {
  day: 'Day',
  week: 'Week',
  month: 'Month',
  year: 'Year',
  schedule: 'Schedule',
  '3days': '3 days',
};

export interface TestingLabCalendarEventAnalytics {
  eventId: string;
  registeredTesters: number;
  capacity: number;
  fillRate: number;
}

function eventStatusClass(status?: string | null) {
  switch (status) {
    case 'Active':
      return 'border-primary/35 bg-primary/10 text-foreground';
    case 'Completed':
      return 'border-border bg-muted text-muted-foreground';
    case 'Cancelled':
      return 'border-destructive/30 bg-destructive/10 text-destructive';
    default:
      return 'border-border bg-muted/60 text-foreground';
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

function eventMode(mode?: TestingLabTestingEventProjection['mode']) {
  switch (mode) {
    case 'InPerson':
      return { label: 'In-person', Icon: MapPin };
    case 'Hybrid':
      return { label: 'Hybrid', Icon: Blend };
    default:
      return { label: 'Online', Icon: MonitorPlay };
  }
}

function capacityState(analytics?: TestingLabCalendarEventAnalytics) {
  if (!analytics) {
    return {
      label: 'Capacity pending',
      detail: 'Capacity information is not available yet.',
      isFull: false,
    };
  }

  const isFull = analytics.capacity > 0 && analytics.registeredTesters >= analytics.capacity;
  if (analytics.capacity <= 0) {
    return {
      label: 'Vacant',
      detail: 'Tester capacity is unlimited.',
      isFull: false,
    };
  }

  const available = Math.max(0, analytics.capacity - analytics.registeredTesters);
  return {
    label: isFull ? 'Full' : 'Vacant',
    detail: isFull
      ? `${analytics.registeredTesters} of ${analytics.capacity} tester spots filled`
      : `${available} tester spot${available === 1 ? '' : 's'} available`,
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
          className={`block rounded border px-2 py-1.5 text-left text-xs transition-colors hover:brightness-95 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${eventStatusClass(event.status)}`}
          aria-label={`${event.name ?? 'Untitled event'}${startsAt ? `, ${format(startsAt, 'PPp')}` : ''}`}
        >
          <span className="flex min-w-0 items-center gap-1.5">
            <ModeIcon className="size-3.5 shrink-0" aria-label={`${modeLabel} event`} />
            <span className="min-w-0 flex-1 truncate font-medium">{event.name ?? 'Untitled event'}</span>
            <Badge
              variant="outline"
              className={`h-5 shrink-0 px-1.5 text-[10px] ${capacity.isFull ? 'border-destructive/35 text-destructive' : 'border-primary/30 text-foreground'}`}
            >
              {capacity.label}
            </Badge>
          </span>
          {compact ? null : (
            <span className="mt-1 block truncate text-[11px] opacity-75">{formatTestingEventStatus(event.status)}</span>
          )}
        </Link>
      </HoverCardTrigger>
      <HoverCardContent align="start" sideOffset={8} className="w-80 space-y-3">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="truncate font-semibold">{event.name ?? 'Untitled event'}</p>
            <p className="text-xs text-muted-foreground">{modeLabel} testing event</p>
          </div>
          <Badge variant="outline">{formatTestingEventStatus(event.status)}</Badge>
        </div>
        {event.description ? <p className="text-sm text-muted-foreground">{event.description}</p> : null}
        <dl className="space-y-2 text-sm">
          <div className="flex items-start gap-2">
            <Clock3 className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
            <div>
              <dt className="sr-only">Schedule</dt>
              <dd>
                {startsAt ? format(startsAt, 'PPp') : 'Schedule pending'}
                {endsAt ? ` - ${format(endsAt, 'p')}` : ''}
              </dd>
            </div>
          </div>
          <div className="flex items-start gap-2">
            <UsersRound className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
            <div>
              <dt className="sr-only">Capacity</dt>
              <dd>{capacity.detail}</dd>
            </div>
          </div>
        </dl>
        <p className="text-xs text-muted-foreground">
          Open event workspace for applications, slots, attendance, and feedback.
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
  const range = calendarRange(anchor, 'schedule', true);
  const scheduled = events
    .filter((event) => {
      const startsAt = eventStart(event);
      return startsAt && startsAt >= range.start && startsAt <= range.end;
    })
    .sort((left, right) => (eventStart(left)?.valueOf() ?? 0) - (eventStart(right)?.valueOf() ?? 0));

  return (
    <section aria-label="Testing Lab schedule" className="overflow-hidden rounded-lg bg-muted/25">
      <div className="px-4 py-3">
        <h2 className="text-lg font-semibold">Schedule</h2>
        <p className="text-sm text-muted-foreground">The next 90 days of Testing Lab events.</p>
      </div>
      {scheduled.length === 0 ? (
        <p className="p-4 text-sm text-muted-foreground">No Testing Lab events are scheduled in this period.</p>
      ) : (
        <ol className="divide-y divide-border/50">
          {scheduled.map((event) => {
            const startsAt = eventStart(event);
            return (
              <li key={event.id} className="flex items-center gap-4 p-4">
                <time className="w-28 shrink-0 text-sm text-muted-foreground" dateTime={startsAt?.toISOString()}>
                  {startsAt ? format(startsAt, 'EEE, MMM d - p') : 'Unscheduled'}
                </time>
                <div className="min-w-0 flex-1">
                  <EventLink event={event} analytics={event.id ? analyticsByEvent.get(event.id) : undefined} compact />
                </div>
                <Badge variant="outline">{formatTestingEventStatus(event.status)}</Badge>
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
  const months = Array.from({ length: 12 }, (_, index) => new Date(anchor.getFullYear(), index, 1));

  return (
    <section aria-label="Testing Lab year" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
      {months.map((month) => {
        const monthEvents = events.filter((event) => {
          const startsAt = eventStart(event);
          return startsAt && isSameMonth(startsAt, month);
        });
        return (
          <div key={month.toISOString()} className="rounded-lg bg-muted/25 p-3">
            <h2 className="text-sm font-semibold">{format(month, 'MMMM')}</h2>
            {monthEvents.length === 0 ? (
              <p className="mt-3 text-sm text-muted-foreground">No events</p>
            ) : (
              <div className="mt-3 space-y-2">
                {monthEvents.slice(0, 4).map((event) => (
                  <EventLink
                    key={event.id}
                    event={event}
                    analytics={event.id ? analyticsByEvent.get(event.id) : undefined}
                  />
                ))}
                {monthEvents.length > 4 ? (
                  <p className="text-xs text-muted-foreground">+{monthEvents.length - 4} more events</p>
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
  view: Exclude<CalendarView, 'year' | 'schedule'>;
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
    ? ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']
    : ['Mon', 'Tue', 'Wed', 'Thu', 'Fri'];
  const monthStart = startOfMonth(anchor);

  return (
    <div className="overflow-x-auto rounded-lg border border-border/60">
      <section aria-label={`${viewLabels[view]} Testing Lab calendar`} className="min-w-[760px] overflow-hidden">
        <div
          className="grid border-b border-border/50 text-center text-xs font-medium text-muted-foreground"
          style={{
            gridTemplateColumns: `repeat(${weekdays.length}, minmax(0, 1fr))`,
          }}
        >
          {weekdays.map((weekday) => (
            <div key={weekday} className="p-2">
              {weekday}
            </div>
          ))}
        </div>
        <div
          className="grid"
          style={{
            gridTemplateColumns: `repeat(${weekdays.length}, minmax(0, 1fr))`,
          }}
        >
          {range.days.map((day) => {
            const key = format(day, 'yyyy-MM-dd');
            const segments = segmentsByDay.get(key) ?? [];
            const outsideMonth = view === 'month' && !isSameMonth(day, monthStart);
            return (
              <div
                key={key}
                className={`group relative min-h-32 border-b border-r border-border/40 p-2 last:border-r-0 ${outsideMonth ? 'bg-muted/25 text-muted-foreground' : ''}`}
              >
                <button
                  type="button"
                  aria-label={`Create event on ${format(day, 'MMMM d, yyyy')}`}
                  className="absolute inset-0 z-0 rounded-none transition-colors hover:bg-muted/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring"
                  onClick={() => onCreateEvent(new Date(day))}
                />
                <time
                  dateTime={day.toISOString()}
                  className="pointer-events-none relative z-10 mb-2 block text-xs font-medium"
                >
                  {format(day, 'd')}
                </time>
                <div className="relative z-10 space-y-1">
                  {segments.slice(0, 3).map((segment) => (
                    <EventLink
                      key={`${segment.event.id}-${segment.dayKey}`}
                      event={segment.event}
                      analytics={segment.event.id ? analyticsByEvent.get(segment.event.id) : undefined}
                      compact
                    />
                  ))}
                  {segments.length > 3 ? (
                    <p className="text-xs text-muted-foreground">+{segments.length - 3} more</p>
                  ) : null}
                </div>
              </div>
            );
          })}
        </div>
      </section>
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
  const [view, setView] = useState<CalendarView>('schedule');
  const [anchor, setAnchor] = useState(() => initialDate);
  const [showWeekends, setShowWeekends] = useState(true);
  const [createDate, setCreateDate] = useState<Date | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const range = calendarRange(anchor, view, showWeekends);
  const analyticsByEvent = useMemo(
    () => new Map(eventAnalytics.map((analytics) => [analytics.eventId, analytics])),
    [eventAnalytics],
  );

  function openCreateEvent(date: Date | null) {
    setCreateDate(date);
    setCreateOpen(true);
  }

  return (
    <section aria-label="Testing Lab calendar" className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <div className="flex items-center gap-2">
          <Button
            type="button"
            variant="outline"
            size="icon"
            className="size-11 sm:size-9"
            aria-label="Previous period"
            onClick={() => setAnchor((date) => shiftCalendarAnchor(date, view, -1))}
          >
            <ChevronLeft className="size-4" />
          </Button>
          <Button className="h-11 sm:h-9" type="button" variant="outline" onClick={() => setAnchor(new Date())}>
            Today
          </Button>
          <Button
            type="button"
            variant="outline"
            size="icon"
            className="size-11 sm:size-9"
            aria-label="Next period"
            onClick={() => setAnchor((date) => shiftCalendarAnchor(date, view, 1))}
          >
            <ChevronRight className="size-4" />
          </Button>
        </div>
        <h2 className="min-w-44 text-lg font-semibold">{calendarRangeLabel(anchor, view, range)}</h2>
        <label className="ml-auto inline-flex min-h-11 items-center gap-2 rounded-md border px-3 text-sm font-medium sm:min-h-9">
          <span className="sr-only">Calendar view</span>
          <select
            aria-label="Calendar view"
            value={view}
            onChange={(event) => setView(event.target.value as CalendarView)}
            className="bg-transparent outline-none"
          >
            {calendarViews.map((value) => (
              <option key={value} value={value}>
                {viewLabels[value]}
              </option>
            ))}
          </select>
        </label>
        {view !== 'schedule' && view !== 'year' ? (
          <Button
            className="h-11 sm:h-9"
            type="button"
            variant="outline"
            aria-pressed={showWeekends}
            onClick={() => setShowWeekends((visible) => !visible)}
          >
            {showWeekends ? 'Hide weekends' : 'Show weekends'}
          </Button>
        ) : null}
      </div>

      {view === 'schedule' ? (
        <ScheduleView events={events} analyticsByEvent={analyticsByEvent} anchor={anchor} />
      ) : null}
      {view === 'year' ? <YearView events={events} analyticsByEvent={analyticsByEvent} anchor={anchor} /> : null}
      {view !== 'schedule' && view !== 'year' ? (
        <>
          <p className="text-xs text-muted-foreground md:hidden">Swipe horizontally to view every day.</p>
          <GridView
            events={events}
            analyticsByEvent={analyticsByEvent}
            anchor={anchor}
            view={view}
            showWeekends={showWeekends}
            onCreateEvent={(date) => openCreateEvent(date)}
          />
        </>
      ) : null}

      <CreateTestingEventDialog
        key={createDate?.toISOString() ?? 'toolbar'}
        initialDate={createDate ?? undefined}
        open={createOpen}
        onOpenChange={setCreateOpen}
        showTrigger={false}
      />
    </section>
  );
}
