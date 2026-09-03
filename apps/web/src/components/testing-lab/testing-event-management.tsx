"use client";

import {
  archiveTestingEvent,
  addTestingEventCommitteeMember,
  assignTestedProjectToRegistration,
  approveTestingEventApplication,
  beginTestingEventApplicationReview,
  configureTestingEventLearning,
  createTestingEvent,
  createTestingEventSlot,
  deleteTestingEvent,
  deleteTestingEventSlot,
  rejectTestingEventApplication,
  removeTestingEventCommitteeMember,
  restoreTestingEvent,
  transitionTestingEvent,
  updateTestingEventAttendance,
  updateTestingEvent,
  updateTestingEventSlot,
  voteOnTestingEventApplication,
  waitlistTestingEventApplication,
  type TestingEventActionResult,
} from "@/lib/testing-lab/events-actions";
import { formatWallClockInTimeZone } from "@/lib/date-time-zone";
import { formatEventDateTime } from "@/lib/testing-lab/event-workspace";
import { formatTestingEventStatus } from "@/lib/testing-lab/format";
import type {
  TestingLabTestingEventCommitteeMemberProjection,
  TestingLabTestingEventProjection,
  TestingLabTestingEventSlotProjection,
  TestingLabTestingEventStatus,
  TestingLabTestingProjectApplicationProjection,
  TestingLabTestingSlotRegistrationProjection,
  TestingLabTestingEventTemplateProjection,
} from "@game-guild/client";
import { Alert, AlertDescription } from "@game-guild/ui/components/alert";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@game-guild/ui/components/alert-dialog";
import { Badge } from "@game-guild/ui/components/badge";
import { Button } from "@game-guild/ui/components/button";
import { DateTimePicker } from "@/components/ui/date-time-picker";
import { DateTimeRangePicker } from "@/components/ui/date-time-range-picker";
import { TimeZoneCombobox } from "@/components/ui/time-zone-combobox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@game-guild/ui/components/dialog";
import { Input } from "@game-guild/ui/components/input";
import { Label } from "@game-guild/ui/components/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@game-guild/ui/components/select";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from "@game-guild/ui/components/sheet";
import { Textarea } from "@game-guild/ui/components/textarea";
import {
  AlertCircle,
  Archive,
  CheckCircle2,
  CircleStop,
  ClipboardCheck,
  Clock3,
  Pencil,
  Play,
  Plus,
  RotateCcw,
  Send,
  ShieldCheck,
  Trash2,
  UserRoundCheck,
} from "lucide-react";
import { useRouter } from "next/navigation";
import {
  useRef,
  useState,
  useTransition,
  type FormEvent,
  type ReactNode,
} from "react";

type Action = (
  formData: FormData,
) => Promise<TestingEventActionResult<unknown>>;

export interface TestingLabMemberOption {
  id: string;
  label: string;
}

export interface TestingLabApprovedApplicationOption {
  id: string;
  label: string;
  slotId?: string | null;
  eligibleTesterUserIds: string[];
}

export interface TestingLabLearningActivityOption {
  id: string;
  courseId: string;
  label: string;
}

function apiDatetimeLocal(value?: string | null, timeZoneId = "UTC") {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.valueOf())) return "";
  return formatWallClockInTimeZone(date, timeZoneId);
}

function localDatetime(date: Date) {
  const local = new Date(date.valueOf() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
}

type TestingEventSchedule = {
  applicationsOpenAt: string;
  applicationsCloseAt: string;
  startsAt: string;
  endsAt: string;
};

const Hour = 60 * 60 * 1000;
const Day = 24 * Hour;

function createTestingEventSchedule(
  now = new Date(),
  eventDate?: Date,
): TestingEventSchedule {
  const applicationsOpenAt = new Date(now);
  applicationsOpenAt.setMinutes(0, 0, 0);
  applicationsOpenAt.setHours(applicationsOpenAt.getHours() + 1);

  let startsAt = eventDate
    ? new Date(
        eventDate.getFullYear(),
        eventDate.getMonth(),
        eventDate.getDate(),
        10,
      )
    : new Date(applicationsOpenAt.valueOf() + 2 * Day);
  const minimumStart = new Date(applicationsOpenAt.valueOf() + 2 * Hour);
  if (startsAt <= minimumStart) startsAt = minimumStart;

  let applicationsCloseAt = new Date(startsAt.valueOf() - Hour);
  if (applicationsCloseAt <= applicationsOpenAt) {
    applicationsCloseAt = new Date(applicationsOpenAt.valueOf() + Hour);
    startsAt = new Date(applicationsCloseAt.valueOf() + Hour);
  }
  const endsAt = new Date(startsAt.valueOf() + 2 * Hour);

  return {
    applicationsOpenAt: localDatetime(applicationsOpenAt),
    applicationsCloseAt: localDatetime(applicationsCloseAt),
    startsAt: localDatetime(startsAt),
    endsAt: localDatetime(endsAt),
  };
}

function scheduleDate(value: string) {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.valueOf()) ? null : date;
}

function updateTestingEventSchedule(
  current: TestingEventSchedule,
  field: keyof TestingEventSchedule,
  value: string,
): TestingEventSchedule {
  const next = { ...current, [field]: value };
  const openAt = scheduleDate(next.applicationsOpenAt);
  let closeAt = scheduleDate(next.applicationsCloseAt);
  let startsAt = scheduleDate(next.startsAt);

  if (
    field === "applicationsOpenAt" &&
    openAt &&
    (!closeAt || closeAt <= openAt)
  ) {
    closeAt = new Date(openAt.valueOf() + Day);
    next.applicationsCloseAt = localDatetime(closeAt);
  }

  if (closeAt && (!startsAt || startsAt < closeAt)) {
    startsAt = new Date(closeAt.valueOf() + Day);
    next.startsAt = localDatetime(startsAt);
  }

  if (field === "startsAt" && startsAt) {
    next.endsAt = localDatetime(new Date(startsAt.valueOf() + 2 * Hour));
    return next;
  }

  const endsAt = scheduleDate(next.endsAt);
  if (startsAt && (!endsAt || endsAt <= startsAt)) {
    next.endsAt = localDatetime(new Date(startsAt.valueOf() + 2 * Hour));
  }

  return next;
}

function ActionMessage({
  result,
}: {
  result: TestingEventActionResult<unknown> | null;
}) {
  if (!result) return null;
  return (
    <Alert
      variant={result.success ? "default" : "destructive"}
      aria-live="polite"
    >
      {result.success ? (
        <CheckCircle2 className="size-4" />
      ) : (
        <AlertCircle className="size-4" />
      )}
      <AlertDescription>
        {result.success ? result.message : result.error}
      </AlertDescription>
    </Alert>
  );
}

