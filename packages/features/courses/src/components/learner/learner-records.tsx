"use client";

import { Badge } from "@game-guild/ui/components/badge";
import { Button } from "@game-guild/ui/components/button";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@game-guild/ui/components/card";
import {
  Award,
  CalendarDays,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Clock3,
  Download,
  ExternalLink,
  GraduationCap,
  List,
  Share2,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import type {
  LearnerAssessment,
  LearnerCertificate,
  LearnerCourseRecord,
  LearnerSubmission,
} from "./types";

function formatDate(value: string, locale = "en-US", timeZone = "UTC") {
  return new Intl.DateTimeFormat(locale, {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone,
  }).format(new Date(value));
}

function formatNumber(value: number) {
  return new Intl.NumberFormat("en-US", {
    maximumFractionDigits: 1,
  }).format(value);
}

function isFinalizedAwaitingRelease(submission: LearnerSubmission | undefined) {
  return (
    submission?.score == null && submission?.status?.toLowerCase() === "graded"
  );
}

function submissionResultLabel(
  submission: LearnerSubmission | undefined,
  assessment: LearnerAssessment,
) {
  if (submission?.score != null) {
    return `${submission.score} / ${assessment.maxScore ?? 0}`;
  }
  if (isFinalizedAwaitingRelease(submission)) {
    return "Graded, awaiting release";
  }
  return submission ? "Awaiting grade" : "Not submitted";
}

function buildCalendarEvents(records: LearnerCourseRecord[]) {
  return records
    .flatMap(({ course, context }) => [
      ...context.calendar.flatMap((entry) => {
        const date = entry.startsAt || entry.dueAt || entry.availableFrom;
        return date
          ? [
              {
                id: `cohort-${course.id}-${entry.itemId}`,
                date,
                title: entry.title || "Scheduled class",
                course: course.title,
                courseId: course.id,
                meta:
                  entry.cohortName || context.cohort?.name || "Course schedule",
                kind: entry.type || entry.itemType || "Class",
              },
            ]
          : [];
      }),
      ...context.assessments.flatMap((assessment) =>
        assessment.dueAt
          ? [
              {
                id: `assessment-${assessment.id}`,
                date: assessment.dueAt,
                title: assessment.title || "Assessment deadline",
                course: course.title,
                courseId: course.id,
                meta: assessment.assessmentGroupName || "Assessment",
                kind: "Deadline",
              },
            ]
          : [],
      ),
    ])
    .sort(
      (left, right) =>
        new Date(left.date).getTime() - new Date(right.date).getTime(),
    );
}

function startOfMonth(value: Date) {
  return new Date(value.getFullYear(), value.getMonth(), 1);
}

function monthGridDays(month: Date) {
  const first = startOfMonth(month);
  const firstGridDay = new Date(first);
  firstGridDay.setDate(first.getDate() - first.getDay());

  return Array.from({ length: 42 }, (_, index) => {
    const day = new Date(firstGridDay);
    day.setDate(firstGridDay.getDate() + index);
    return day;
  });
}

function sameDay(left: Date, right: Date) {
  return (
    left.getFullYear() === right.getFullYear() &&
    left.getMonth() === right.getMonth() &&
    left.getDate() === right.getDate()
  );
}

export function LearnerCalendar({
  records,
  locale = "en-US",
}: {
  records: LearnerCourseRecord[];
  locale?: string;
}) {
  const events = useMemo(() => buildCalendarEvents(records), [records]);
  const [view, setView] = useState<"agenda" | "month">("agenda");
  const [courseId, setCourseId] = useState("all");
  const [kind, setKind] = useState("all");
  const [timeZone, setTimeZone] = useState("UTC");
  const [month, setMonth] = useState(() =>
    startOfMonth(events[0] ? new Date(events[0].date) : new Date()),
  );

  useEffect(() => {
    setTimeZone(Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC");
  }, []);

  const courseOptions = useMemo(
    () =>
      Array.from(
        new Map(events.map((event) => [event.courseId, event.course] as const)),
      ),
    [events],
  );
  const kindOptions = useMemo(
    () => Array.from(new Set(events.map((event) => event.kind))).sort(),
    [events],
  );
  const filteredEvents = events.filter(
    (event) =>
      (courseId === "all" || event.courseId === courseId) &&
      (kind === "all" || event.kind === kind),
  );
  const monthLabel = new Intl.DateTimeFormat(locale, {
    month: "long",
    year: "numeric",
  }).format(month);
  const weekdays = Array.from({ length: 7 }, (_, day) =>
    new Intl.DateTimeFormat(locale, { weekday: "short" }).format(
      new Date(2026, 7, 2 + day),
    ),
  );

  return (
    <div className="space-y-6">
      <header className="flex flex-col gap-4 border-b pb-6 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <p className="text-sm font-medium text-primary">Your schedule</p>
          <h1 className="mt-2 text-3xl font-semibold">Calendar</h1>
          <p className="mt-2 text-sm text-muted-foreground">
            Classes, content releases, and assessment deadlines across every
            active enrollment.
          </p>
        </div>
        <div className="inline-flex h-9 w-fit items-center rounded-md border bg-muted p-1">
          <Button
            type="button"
            size="sm"
            variant={view === "agenda" ? "secondary" : "ghost"}
            aria-pressed={view === "agenda"}
            onClick={() => setView("agenda")}
          >
            <List className="size-4" />
            Agenda
          </Button>
          <Button
            type="button"
            size="sm"
            variant={view === "month" ? "secondary" : "ghost"}
            aria-pressed={view === "month"}
            onClick={() => setView("month")}
          >
            <CalendarDays className="size-4" />
            Month
          </Button>
        </div>
      </header>

      <div className="grid gap-3 border-b pb-5 sm:grid-cols-2 lg:grid-cols-[minmax(12rem,18rem)_minmax(12rem,18rem)_1fr] lg:items-end">
        <label className="space-y-2 text-sm font-medium">
          <span>Course</span>
          <select
            aria-label="Course"
            className="h-10 w-full rounded-md border bg-background px-3 text-sm"
            value={courseId}
            onChange={(event) => setCourseId(event.target.value)}
          >
            <option value="all">All courses</option>
            {courseOptions.map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>
        <label className="space-y-2 text-sm font-medium">
          <span>Event type</span>
          <select
            aria-label="Event type"
            className="h-10 w-full rounded-md border bg-background px-3 text-sm"
            value={kind}
            onChange={(event) => setKind(event.target.value)}
          >
            <option value="all">All event types</option>
            {kindOptions.map((value) => (
              <option key={value} value={value}>
                {value}
              </option>
            ))}
          </select>
        </label>
        <p className="text-sm text-muted-foreground lg:text-right">
          Timezone:{" "}
          <span className="font-medium text-foreground">{timeZone}</span>
        </p>
      </div>

      {events.length === 0 ? (
        <Card className="rounded-lg bg-card">
          <CardContent className="flex min-h-56 flex-col items-center justify-center text-center">
            <CalendarDays className="size-8 text-muted-foreground" />
            <h2 className="mt-4 font-semibold">No scheduled events</h2>
            <p className="mt-2 text-sm text-muted-foreground">
              Cohort sessions and deadlines will appear here.
            </p>
          </CardContent>
        </Card>
      ) : filteredEvents.length === 0 ? (
        <div className="flex min-h-40 items-center justify-center border-y text-sm text-muted-foreground">
          No events match the selected filters.
        </div>
      ) : view === "agenda" ? (
        <div className="divide-y border-y">
          {filteredEvents.map((event) => (
            <article
              key={event.id}
              className="grid gap-3 px-2 py-5 sm:grid-cols-[2.75rem_minmax(0,1fr)_auto] sm:items-center"
            >
              <div className="flex size-11 shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary">
                <CalendarDays className="size-5" />
              </div>
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <h2 className="font-semibold">{event.title}</h2>
                  <Badge variant="outline">{event.kind}</Badge>
                </div>
                <p className="mt-1 text-sm text-muted-foreground">
                  {event.course} / {event.meta}
                </p>
              </div>
              <time className="inline-flex items-center gap-2 text-sm text-muted-foreground">
                <Clock3 className="size-4" />
                {formatDate(event.date, locale, timeZone)}
              </time>
            </article>
          ))}
        </div>
      ) : (
        <section aria-label="Monthly calendar" className="space-y-4">
          <div className="flex items-center justify-between gap-3">
            <div className="flex items-center gap-1">
              <Button
                type="button"
                size="icon"
                variant="outline"
                aria-label="Previous month"
                onClick={() =>
                  setMonth(
                    new Date(month.getFullYear(), month.getMonth() - 1, 1),
                  )
                }
              >
                <ChevronLeft className="size-4" />
              </Button>
              <Button
                type="button"
                size="icon"
                variant="outline"
                aria-label="Next month"
                onClick={() =>
                  setMonth(
                    new Date(month.getFullYear(), month.getMonth() + 1, 1),
                  )
                }
              >
                <ChevronRight className="size-4" />
              </Button>
            </div>
            <h2 className="text-lg font-semibold">{monthLabel}</h2>
            <Button
              type="button"
              size="sm"
              variant="outline"
              onClick={() => setMonth(startOfMonth(new Date()))}
            >
              Today
            </Button>
          </div>
          <div
            role="grid"
            aria-label={monthLabel}
            className="grid grid-cols-7 overflow-hidden rounded-md border"
          >
            {weekdays.map((weekday) => (
              <div
                key={weekday}
                role="columnheader"
                className="border-b bg-muted px-2 py-2 text-center text-xs font-medium text-muted-foreground"
              >
                {weekday}
              </div>
            ))}
            {monthGridDays(month).map((day) => {
              const dayEvents = filteredEvents.filter((event) =>
                sameDay(new Date(event.date), day),
              );
              const inMonth = day.getMonth() === month.getMonth();
              return (
                <div
                  key={day.toISOString()}
                  role="gridcell"
                  className="min-h-28 border-b border-r p-2 last:border-r-0"
                >
                  <time
                    dateTime={day.toISOString()}
                    className={
                      inMonth
                        ? "text-xs font-medium"
                        : "text-xs text-muted-foreground/50"
                    }
                  >
                    {day.getDate()}
                  </time>
                  <div className="mt-2 space-y-1">
                    {dayEvents.map((event) => (
                      <div
                        key={event.id}
                        className="rounded-sm bg-primary/10 px-2 py-1 text-xs text-primary"
                        title={`${event.course}: ${event.title}`}
                      >
                        <span className="block truncate font-medium">
                          {event.title}
                        </span>
                        <span className="block truncate opacity-80">
                          {event.course}
                        </span>
                      </div>
                    ))}
                  </div>
                </div>
              );
            })}
          </div>
        </section>
      )}
    </div>
  );
}

export function LearnerGradebook({
  records,
}: {
  records: LearnerCourseRecord[];
}) {
  const summaries = records.flatMap(({ course, context }) =>
    context.gradeSummary ? [{ course, summary: context.gradeSummary }] : [],
  );
  const assessments = records.flatMap(({ course, context }) =>
    context.assessments.map((assessment) => ({
      course,
      assessment,
      submission: context.submissions.find(
        (candidate) => candidate.assessmentId === assessment.id,
      ),
    })),
  );
  const gradeGroups = records.flatMap(({ course, context }) =>
    (context.assessmentGroups ?? []).map((group) => {
      const rows = context.assessments
        .filter((assessment) => assessment.assessmentGroupId === group.id)
        .map((assessment) => ({
          assessment,
          submission: context.submissions.find(
            (candidate) => candidate.assessmentId === assessment.id,
          ),
        }));
      const graded = rows.filter((row) => row.submission?.score != null);
      const earned = graded.reduce(
        (total, row) => total + (row.submission?.score ?? 0),
        0,
      );
      const possible = graded.reduce(
        (total, row) => total + (row.assessment.maxScore ?? 0),
        0,
      );
      const percentage = possible > 0 ? (earned / possible) * 100 : null;
      const contribution =
        percentage == null ? null : (percentage * group.weightPercent) / 100;
      const awaitingRelease = rows.some((row) =>
        isFinalizedAwaitingRelease(row.submission),
      );
      return {
        course,
        group,
        rows,
        percentage,
        contribution,
        awaitingRelease,
      };
    }),
  );
  const graded = assessments.filter((row) => row.submission?.score != null);
  const earned = graded.reduce(
    (total, row) => total + (row.submission?.score ?? 0),
    0,
  );
  const possible = graded.reduce(
    (total, row) => total + (row.assessment.maxScore ?? 0),
    0,
  );
  const summarizedEarned = summaries.reduce(
    (total, row) => total + (row.summary.earnedPoints ?? 0),
    0,
  );
  const summarizedPossible = summaries.reduce(
    (total, row) => total + (row.summary.possiblePoints ?? 0),
    0,
  );
  const gradedCount =
    summaries.length > 0
      ? summaries.reduce(
          (total, row) => total + (row.summary.gradedAssessments ?? 0),
          0,
        )
      : graded.length;
  const awaitingCount =
    summaries.length > 0
      ? summaries.reduce(
          (total, row) =>
            total +
            Math.max(
              0,
              (row.summary.totalAssessments ?? 0) -
                (row.summary.gradedAssessments ?? 0),
            ),
          0,
        )
      : assessments.filter(
          (row) => row.submission && row.submission.score == null,
        ).length;
  const gradedGroupWeight = gradeGroups.reduce(
    (total, row) =>
      total + (row.percentage == null ? 0 : row.group.weightPercent),
    0,
  );
  const weightedContribution = gradeGroups.reduce(
    (total, row) => total + (row.contribution ?? 0),
    0,
  );
  const currentScore =
    summaries.length > 0
      ? summarizedPossible > 0
        ? Math.round((summarizedEarned / summarizedPossible) * 100)
        : 0
      : gradeGroups.length > 0 && gradedGroupWeight > 0
        ? Math.round((weightedContribution / gradedGroupWeight) * 100)
        : possible > 0
          ? Math.round((earned / possible) * 100)
          : 0;

  return (
    <div className="space-y-6">
      <header>
        <p className="text-sm font-medium text-primary">Academic record</p>
        <h1 className="mt-2 text-3xl font-semibold">Grades and feedback</h1>
        <p className="mt-2 text-sm text-muted-foreground">
          See how every graded activity and weighted group contributes to your
          current result.
        </p>
      </header>
      <div className="grid gap-4 sm:grid-cols-3">
        <Card className="rounded-lg bg-card">
          <CardHeader>
            <CardTitle className="text-sm text-muted-foreground">
              Graded work
            </CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold">
            {gradedCount}
          </CardContent>
        </Card>
        <Card className="rounded-lg bg-card">
          <CardHeader>
            <CardTitle className="text-sm text-muted-foreground">
              Current score
            </CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold">
            {currentScore}%
          </CardContent>
        </Card>
        <Card className="rounded-lg bg-card">
          <CardHeader>
            <CardTitle className="text-sm text-muted-foreground">
              Awaiting results
            </CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold">
            {awaitingCount}
          </CardContent>
        </Card>
      </div>

      {gradeGroups.length > 0 ? (
        <section aria-labelledby="weighted-groups-title" className="space-y-3">
          <div className="flex flex-wrap items-end justify-between gap-3">
            <div>
              <h2 id="weighted-groups-title" className="text-lg font-semibold">
                Weighted grade groups
              </h2>
              <p className="mt-1 text-sm text-muted-foreground">
                Group scores are multiplied by their configured course weight.
              </p>
            </div>
            <Badge variant="outline">
              {formatNumber(
                gradeGroups.reduce(
                  (total, row) => total + row.group.weightPercent,
                  0,
                ),
              )}
              % configured
            </Badge>
          </div>
          <div className="divide-y rounded-md border">
            {gradeGroups.map(
              ({
                course,
                group,
                rows,
                percentage,
                contribution,
                awaitingRelease,
              }) => (
                <article
                  key={`${course.id}-${group.id}`}
                  className="grid gap-4 p-4 md:grid-cols-[minmax(0,1fr)_auto_auto] md:items-center"
                >
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <h3 className="font-semibold">{group.name}</h3>
                      <Badge variant="secondary">
                        {formatNumber(group.weightPercent)}% of final grade
                      </Badge>
                    </div>
                    <p className="mt-1 text-sm text-muted-foreground">
                      {course.title} / {rows.length} assessment
                      {rows.length === 1 ? "" : "s"}
                    </p>
                  </div>
                  <div className="text-sm">
                    <span className="text-muted-foreground">Group score</span>
                    <strong className="ml-2">
                      {percentage == null
                        ? awaitingRelease
                          ? "Awaiting release"
                          : "Not graded"
                        : `${formatNumber(percentage)}%`}
                    </strong>
                  </div>
                  <div className="text-sm font-medium">
                    {contribution == null
                      ? "No contribution yet"
                      : `${formatNumber(contribution)} points contributed`}
                  </div>
                </article>
              ),
            )}
          </div>
        </section>
      ) : null}

      {summaries.length > 0 && assessments.length > 0 ? (
        <div className="space-y-3">
          {summaries.map(({ course, summary }) => (
            <Card key={course.id} className="rounded-lg bg-card">
              <CardContent className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <h2 className="font-semibold">{course.title}</h2>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {summary.gradedAssessments ?? 0} of{" "}
                    {summary.totalAssessments ?? 0} assessments graded
                  </p>
                </div>
                <strong className="text-2xl">
                  {Math.round(summary.percentage ?? summary.finalGrade ?? 0)}%
                </strong>
              </CardContent>
            </Card>
          ))}
        </div>
      ) : null}

      {assessments.length === 0 && summaries.length === 0 ? (
        <Card className="rounded-lg bg-card">
          <CardContent className="flex min-h-48 items-center justify-center text-sm text-muted-foreground">
            No assessments are assigned yet.
          </CardContent>
        </Card>
      ) : summaries.length > 0 && assessments.length === 0 ? (
        <div className="space-y-3">
          {summaries.map(({ course, summary }) => (
            <Card key={course.id} className="rounded-lg bg-card">
              <CardContent className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <h2 className="font-semibold">{course.title}</h2>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {summary.gradedAssessments ?? 0} of{" "}
                    {summary.totalAssessments ?? 0} assessments graded
                  </p>
                </div>
                <strong className="text-2xl">
                  {Math.round(summary.percentage ?? summary.finalGrade ?? 0)}%
                </strong>
              </CardContent>
            </Card>
          ))}
        </div>
      ) : (
        <div className="divide-y border-y">
          {assessments.map(({ course, assessment, submission }) => (
            <article key={`${course.id}-${assessment.id}`} className="py-5">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="font-semibold">
                      {assessment.title || "Untitled assessment"}
                    </h2>
                    {assessment.assessmentGroupName ? (
                      <Badge variant="outline">
                        {assessment.assessmentGroupName}
                      </Badge>
                    ) : null}
                  </div>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {course.title}
                  </p>
                </div>
                <strong className="text-lg">
                  {submissionResultLabel(submission, assessment)}
                </strong>
              </div>
              {submission?.feedback ? (
                <div className="mt-4 border-l-2 border-primary pl-4">
                  <p className="text-xs font-medium uppercase text-primary">
                    Instructor feedback
                  </p>
                  <p className="mt-2 text-sm leading-6">
                    {submission.feedback}
                  </p>
                </div>
              ) : null}
            </article>
          ))}
        </div>
      )}
    </div>
  );
}

function slugify(value: string) {
  return value
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
}

function escapeHtml(value: string) {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function certificateDownload(certificate: LearnerCertificate) {
  const course = certificate.courseName || "Game Guild course";
  const number =
    certificate.certificateNumber || certificate.id || "credential";
  const issued = certificate.issuedAt
    ? new Intl.DateTimeFormat("en-US", { dateStyle: "long" }).format(
        new Date(certificate.issuedAt),
      )
    : "";
  const html = `<!doctype html><html lang="en"><head><meta charset="utf-8"><title>${escapeHtml(course)} certificate</title><style>body{font-family:Arial,sans-serif;margin:0;padding:64px;color:#111827}main{max-width:960px;margin:auto;border:2px solid #111827;padding:64px;text-align:center}p{color:#4b5563}small{font-family:monospace}</style></head><body><main><p>Game Guild Certificate of Completion</p><h1>${escapeHtml(course)}</h1><p>Issued ${escapeHtml(issued)}</p><small>${escapeHtml(number)}</small></main></body></html>`;
  return `data:text/html;charset=utf-8,${encodeURIComponent(html)}`;
}

function ShareCertificateButton({
  certificate,
}: {
  certificate: LearnerCertificate;
}) {
  const [message, setMessage] = useState("");
  const url = certificate.verificationUrl || "";

  async function share() {
    if (!url) return;
    try {
      if (navigator.share) {
        await navigator.share({
          title: `${certificate.courseName || "Game Guild"} certificate`,
          text: `Verify certificate ${certificate.certificateNumber || certificate.id || ""}`,
          url,
        });
        setMessage("Certificate shared.");
      } else {
        await navigator.clipboard.writeText(url);
        setMessage("Verification link copied.");
      }
    } catch {
      setMessage("Sharing was canceled.");
    }
  }

  return (
    <>
      <Button
        type="button"
        size="sm"
        variant="outline"
        disabled={!url}
        onClick={share}
      >
        <Share2 className="size-4" />
        Share
      </Button>
      <span className="sr-only" aria-live="polite">
        {message}
      </span>
    </>
  );
}

export function LearnerCertificates({
  certificates,
}: {
  certificates: LearnerCertificate[];
}) {
  return (
    <div className="space-y-6">
      <header>
        <p className="text-sm font-medium text-primary">
          Verified achievements
        </p>
        <h1 className="mt-2 text-3xl font-semibold">Certificates</h1>
        <p className="mt-2 text-sm text-muted-foreground">
          View, download, share, and verify credentials issued after confirmed
          course completion.
        </p>
      </header>
      {certificates.length === 0 ? (
        <Card className="rounded-lg bg-card">
          <CardContent className="flex min-h-64 flex-col items-center justify-center text-center">
            <Award className="size-9 text-muted-foreground" />
            <h2 className="mt-4 text-lg font-semibold">
              No certificates issued yet
            </h2>
            <p className="mt-2 max-w-md text-sm text-muted-foreground">
              Complete an eligible course and satisfy its assessment
              requirements to receive a credential.
            </p>
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-4 lg:grid-cols-2">
          {certificates.map((certificate) => {
            const courseName = certificate.courseName || "Game Guild course";
            const downloadName = `${slugify(courseName) || "game-guild"}-certificate.html`;
            return (
              <Card key={certificate.id} className="rounded-lg bg-card">
                <CardContent className="p-6">
                  <div className="flex items-start gap-4">
                    <div className="flex size-12 shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary">
                      <GraduationCap className="size-6" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <h2 className="font-semibold">{courseName}</h2>
                        <Badge>
                          <CheckCircle2 className="mr-1 size-3" />
                          {certificate.status || "Active"}
                        </Badge>
                      </div>
                      <p className="mt-3 font-mono text-sm">
                        {certificate.certificateNumber || certificate.id}
                      </p>
                      {certificate.issuedAt ? (
                        <p className="mt-2 text-xs text-muted-foreground">
                          Issued {formatDate(certificate.issuedAt)}
                        </p>
                      ) : null}
                      <div className="mt-5 flex flex-wrap gap-2">
                        {certificate.verificationUrl ? (
                          <Button nativeButton={false}
                            size="sm"
                            variant="outline"
                            render={
                              <a
                                href={certificate.verificationUrl}
                                target="_blank"
                                rel="noreferrer"
                              />
                            }
                          >
                            <ExternalLink className="size-4" />
                            Verify
                          </Button>
                        ) : null}
                        <Button nativeButton={false}
                          size="sm"
                          variant="outline"
                          render={
                            <a
                              href={certificateDownload(certificate)}
                              download={downloadName}
                            />
                          }
                        >
                          <Download className="size-4" />
                          Download
                        </Button>
                        <ShareCertificateButton certificate={certificate} />
                      </div>
                    </div>
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}