function EventActionDialog({
  trigger,
  title,
  description,
  submitLabel,
  action,
  children,
  destructive = false,
  successHref,
}: {
  trigger: ReactNode;
  title: string;
  description: string;
  submitLabel: string;
  action: Action;
  children: ReactNode;
  destructive?: boolean;
  successHref?: string;
}) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [pending, startTransition] = useTransition();
  const [result, setResult] =
    useState<TestingEventActionResult<unknown> | null>(null);

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    startTransition(async () => {
      const next = await action(data);
      setResult(next);
      if (next.success) {
        form.reset();
        if (successHref) router.push(successHref);
        else router.refresh();
        window.setTimeout(() => setOpen(false), 450);
      }
    });
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) setResult(null);
      }}
    >
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <form onSubmit={submit} className="space-y-5">
          <DialogHeader>
            <DialogTitle>{title}</DialogTitle>
            <DialogDescription>{description}</DialogDescription>
          </DialogHeader>
          {children}
          <ActionMessage result={result} />
          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              disabled={pending}
              onClick={() => setOpen(false)}
            >
              Cancel
            </Button>
            <Button
              type="submit"
              variant={destructive ? "destructive" : "default"}
              disabled={pending}
            >
              {pending ? "Working..." : submitLabel}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

type EventFieldsProps = {
  event?: TestingLabTestingEventProjection;
  schedule?: TestingEventSchedule;
  onScheduleChange?: (field: keyof TestingEventSchedule, value: string) => void;
  timeZoneId?: string;
  stacked?: boolean;
};

function EventIdentityFields({
  event,
  includeBrief = true,
  compact = false,
}: {
  event?: TestingLabTestingEventProjection;
  includeBrief?: boolean;
  compact?: boolean;
}) {
  const fieldSuffix = event?.id ?? "new";

  return (
    <div className={`grid sm:grid-cols-2 ${compact ? "gap-3" : "gap-4"}`}>
      <div className="space-y-2 sm:col-span-2">
        <Label
          className={compact ? "sr-only" : undefined}
          htmlFor={`event-name-${fieldSuffix}`}
        >
          Event name
        </Label>
        <Input
          id={`event-name-${fieldSuffix}`}
          name="name"
          required
          placeholder={compact ? "Event name" : undefined}
          defaultValue={event?.name ?? ""}
          className={
            compact
              ? "h-12 rounded-none border-x-0 border-t-0 bg-transparent px-0 text-lg font-semibold shadow-none focus-visible:ring-0"
              : undefined
          }
        />
      </div>
      <div className="space-y-2">
        <Label htmlFor={`event-format-${fieldSuffix}`}>Event format</Label>
        <Select name="mode" defaultValue={event?.mode ?? "Online"}>
          <SelectTrigger id={`event-format-${fieldSuffix}`} className="w-full">
            <SelectValue>
              {(value: string | null) =>
                value === "InPerson"
                  ? "In person"
                  : value === "Hybrid"
                    ? "Hybrid"
                    : "Online"
              }
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="Online">Online</SelectItem>
            <SelectItem value="InPerson">In person</SelectItem>
            <SelectItem value="Hybrid">Hybrid</SelectItem>
          </SelectContent>
        </Select>
      </div>
      <div className="space-y-2">
        <Label htmlFor={`project-review-${fieldSuffix}`}>Project review</Label>
        <Select
          name="approvalMode"
          defaultValue={event?.approvalMode ?? "ManagerOnly"}
        >
          <SelectTrigger
            id={`project-review-${fieldSuffix}`}
            className="w-full"
          >
            <SelectValue>
              {(value: string | null) =>
                value === "Committee"
                  ? "Review committee votes"
                  : "Event managers decide"
              }
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="ManagerOnly">Event managers decide</SelectItem>
            <SelectItem value="Committee">Review committee votes</SelectItem>
          </SelectContent>
        </Select>
      </div>
      {includeBrief ? (
        <div className="space-y-2 sm:col-span-2">
          <Label htmlFor={`event-description-${fieldSuffix}`}>
            Purpose and tester brief
          </Label>
          <Textarea
            id={`event-description-${fieldSuffix}`}
            name="description"
            rows={3}
            defaultValue={event?.description ?? ""}
          />
        </div>
      ) : null}
    </div>
  );
}

function EventTimelineFields({
  event,
  schedule,
  onScheduleChange,
  timeZoneId,
  stacked = false,
}: EventFieldsProps) {
  const eventTimeZone = timeZoneId ?? event?.timeZoneId ?? "UTC";
  const applicationsOpenAt =
    schedule?.applicationsOpenAt ??
    apiDatetimeLocal(event?.applicationsOpenAt, eventTimeZone);
  const applicationsCloseAt =
    schedule?.applicationsCloseAt ??
    apiDatetimeLocal(event?.applicationsCloseAt, eventTimeZone);
  const startsAt =
    schedule?.startsAt ?? apiDatetimeLocal(event?.startsAt, eventTimeZone);
  const endsAt =
    schedule?.endsAt ?? apiDatetimeLocal(event?.endsAt, eventTimeZone);

  const fieldSuffix = event?.id ?? "new";

  function changeRange(
    startField: "applicationsOpenAt" | "startsAt",
    endField: "applicationsCloseAt" | "endsAt",
    next: { start: string; end: string },
    currentStart: string,
    currentEnd: string,
  ) {
    if (next.start !== currentStart) {
      onScheduleChange?.(startField, next.start);
    }
    if (next.end !== currentEnd) {
      onScheduleChange?.(endField, next.end);
    }
  }

  return (
    <div className={stacked ? "grid gap-4" : "grid gap-4 md:grid-cols-2"}>
      <div className="min-w-0 space-y-2">
        <Label htmlFor={`applications-window-${fieldSuffix}`}>
          Application window
        </Label>
        <DateTimeRangePicker
          id={`applications-window-${fieldSuffix}`}
          label="Application window"
          startName="applicationsOpenAt"
          endName="applicationsCloseAt"
          timeZoneId={eventTimeZone}
          required
          value={
            schedule
              ? { start: applicationsOpenAt, end: applicationsCloseAt }
              : undefined
          }
          defaultValue={{ start: applicationsOpenAt, end: applicationsCloseAt }}
          onValueChange={(next) =>
            changeRange(
              "applicationsOpenAt",
              "applicationsCloseAt",
              next,
              applicationsOpenAt,
              applicationsCloseAt,
            )
          }
        />
      </div>
      <div className="min-w-0 space-y-2">
        <Label htmlFor={`event-schedule-${fieldSuffix}`}>Event schedule</Label>
        <DateTimeRangePicker
          id={`event-schedule-${fieldSuffix}`}
          label="Event schedule"
          startName="startsAt"
          endName="endsAt"
          timeZoneId={eventTimeZone}
          required
          value={schedule ? { start: startsAt, end: endsAt } : undefined}
          defaultValue={{ start: startsAt, end: endsAt }}
          onValueChange={(next) =>
            changeRange("startsAt", "endsAt", next, startsAt, endsAt)
          }
        />
      </div>
    </div>
  );
}

function EventFeedbackField({
  event,
}: {
  event?: TestingLabTestingEventProjection;
}) {
  return (
    <label className="flex items-start gap-3 rounded-md bg-muted/30 p-3 text-sm">
      <input
        name="requiresFeedback"
        type="checkbox"
        defaultChecked={event?.requiresFeedback ?? true}
        className="mt-1"
      />
      <span>
        <strong className="block font-medium">Require tester feedback</strong>
        <span className="text-muted-foreground">
          Testers must submit project feedback before attendance is complete.
        </span>
      </span>
    </label>
  );
}

function EventFields({
  event,
  schedule,
  onScheduleChange,
  timeZoneId,
}: EventFieldsProps) {
  return (
    <div className="space-y-6">
      {event?.id ? (
        <input type="hidden" name="eventId" value={event.id} />
      ) : null}
      <input
        type="hidden"
        name="timeZoneId"
        value={timeZoneId ?? event?.timeZoneId ?? "UTC"}
      />
      <EventIdentityFields event={event} />
      <EventTimelineFields
        event={event}
        schedule={schedule}
        onScheduleChange={onScheduleChange}
        timeZoneId={timeZoneId}
      />
      <EventFeedbackField event={event} />
    </div>
  );
}

function EventRecurrenceFields({
  onDirty,
  startDate,
}: {
  onDirty: () => void;
  startDate: string;
}) {
  const allDays = [
    "Sunday",
    "Monday",
    "Tuesday",
    "Wednesday",
    "Thursday",
    "Friday",
    "Saturday",
  ];
  const displayDays = [
    "Monday",
    "Tuesday",
    "Wednesday",
    "Thursday",
    "Friday",
    "Saturday",
    "Sunday",
  ];
  const parsedStart = new Date(startDate);
  const startDay = Number.isNaN(parsedStart.valueOf())
    ? "Monday"
    : allDays[parsedStart.getDay()]!;
  const startDayOfMonth = Number.isNaN(parsedStart.valueOf())
    ? 1
    : parsedStart.getDate();
  const [repeatOption, setRepeatOption] = useState("none");
  const [customFrequency, setCustomFrequency] = useState("Weekly");
  const [customDays, setCustomDays] = useState<string[]>([startDay]);
  const [endMode, setEndMode] = useState("count");
  const frequency =
    repeatOption === "custom"
      ? customFrequency
      : repeatOption === "none"
        ? ""
        : repeatOption;

  function toggleCustomDay(day: string) {
    setCustomDays((current) =>
      current.includes(day)
        ? current.filter((value) => value !== day)
        : [...current, day],
    );
    onDirty();
  }

  return (
    <section aria-labelledby="event-recurrence-heading" className="space-y-3">
      <div className="space-y-2">
        <Label id="event-recurrence-heading" htmlFor="event-recurrence">
          Repeats
        </Label>
        <Select
          value={repeatOption}
          onValueChange={(value) => {
            setRepeatOption(value);
            onDirty();
          }}
        >
          <SelectTrigger id="event-recurrence" className="w-full">
            <SelectValue>
              {(value: string | null) =>
                value === "Daily"
                  ? "Daily"
                  : value === "Weekly"
                    ? `Weekly on ${startDay}`
                    : value === "Monthly"
                      ? `Monthly on day ${startDayOfMonth}`
                      : value === "custom"
                        ? "Custom…"
                        : "Does not repeat"
              }
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="none">Does not repeat</SelectItem>
            <SelectItem value="Daily">Daily</SelectItem>
            <SelectItem value="Weekly">Weekly on {startDay}</SelectItem>
            <SelectItem value="Monthly">
              Monthly on day {startDayOfMonth}
            </SelectItem>
            <SelectItem value="custom">Custom…</SelectItem>
          </SelectContent>
        </Select>
        <input type="hidden" name="recurrenceFrequency" value={frequency} />
      </div>

      {frequency ? (
        <div className="space-y-3 rounded-md bg-muted/30 p-3">
          {repeatOption === "custom" ? (
            <div className="space-y-2">
              <Label htmlFor="recurrence-interval">Repeat every</Label>
              <div className="grid grid-cols-[5rem_minmax(0,1fr)] gap-2">
                <Input
                  id="recurrence-interval"
                  name="recurrenceInterval"
                  type="number"
                  min="1"
                  max="52"
                  defaultValue="1"
                  required
                />
                <Select
                  value={customFrequency}
                  onValueChange={(value) => {
                    setCustomFrequency(value);
                    onDirty();
                  }}
                >
                  <SelectTrigger aria-label="Repeat unit" className="w-full">
                    <SelectValue>
                      {(value: string | null) =>
                        value === "Daily"
                          ? "Day(s)"
                          : value === "Monthly"
                            ? "Month(s)"
                            : "Week(s)"
                      }
                    </SelectValue>
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Daily">Day(s)</SelectItem>
                    <SelectItem value="Weekly">Week(s)</SelectItem>
                    <SelectItem value="Monthly">Month(s)</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
          ) : (
            <input type="hidden" name="recurrenceInterval" value="1" />
          )}

          {frequency === "Weekly" ? (
            repeatOption === "custom" ? (
              <fieldset className="space-y-2">
                <legend className="text-sm font-medium">Repeat on</legend>
                <div className="flex flex-wrap gap-1.5">
                  {displayDays.map((day) => {
                    const selected = customDays.includes(day);
                    return (
                      <label
                        key={day}
                        className={`flex size-9 cursor-pointer items-center justify-center rounded-full text-xs font-medium transition-colors ${selected ? "bg-primary text-primary-foreground" : "bg-background text-muted-foreground hover:text-foreground"}`}
                      >
                        <input
                          className="sr-only"
                          name="recurrenceDaysOfWeek"
                          type="checkbox"
                          value={day}
                          checked={selected}
                          onChange={() => toggleCustomDay(day)}
                        />
                        {day.slice(0, 1)}
                      </label>
                    );
                  })}
                </div>
              </fieldset>
            ) : (
              <input
                type="hidden"
                name="recurrenceDaysOfWeek"
                value={startDay}
              />
            )
          ) : null}

          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="recurrence-end-mode">Ends</Label>
              <Select
                value={endMode}
                onValueChange={(value) => {
                  setEndMode(value);
                  onDirty();
                }}
              >
                <SelectTrigger id="recurrence-end-mode" className="w-full">
                  <SelectValue>
                    {(value: string | null) =>
                      value === "date" ? "On a date" : "After"
                    }
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="count">After</SelectItem>
                  <SelectItem value="date">On a date</SelectItem>
                </SelectContent>
              </Select>
              <input type="hidden" name="recurrenceEndMode" value={endMode} />
            </div>
            {endMode === "date" ? (
              <div className="space-y-2">
                <Label htmlFor="recurrence-ends-at">End date</Label>
                <DateTimePicker
                  id="recurrence-ends-at"
                  name="recurrenceEndsAt"
                  required
                />
              </div>
            ) : (
              <div className="space-y-2">
                <Label htmlFor="recurrence-count">Number of events</Label>
                <Input
                  id="recurrence-count"
                  name="recurrenceOccurrenceCount"
                  type="number"
                  min="1"
                  max="104"
                  defaultValue="4"
                  required
                />
              </div>
            )}
          </div>
        </div>
      ) : null}
    </section>
  );
}

export interface CreateTestingEventDialogProps {
  initialDate?: Date;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  showTrigger?: boolean;
  templates?: TestingLabTestingEventTemplateProjection[];
  defaultTimeZone?: string;
}

export function CreateTestingEventDialog({
  initialDate,
  open: controlledOpen,
  onOpenChange,
  showTrigger = true,
  defaultTimeZone = "UTC",
}: CreateTestingEventDialogProps = {}) {
  const router = useRouter();
  const formRef = useRef<HTMLFormElement>(null);
  const [internalOpen, setInternalOpen] = useState(false);
  const open = controlledOpen ?? internalOpen;
  const [dirty, setDirty] = useState(false);
  const [discardOpen, setDiscardOpen] = useState(false);
  const [pending, startTransition] = useTransition();
  const [result, setResult] =
    useState<TestingEventActionResult<unknown> | null>(null);
  const [timeZoneId, setTimeZoneId] = useState(defaultTimeZone);
  const [schedule, setSchedule] = useState<TestingEventSchedule>(() =>
    createTestingEventSchedule(new Date(), initialDate),
  );
  function setOpen(next: boolean) {
    if (controlledOpen === undefined) setInternalOpen(next);
    onOpenChange?.(next);
  }

  function resetDraft() {
    formRef.current?.reset();
    setSchedule(createTestingEventSchedule(new Date(), initialDate));
    setTimeZoneId(defaultTimeZone);
    setDirty(false);
    setResult(null);
  }

  function closeDrawer() {
    resetDraft();
    setDiscardOpen(false);
    setOpen(false);
  }

  function requestClose() {
    if (pending) return;
    if (dirty) setDiscardOpen(true);
    else closeDrawer();
  }

  function trackChanges() {
    setDirty(true);
  }

  function changeSchedule(field: keyof TestingEventSchedule, value: string) {
    setDirty(true);
    setSchedule((current) => updateTestingEventSchedule(current, field, value));
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    startTransition(async () => {
      const next = await createTestingEvent(new FormData(form));
      if (next.success) {
        closeDrawer();
        router.refresh();
        return;
      }
      setResult(next);
    });
  }

  return (
    <>
      {showTrigger ? (
        <Button
          onClick={() => {
            resetDraft();
            setOpen(true);
          }}
        >
          <Plus className="mr-2 size-4" />
          New event
        </Button>
      ) : null}
      <Sheet
        open={open}
        onOpenChange={(next) => {
          if (next) setOpen(true);
          else requestClose();
        }}
      >
        <SheetContent
          side="right"
          className="gap-0 p-0 data-[side=right]:w-full! data-[side=right]:sm:max-w-lg!"
        >
          <form
            ref={formRef}
            onSubmit={submit}
            onChange={trackChanges}
            className="flex min-h-0 flex-1 flex-col"
          >
            <SheetHeader className="px-5 pb-3 pt-4">
              <SheetTitle>New testing event</SheetTitle>
              <SheetDescription className="sr-only">
                Create a testing event draft with its format, review process,
                schedule, time zone, and recurrence.
              </SheetDescription>
            </SheetHeader>
            <div className="min-h-0 flex-1 overflow-y-auto px-5 pb-5">
              {result ? <ActionMessage result={result} /> : null}
              <div className="space-y-5">
                <EventIdentityFields includeBrief={false} compact />

                <section
                  aria-labelledby="new-event-timeline-heading"
                  className="space-y-3 pt-1"
                >
                  <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                    <h3
                      id="new-event-timeline-heading"
                      className="text-sm font-medium"
                    >
                      Schedule
                    </h3>
                    <div className="min-w-0 sm:max-w-80 sm:flex-1">
                      <Label className="sr-only" htmlFor="new-event-time-zone">
                        Time zone
                      </Label>
                      <TimeZoneCombobox
                        id="new-event-time-zone"
                        value={timeZoneId}
                        onValueChange={(value) => {
                          setTimeZoneId(value);
                          setDirty(true);
                        }}
                      />
                    </div>
                  </div>
                  <EventTimelineFields
                    schedule={schedule}
                    onScheduleChange={changeSchedule}
                    timeZoneId={timeZoneId}
                    stacked
                  />
                </section>

                <EventRecurrenceFields
                  startDate={schedule.startsAt}
                  onDirty={() => setDirty(true)}
                />
                <input type="hidden" name="requiresFeedback" value="true" />
              </div>
            </div>
            <SheetFooter className="px-5 py-3 sm:flex-row sm:justify-end">
              <Button
                type="button"
                variant="outline"
                disabled={pending}
                onClick={requestClose}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={pending}>
                {pending ? "Creating event..." : "Create event"}
              </Button>
            </SheetFooter>
          </form>
        </SheetContent>
      </Sheet>
      <AlertDialog open={discardOpen} onOpenChange={setDiscardOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Discard testing event draft?</AlertDialogTitle>
            <AlertDialogDescription>
              Your unsaved event details and recurrence schedule will be lost.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Keep editing</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={closeDrawer}>
              Discard draft
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}

export function EditTestingEventDialog({
  event,
}: {
  event: TestingLabTestingEventProjection;
}) {
  return (
    <EventActionDialog
      trigger={
        <Button variant="outline">
          <Pencil className="mr-2 size-4" />
          Edit
        </Button>
      }
      title="Edit testing event"
      description="Adjust the event brief, review model, application window, and delivery dates."
      submitLabel="Save event"
      action={updateTestingEvent}
    >
      <EventFields event={event} timeZoneId={event.timeZoneId ?? "UTC"} />
    </EventActionDialog>
  );
}

export function CreateTestingEventSlotDialog({ eventId }: { eventId: string }) {
  return (
    <EventActionDialog
      trigger={
        <Button size="sm">
          <Plus className="mr-2 size-4" />
          Add slot
        </Button>
      }
      title="Add testing slot"
      description="A slot defines one test window and its independent tester and approved-project capacity."
      submitLabel="Create slot"
      action={createTestingEventSlot}
    >
      <input type="hidden" name="eventId" value={eventId} />
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <Label>Mode</Label>
          <Select name="mode" defaultValue="InPerson">
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="InPerson">In person</SelectItem>
              <SelectItem value="Online">Online</SelectItem>
              <SelectItem value="Hybrid">Hybrid</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-2">
          <Label htmlFor="slot-location">Saved location id</Label>
          <Input
            id="slot-location"
            name="locationId"
            placeholder="Optional location UUID"
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor="slot-start">Starts</Label>
          <DateTimePicker id="slot-start" name="startsAt" required />
        </div>
        <div className="space-y-2">
          <Label htmlFor="slot-end">Ends</Label>
          <DateTimePicker id="slot-end" name="endsAt" required />
        </div>
        <div className="space-y-2">
          <Label htmlFor="slot-campus">Campus</Label>
          <Input
            id="slot-campus"
            name="campusName"
            placeholder="Required in person"
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor="slot-room">Room</Label>
          <Input
            id="slot-room"
            name="roomName"
            placeholder="Required in person"
          />
        </div>
        <div className="space-y-2 sm:col-span-2">
          <Label htmlFor="slot-url">Meeting URL</Label>
          <Input
            id="slot-url"
            name="meetingUrl"
            type="url"
            placeholder="Required online"
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor="slot-testers">Tester capacity</Label>
          <Input
            id="slot-testers"
            name="maxTesters"
            type="number"
            min="1"
            placeholder="Unlimited"
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor="slot-projects">Approved project capacity</Label>
          <Input
            id="slot-projects"
            name="maxProjects"
            type="number"
            min="1"
            placeholder="Unlimited"
          />
        </div>
      </div>
    </EventActionDialog>
  );
}

export function ManageTestingEventSlotDialog({
  eventId,
  slot,
}: {
  eventId: string;
  slot: TestingLabTestingEventSlotProjection;
}) {
  if (!slot.id) return null;
  return (
    <EventActionDialog
      trigger={
        <Button size="sm" variant="outline">
          <Pencil className="mr-2 size-4" />
          Edit slot
        </Button>
      }
      title="Edit testing slot"
      description="Change this slot without affecting the schedules and capacity of other slots."
      submitLabel="Save slot"
      action={updateTestingEventSlot}
    >
      <input type="hidden" name="eventId" value={eventId} />
      <input type="hidden" name="slotId" value={slot.id} />
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <Label>Mode</Label>
          <Select name="mode" defaultValue={slot.mode ?? "Online"}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="InPerson">In person</SelectItem>
              <SelectItem value="Online">Online</SelectItem>
              <SelectItem value="Hybrid">Hybrid</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-2">
          <Label htmlFor={`slot-location-${slot.id}`}>Saved location id</Label>
          <Input
            id={`slot-location-${slot.id}`}
            name="locationId"
            defaultValue={slot.locationId ?? ""}
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor={`slot-start-${slot.id}`}>Starts</Label>
          <DateTimePicker
            id={`slot-start-${slot.id}`}
            name="startsAt"
            required
            defaultValue={apiDatetimeLocal(slot.startsAt)}
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor={`slot-end-${slot.id}`}>Ends</Label>
          <DateTimePicker
            id={`slot-end-${slot.id}`}
            name="endsAt"
            required
            defaultValue={apiDatetimeLocal(slot.endsAt)}
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor={`slot-campus-${slot.id}`}>Campus</Label>
          <Input
            id={`slot-campus-${slot.id}`}
            name="campusName"
            defaultValue={slot.campusName ?? ""}
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor={`slot-room-${slot.id}`}>Room</Label>
          <Input
            id={`slot-room-${slot.id}`}
            name="roomName"
            defaultValue={slot.roomName ?? ""}
          />
        </div>
        <div className="space-y-2 sm:col-span-2">
          <Label htmlFor={`slot-url-${slot.id}`}>Meeting URL</Label>
          <Input
            id={`slot-url-${slot.id}`}
            name="meetingUrl"
            type="url"
            defaultValue={slot.meetingUrl ?? ""}
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor={`slot-testers-${slot.id}`}>Tester capacity</Label>
          <Input
            id={`slot-testers-${slot.id}`}
            name="maxTesters"
            type="number"
            min="1"
            defaultValue={slot.maxTesters ?? ""}
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor={`slot-projects-${slot.id}`}>
            Approved project capacity
          </Label>
          <Input
            id={`slot-projects-${slot.id}`}
            name="maxProjects"
            type="number"
            min="1"
            defaultValue={slot.maxProjects ?? ""}
          />
        </div>
      </div>
      <div className="border-t pt-4">
        <EventActionDialog
          trigger={
            <Button type="button" size="sm" variant="destructive">
              <Trash2 className="mr-2 size-4" />
              Delete slot
            </Button>
          }
          title="Delete this testing slot?"
          description="A slot with approved projects or tester registrations cannot be deleted."
          submitLabel="Delete slot"
          action={deleteTestingEventSlot}
          destructive
        >
          <input type="hidden" name="eventId" value={eventId} />
          <input type="hidden" name="slotId" value={slot.id} />
        </EventActionDialog>
      </div>
    </EventActionDialog>
  );
}

export function TestingEventLifecycleActions({
  event,
}: {
  event: TestingLabTestingEventProjection;
}) {
  const [pending, startTransition] = useTransition();
  const [result, setResult] =
    useState<TestingEventActionResult<unknown> | null>(null);
  const nextByStatus: Partial<
    Record<TestingLabTestingEventStatus, [string, string, typeof Send]>
  > = {
    Draft: ["open-applications", "Open applications", Send],
    ApplicationsOpen: ["close-applications", "Close applications", CircleStop],
    ApplicationsClosed: ["schedule", "Schedule event", Clock3],
    Scheduled: ["activate", "Start event", Play],
    Active: ["complete", "Complete event", CheckCircle2],
  };
  const next = nextByStatus[event.status ?? "Draft"];
  const configuration = event.configuration;
  const draftConfigurationReady = Boolean(
    configuration?.generalRules?.trim() &&
    configuration.candidateInstructions?.trim() &&
    configuration.testerInstructions?.trim() &&
    configuration.projectApplicationSchema &&
    configuration.testerRegistrationSchema,
  );

  function run(transition: string) {
    if (!event.id) return;
    const form = new FormData();
    form.set("eventId", event.id);
    form.set("transition", transition);
    startTransition(async () => setResult(await transitionTestingEvent(form)));
  }

  const NextIcon = next?.[2];
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap gap-2">
        {event.status === "Draft" && !draftConfigurationReady && event.id ? (
          <Button asChild size="sm">
            <a
              href={`/console/community/testing-lab/events/${event.id}/overview#event-configuration-heading`}
            >
              <Pencil className="mr-2 size-4" />
              Complete setup
            </a>
          </Button>
        ) : next && NextIcon ? (
          <Button size="sm" disabled={pending} onClick={() => run(next[0])}>
            <NextIcon className="mr-2 size-4" />
            {next[1]}
          </Button>
        ) : null}
        {!["Completed", "Cancelled"].includes(event.status ?? "") ? (
          <EventActionDialog
            trigger={
              <Button size="sm" variant="outline">
                <CircleStop className="mr-2 size-4" />
                Cancel event
              </Button>
            }
            title="Cancel this testing event?"
            description="Cancellation preserves applications, attendance, feedback, and audit history."
            submitLabel="Cancel event"
            action={transitionTestingEvent}
            destructive
          >
            <input type="hidden" name="eventId" value={event.id} />
            <input type="hidden" name="transition" value="cancel" />
            <div className="space-y-2">
              <Label htmlFor="event-cancellation-reason">
                Cancellation reason
              </Label>
              <Textarea
                id="event-cancellation-reason"
                name="reason"
                required
                rows={4}
              />
            </div>
          </EventActionDialog>
        ) : null}
        {event.status === "Draft" && event.id ? (
          <EventActionDialog
            trigger={
              <Button size="sm" variant="destructive">
                <Trash2 className="mr-2 size-4" />
                Delete draft
              </Button>
            }
            title="Delete this draft event?"
            description="Only an unused draft can be deleted. This action cannot be undone."
            submitLabel="Delete draft"
            action={deleteTestingEvent}
            destructive
            successHref="/console/community/testing-lab/events"
          >
            <input type="hidden" name="eventId" value={event.id} />
          </EventActionDialog>
        ) : null}
        {["Completed", "Cancelled"].includes(event.status ?? "") && event.id ? (
          <EventActionDialog
            trigger={
              <Button size="sm" variant="outline">
                <Archive className="mr-2 size-4" />
                Archive event
              </Button>
            }
            title="Archive this testing event?"
            description="The event leaves the active directory while its audit history remains available for restoration."
            submitLabel="Archive event"
            action={archiveTestingEvent}
            successHref="/console/community/testing-lab/events"
          >
            <input type="hidden" name="eventId" value={event.id} />
          </EventActionDialog>
        ) : null}
      </div>
      <ActionMessage result={result} />
    </div>
  );
}

export function RestoreTestingEventDialog({
  event,
}: {
  event: TestingLabTestingEventProjection;
}) {
  if (!event.id) return null;
  return (
    <EventActionDialog
      trigger={
        <Button variant="outline">
          <RotateCcw className="mr-2 size-4" />
          Restore event
        </Button>
      }
      title="Restore this testing event?"
      description="The event returns to the active directory with its terminal status and audit history intact."
      submitLabel="Restore event"
      action={restoreTestingEvent}
    >
      <input type="hidden" name="eventId" value={event.id} />
    </EventActionDialog>
  );
}

export function TestingEventCommittee({
  event,
  members,
  committee,
  readOnly = false,
}: {
  event: TestingLabTestingEventProjection;
  members: TestingLabMemberOption[];
  committee: TestingLabTestingEventCommitteeMemberProjection[];
  readOnly?: boolean;
}) {
  return (
    <section>
      <div className="mb-3 flex items-center justify-between gap-3">
        <div>
          <h2 className="font-semibold">Review committee</h2>
          <p className="text-sm text-muted-foreground">
            {event.approvalMode === "Committee"
              ? "Committee votes inform approval; the manager resolves ties."
              : "Manager-only approval is active."}
          </p>
        </div>
        {event.id && !readOnly ? (
          <EventActionDialog
            trigger={
              <Button size="sm" variant="outline">
                <UserRoundCheck className="mr-2 size-4" />
                Add reviewer
              </Button>
            }
            title="Add committee reviewer"
            description="Only active tenant members can review project applications."
            submitLabel="Add reviewer"
            action={addTestingEventCommitteeMember}
          >
            <input type="hidden" name="eventId" value={event.id} />
            <div className="space-y-2">
              <Label>Member</Label>
              <Select name="userId" required>
                <SelectTrigger>
                  <SelectValue placeholder="Choose a member" />
                </SelectTrigger>
                <SelectContent>
                  {members.map((member) => (
                    <SelectItem key={member.id} value={member.id}>
                      {member.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" name="isChair" /> Committee chair
            </label>
          </EventActionDialog>
        ) : null}
      </div>
      {committee.length === 0 ? (
        <p className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
          No reviewers assigned.
        </p>
      ) : (
        <div className="divide-y rounded-md border">
          {committee.map((reviewer) => (
            <div
              key={reviewer.id ?? reviewer.userId}
              className="flex items-center justify-between gap-3 p-3"
            >
              <div className="min-w-0">
                <p className="truncate text-sm font-medium">
                  {reviewer.userName ?? reviewer.userEmail ?? reviewer.userId}
                </p>
                <p className="truncate text-xs text-muted-foreground">
                  {reviewer.userEmail}
                </p>
              </div>
              <div className="flex items-center gap-2">
                {reviewer.isChair ? (
                  <Badge variant="secondary">Chair</Badge>
                ) : null}
                {event.id && reviewer.userId && !readOnly ? (
                  <EventActionDialog
                    trigger={
                      <Button
                        size="icon"
                        variant="ghost"
                        aria-label={`Remove ${reviewer.userName ?? "reviewer"}`}
                      >
                        <Trash2 className="size-4" />
                      </Button>
                    }
                    title="Remove this reviewer?"
                    description="A reviewer with recorded votes cannot be removed from the audit trail."
                    submitLabel="Remove reviewer"
                    action={removeTestingEventCommitteeMember}
                    destructive
                  >
                    <input type="hidden" name="eventId" value={event.id} />
                    <input
                      type="hidden"
                      name="userId"
                      value={reviewer.userId}
                    />
                  </EventActionDialog>
                ) : null}
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

export interface TestingEventApplicationAccess {
  canViewApplications?: boolean;
  canManageApplications?: boolean;
  canVote?: boolean;
}

export function TestingEventApplications({
  eventId,
  access,
  applications,
  slots,
  projectLabels = {},
  memberLabels = {},
  readOnly = false,
}: {
  eventId: string;
  access: TestingEventApplicationAccess | null;
  applications: TestingLabTestingProjectApplicationProjection[];
  slots: TestingLabTestingEventSlotProjection[];
  readOnly?: boolean;
  projectLabels?: Record<string, string>;
  memberLabels?: Record<string, string>;
}) {
  const router = useRouter();
  const [reviewingApplicationId, setReviewingApplicationId] = useState<
    string | null
  >(null);
  const [reviewPending, startReviewTransition] = useTransition();
  const [reviewResult, setReviewResult] = useState<{
    applicationId: string;
    result: TestingEventActionResult<unknown>;
  } | null>(null);

  if (applications.length === 0) {
    return (
      <p className="rounded-md border border-dashed p-5 text-center text-sm text-muted-foreground">
        No project applications yet.
      </p>
    );
  }

  return (
    <div className="divide-y rounded-md border">
      {applications.map((application) => {
        const status = application.status ?? "Pending";
        const projectLabel = application.projectId
          ? (projectLabels[application.projectId] ??
            "Project details unavailable")
          : "Project details unavailable";
        const memberLabel = application.submittedByUserId
          ? (memberLabels[application.submittedByUserId] ??
            "Member details unavailable")
          : "Member details unavailable";
        return (
          <div
            key={application.id}
            className="flex flex-col gap-3 p-4 lg:flex-row lg:items-center lg:justify-between"
          >
            <div className="min-w-0">
              <p className="truncate font-medium">{projectLabel}</p>
              <p className="text-xs text-muted-foreground">
                Submitted by {memberLabel}
              </p>
              {application.decisionRationale ? (
                <p className="mt-1 text-sm text-muted-foreground">
                  {application.decisionRationale}
                </p>
              ) : null}
              {reviewResult?.applicationId === application.id ? (
                <div className="mt-3">
                  <ActionMessage result={reviewResult?.result ?? null} />
                </div>
              ) : null}
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="outline">
                {formatTestingEventStatus(status)}
              </Badge>
              {!readOnly &&
              access?.canManageApplications &&
              status === "Pending" &&
              application.id ? (
                <Button
                  size="sm"
                  variant="outline"
                  disabled={
                    reviewPending && reviewingApplicationId === application.id
                  }
                  onClick={() => {
                    setReviewingApplicationId(application.id!);
                    startReviewTransition(async () => {
                      const form = new FormData();
                      form.set("eventId", eventId);
                      form.set("applicationId", application.id!);
                      const result =
                        await beginTestingEventApplicationReview(form);
                      setReviewResult({
                        applicationId: application.id!,
                        result,
                      });
                      if (result.success) router.refresh();
                      setReviewingApplicationId(null);
                    });
                  }}
                >
                  {reviewPending && reviewingApplicationId === application.id
                    ? "Starting..."
                    : "Review"}
                </Button>
              ) : null}
              {!readOnly &&
              ["Pending", "UnderReview", "Waitlisted"].includes(status) &&
              application.id ? (
                <>
                  {access?.canManageApplications ? (
                    <>
                      <EventActionDialog
                        trigger={
                          <Button size="sm">
                            <CheckCircle2 className="mr-2 size-4" />
                            Approve
                          </Button>
                        }
                        title="Approve project application"
                        description="Capacity is reserved only after this approval is accepted."
                        submitLabel="Approve project"
                        action={approveTestingEventApplication}
                      >
                        <input type="hidden" name="eventId" value={eventId} />
                        <input
                          type="hidden"
                          name="applicationId"
                          value={application.id}
                        />
                        <div className="space-y-2">
                          <Label htmlFor={`approve-slot-${application.id}`}>
                            Testing slot
                          </Label>
                          <Select name="slotId" required>
                            <SelectTrigger
                              id={`approve-slot-${application.id}`}
                              aria-label="Testing slot"
                            >
                              <SelectValue placeholder="Choose a slot" />
                            </SelectTrigger>
                            <SelectContent>
                              {slots
                                .filter((slot) => slot.id)
                                .map((slot) => (
                                  <SelectItem key={slot.id} value={slot.id!}>
                                    {formatEventDateTime(slot.startsAt)} ·{" "}
                                    {slot.campusName ??
                                      slot.meetingUrl ??
                                      slot.mode}
                                  </SelectItem>
                                ))}
                            </SelectContent>
                          </Select>
                        </div>
                        <div className="space-y-2">
                          <Label
                            htmlFor={`approve-rationale-${application.id}`}
                          >
                            Decision notes
                          </Label>
                          <Textarea
                            id={`approve-rationale-${application.id}`}
                            name="rationale"
                            rows={3}
                          />
                        </div>
                      </EventActionDialog>
                      <EventActionDialog
                        trigger={
                          <Button size="sm" variant="outline">
                            Waitlist
                          </Button>
                        }
                        title="Waitlist project application"
                        description="The application remains eligible but does not consume project capacity."
                        submitLabel="Add to waitlist"
                        action={waitlistTestingEventApplication}
                      >
                        <input type="hidden" name="eventId" value={eventId} />
                        <input
                          type="hidden"
                          name="applicationId"
                          value={application.id}
                        />
                        <div className="space-y-2">
                          <Label
                            htmlFor={`waitlist-rationale-${application.id}`}
                          >
                            Notes
                          </Label>
                          <Textarea
                            id={`waitlist-rationale-${application.id}`}
                            name="rationale"
                            rows={3}
                          />
                        </div>
                      </EventActionDialog>
                      <EventActionDialog
                        trigger={
                          <Button size="sm" variant="destructive">
                            Reject
                          </Button>
                        }
                        title="Reject project application"
                        description="A clear rationale is required and remains in the application audit trail."
                        submitLabel="Reject project"
                        action={rejectTestingEventApplication}
                        destructive
                      >
                        <input type="hidden" name="eventId" value={eventId} />
                        <input
                          type="hidden"
                          name="applicationId"
                          value={application.id}
                        />
                        <div className="space-y-2">
                          <Label htmlFor={`reject-rationale-${application.id}`}>
                            Rejection rationale
                          </Label>
                          <Textarea
                            id={`reject-rationale-${application.id}`}
                            name="rationale"
                            required
                            rows={4}
                          />
                        </div>
                      </EventActionDialog>
                    </>
                  ) : null}
                  {access?.canVote && status === "UnderReview" ? (
                    <EventActionDialog
                      trigger={
                        <Button size="sm" variant="ghost">
                          <ShieldCheck className="mr-2 size-4" />
                          Vote
                        </Button>
                      }
                      title="Record committee vote"
                      description="Votes are auditable and cannot be silently replaced by another reviewer."
                      submitLabel="Record vote"
                      action={voteOnTestingEventApplication}
                    >
                      <input type="hidden" name="eventId" value={eventId} />
                      <input
                        type="hidden"
                        name="applicationId"
                        value={application.id}
                      />
                      <div className="space-y-2">
                        <Label>Vote</Label>
                        <Select name="decision" required>
                          <SelectTrigger>
                            <SelectValue placeholder="Choose decision" />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="Approve">Approve</SelectItem>
                            <SelectItem value="Reject">Reject</SelectItem>
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="space-y-2">
                        <Label htmlFor={`vote-comments-${application.id}`}>
                          Comments
                        </Label>
                        <Textarea
                          id={`vote-comments-${application.id}`}
                          name="comments"
                          rows={3}
                        />
                      </div>
                    </EventActionDialog>
                  ) : null}
                </>
              ) : null}
            </div>
          </div>
        );
      })}
    </div>
  );
}

export function TestingSlotRegistrations({
  eventId,
  registrations,
  memberLabels,
  approvedApplications,
  readOnly = false,
}: {
  eventId: string;
  registrations: TestingLabTestingSlotRegistrationProjection[];
  memberLabels: Record<string, string>;
  approvedApplications: TestingLabApprovedApplicationOption[];
  readOnly?: boolean;
}) {
  if (registrations.length === 0)
    return (
      <p className="text-sm text-muted-foreground">No tester registrations.</p>
    );
  return (
    <div className="mt-3 divide-y border-t">
      {registrations.map((registration) => {
        const testerLabel = registration.userId
          ? memberLabels[registration.userId]
          : undefined;
        const assignableProjects = approvedApplications.filter(
          (application) => {
            if (!registration.userId) return false;
            return (
              application.eligibleTesterUserIds.includes(registration.userId) &&
              (!application.slotId ||
                application.slotId === registration.slotId)
            );
          },
        );
        const canAssignProject = ["CheckedIn", "Attended"].includes(
          registration.status ?? "",
        );
        const isTerminalRegistration = [
          "Cancelled",
          "Completed",
          "NoShow",
        ].includes(registration.status ?? "");
        const pendingFeedbackCount = isTerminalRegistration
          ? 0
          : (registration.pendingFeedbackCount ?? 0);

        return (
          <div
            key={registration.id}
            className="flex flex-col gap-3 py-3 lg:flex-row lg:items-center lg:justify-between"
          >
            <div className="min-w-0">
              <p className="truncate text-sm font-medium">
                {testerLabel ?? "Unknown tester"}
              </p>
              <p className="text-xs text-muted-foreground">
                {pendingFeedbackCount} pending feedback /{" "}
                {formatTestingEventStatus(registration.status)}
              </p>
            </div>
            {registration.id && !readOnly && !isTerminalRegistration ? (
              <div className="flex flex-wrap items-center gap-2">
                {canAssignProject && assignableProjects.length > 0 ? (
                  <EventActionDialog
                    trigger={
                      <Button size="sm" variant="outline">
                        <ClipboardCheck className="mr-2 size-4" />
                        Assign tested project
                      </Button>
                    }
                    title="Assign a tested project"
                    description="Create the feedback obligation for this tester after check-in."
                    submitLabel="Assign project"
                    action={assignTestedProjectToRegistration}
                  >
                    <input type="hidden" name="eventId" value={eventId} />
                    <input
                      type="hidden"
                      name="registrationId"
                      value={registration.id}
                    />
                    <div className="space-y-2">
                      <Label>Approved project</Label>
                      <Select name="applicationId" required>
                        <SelectTrigger>
                          <SelectValue placeholder="Choose a project" />
                        </SelectTrigger>
                        <SelectContent>
                          {assignableProjects.map((application) => (
                            <SelectItem
                              key={application.id}
                              value={application.id}
                            >
                              {application.label}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                  </EventActionDialog>
                ) : null}
                <form
                  className="flex items-center gap-2"
                  action={async (formData) => {
                    await updateTestingEventAttendance(formData);
                  }}
                >
                  <input type="hidden" name="eventId" value={eventId} />
                  <input
                    type="hidden"
                    name="registrationId"
                    value={registration.id}
                  />
                  <Select name="attendance" required>
                    <SelectTrigger className="w-36">
                      <SelectValue placeholder="Attendance" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="check-in">Check in</SelectItem>
                      <SelectItem value="check-out">Check out</SelectItem>
                      <SelectItem value="no-show">No show</SelectItem>
                      <SelectItem value="complete">Complete</SelectItem>
                    </SelectContent>
                  </Select>
                  <Button size="sm" type="submit">
                    Update
                  </Button>
                </form>
              </div>
            ) : null}
          </div>
        );
      })}
    </div>
  );
}
export function TestingEventLearningDialog({
  event,
  activities,
  readOnly = false,
}: {
  event: TestingLabTestingEventProjection;
  activities: TestingLabLearningActivityOption[];
  readOnly?: boolean;
}) {
  const initialActivity = activities.find(
    (activity) => activity.id === event.learningActivityId,
  );
  const [selectedActivityId, setSelectedActivityId] = useState(
    initialActivity?.id ?? "",
  );
  const selectedActivity = activities.find(
    (activity) => activity.id === selectedActivityId,
  );

  if (!event.id || readOnly) return null;
  return (
    <EventActionDialog
      trigger={
        <Button size="sm" variant="outline">
          <Pencil className="mr-2 size-4" />
          Configure learning
        </Button>
      }
      title="Connect learning evidence"
      description="Testing Lab publishes completion evidence; Learning remains responsible for enrollment and grades."
      submitLabel="Save learning link"
      action={configureTestingEventLearning}
    >
      <input type="hidden" name="eventId" value={event.id} />
      <input
        type="hidden"
        name="courseId"
        value={selectedActivity?.courseId ?? event.courseId ?? ""}
      />
      <div className="space-y-2">
        <Label>Course activity</Label>
        <Select
          name="learningActivityId"
          required
          value={selectedActivityId}
          onValueChange={setSelectedActivityId}
        >
          <SelectTrigger>
            <SelectValue placeholder="Choose a lesson or graded activity" />
          </SelectTrigger>
          <SelectContent>
            {activities.map((activity) => (
              <SelectItem
                key={`${activity.courseId}:${activity.id}`}
                value={activity.id}
              >
                {activity.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="space-y-2">
        <Label htmlFor="learning-cohort">Cohort id</Label>
        <Input
          id="learning-cohort"
          name="cohortId"
          defaultValue={event.cohortId ?? ""}
          placeholder="Optional: restrict evidence to one cohort"
        />
      </div>
      <div className="space-y-2">
        <Label>Completion requirement</Label>
        <Select
          name="requirement"
          defaultValue={
            event.learningCompletionRequirement ?? "AttendanceAndFeedback"
          }
        >
          <SelectTrigger>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="Attendance">Attendance</SelectItem>
            <SelectItem value="Feedback">Required feedback</SelectItem>
            <SelectItem value="AttendanceAndFeedback">
              Attendance and feedback
            </SelectItem>
            <SelectItem value="ProjectTested">
              Assigned project tested
            </SelectItem>
          </SelectContent>
        </Select>
      </div>
    </EventActionDialog>
  );
}
