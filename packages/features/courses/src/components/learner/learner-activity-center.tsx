"use client";

import { Badge } from "@game-guild/ui/components/badge";
import { Button } from "@game-guild/ui/components/button";
import { Input } from "@game-guild/ui/components/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@game-guild/ui/components/select";
import { ArrowRight, CalendarClock, ClipboardList, Search } from "lucide-react";
import Link from "next/link";
import { useMemo, useState } from "react";

import type { LearnerCourseRecord } from "./types";

type ActivityState =
  "pending" | "overdue" | "submitted" | "graded" | "completed" | "locked";

interface ActivityRow {
  id: string;
  courseId: string;
  courseTitle: string;
  title: string;
  description: string;
  type: string;
  state: ActivityState;
  dueAt: string | null;
  points: number | null;
  score: number | null;
  href: string;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("en-US", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}

function assessmentState(
  submission: LearnerCourseRecord["context"]["submissions"][number] | undefined,
  dueAt: string | null | undefined,
  available: boolean | null | undefined,
): ActivityState {
  if (submission?.score != null || submission?.status === "Graded")
    return "graded";
  if (submission) return "submitted";
  if (available === false) return "locked";
  if (dueAt && new Date(dueAt).getTime() < Date.now()) return "overdue";
  return "pending";
}

function buildRows(
  records: LearnerCourseRecord[],
  courseBasePath: string,
): ActivityRow[] {
  return records.flatMap(({ course, context }) => {
    const assessments = context.assessments.flatMap((assessment) => {
      if (!assessment.id) return [];
      const submission = context.submissions.find(
        (candidate) => candidate.assessmentId === assessment.id,
      );
      return [
        {
          id: "assessment-" + assessment.id,
          courseId: course.id,
          courseTitle: course.title,
          title: assessment.title ?? "Untitled assessment",
          description:
            assessment.description ||
            "Review the instructions and submit your work.",
          type:
            assessment.type === "Exam"
              ? "Quiz"
              : assessment.type || "Assessment",
          state: assessmentState(
            submission,
            assessment.dueAt,
            assessment.isAvailable,
          ),
          dueAt: assessment.dueAt ?? null,
          points: assessment.maxScore ?? null,
          score: submission?.score ?? null,
          href: `${courseBasePath}/${course.slug}/activities/assessment-${assessment.id}`,
        } satisfies ActivityRow,
      ];
    });

    const participation = course.modules.flatMap((module) =>
      module.items
        .filter((item) =>
          ["Discussion", "Reflection", "Survey"].includes(
            item.contentType ?? "",
          ),
        )
        .map(
          (item) =>
            ({
              id: "content-" + item.id,
              courseId: course.id,
              courseTitle: course.title,
              title: item.title,
              description:
                item.description ||
                "Complete this course participation activity.",
              type: item.contentType || "Activity",
              state:
                item.status === "completed"
                  ? "completed"
                  : item.status === "locked"
                    ? "locked"
                    : "pending",
              dueAt: null,
              points: item.maxPoints ?? null,
              score: null,
              href: `${courseBasePath}/${course.slug}/activities/content-${item.id}`,
            }) satisfies ActivityRow,
        ),
    );

    return [...assessments, ...participation];
  });
}

function matchesDate(row: ActivityRow, filter: string) {
  if (filter === "all") return true;
  if (!row.dueAt) return filter === "none";
  const due = new Date(row.dueAt).getTime();
  const now = Date.now();
  if (filter === "overdue") return due < now;
  if (filter === "week") {
    return due >= now && due <= now + 7 * 24 * 60 * 60 * 1000;
  }
  return filter === "later" && due > now + 7 * 24 * 60 * 60 * 1000;
}

function statusLabel(state: ActivityState) {
  return state.charAt(0).toUpperCase() + state.slice(1);
}

function stripTrailingSlashes(value: string) {
  let result = value;
  while (result.endsWith("/")) result = result.slice(0, -1);
  return result;
}

export function LearnerActivityCenter({
  records,
  courseBasePath = "/courses",
}: {
  records: LearnerCourseRecord[];
  courseBasePath?: string;
}) {
  const normalizedCourseBasePath = stripTrailingSlashes(courseBasePath);
  const rows = useMemo(
    () => buildRows(records, normalizedCourseBasePath),
    [records, normalizedCourseBasePath],
  );
  const [query, setQuery] = useState("");
  const [courseId, setCourseId] = useState("all");
  const [type, setType] = useState("all");
  const [status, setStatus] = useState("all");
  const [date, setDate] = useState("all");
  const courses = Array.from(
    new Map(records.map(({ course }) => [course.id, course.title])),
  );
  const types = Array.from(new Set(rows.map((row) => row.type))).sort();
  const normalizedQuery = query.trim().toLowerCase();
  const filtered = rows
    .filter((row) => courseId === "all" || row.courseId === courseId)
    .filter((row) => type === "all" || row.type === type)
    .filter((row) => status === "all" || row.state === status)
    .filter((row) => matchesDate(row, date))
    .filter(
      (row) =>
        !normalizedQuery ||
        (row.title + " " + row.courseTitle)
          .toLowerCase()
          .includes(normalizedQuery),
    )
    .sort((left, right) => {
      if (!left.dueAt) {
        return right.dueAt ? 1 : left.title.localeCompare(right.title);
      }
      if (!right.dueAt) return -1;
      return new Date(left.dueAt).getTime() - new Date(right.dueAt).getTime();
    });

  const hasFilters =
    Boolean(query) ||
    courseId !== "all" ||
    type !== "all" ||
    status !== "all" ||
    date !== "all";

  function clearFilters() {
    setQuery("");
    setCourseId("all");
    setType("all");
    setStatus("all");
    setDate("all");
  }

  return (
    <div className="space-y-6">
      <header className="border-b pb-6">
        <p className="text-sm font-medium text-primary">Your work</p>
        <h1 className="mt-2 text-3xl font-semibold">
          Assignments and activities
        </h1>
        <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
          Review pending work, submissions, grades, and participation across
          every active course.
        </p>
      </header>

      <section
        aria-label="Activity filters"
        className="grid gap-3 border-b pb-5 md:grid-cols-2 xl:grid-cols-[minmax(16rem,1fr)_repeat(4,minmax(10rem,13rem))]"
      >
        <label className="relative md:col-span-2 xl:col-span-1">
          <span className="sr-only">Search activities</span>
          <Search className="pointer-events-none absolute left-3 top-3 size-4 text-muted-foreground" />
          <Input
            aria-label="Search activities"
            className="pl-9"
            placeholder="Search activities"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
          />
        </label>
        <Select value={courseId} onValueChange={(value) => setCourseId(value ?? "all")}>
          <SelectTrigger aria-label="Course">
            <SelectValue placeholder="All courses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All courses</SelectItem>
            {courses.map(([value, label]) => (
              <SelectItem key={value} value={value}>
                {label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={type} onValueChange={(value) => setType(value ?? "all")}>
          <SelectTrigger aria-label="Activity type">
            <SelectValue placeholder="All types" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All types</SelectItem>
            {types.map((value) => (
              <SelectItem key={value} value={value}>
                {value}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={status} onValueChange={(value) => setStatus(value ?? "all")}>
          <SelectTrigger aria-label="Activity status">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            <SelectItem value="pending">Pending</SelectItem>
            <SelectItem value="overdue">Overdue</SelectItem>
            <SelectItem value="submitted">Submitted</SelectItem>
            <SelectItem value="graded">Graded</SelectItem>
            <SelectItem value="completed">Completed</SelectItem>
            <SelectItem value="locked">Locked</SelectItem>
          </SelectContent>
        </Select>
        <Select value={date} onValueChange={(value) => setDate(value ?? "all")}>
          <SelectTrigger aria-label="Due date">
            <SelectValue placeholder="Any due date" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Any due date</SelectItem>
            <SelectItem value="overdue">Overdue</SelectItem>
            <SelectItem value="week">Next 7 days</SelectItem>
            <SelectItem value="later">Later</SelectItem>
            <SelectItem value="none">No deadline</SelectItem>
          </SelectContent>
        </Select>
      </section>

      <div className="flex items-center justify-between gap-4 text-sm">
        <p className="text-muted-foreground">
          {filtered.length} of {rows.length} activities
        </p>
        {hasFilters ? (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={clearFilters}
          >
            Clear filters
          </Button>
        ) : null}
      </div>

      {filtered.length === 0 ? (
        <div className="flex min-h-56 flex-col items-center justify-center border-y text-center">
          <ClipboardList className="size-8 text-muted-foreground" />
          <h2 className="mt-4 text-lg font-semibold">No matching activities</h2>
          <p className="mt-2 text-sm text-muted-foreground">
            Adjust the filters or browse your active courses.
          </p>
        </div>
      ) : (
        <div className="divide-y border-y">
          {filtered.map((row) => (
            <article
              key={row.courseId + "-" + row.id}
              className="grid gap-4 py-5 md:grid-cols-[minmax(0,1fr)_auto_auto] md:items-center"
            >
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <h2 className="font-semibold">{row.title}</h2>
                  <Badge variant="outline">{row.type}</Badge>
                  <Badge
                    variant={
                      row.state === "overdue"
                        ? "destructive"
                        : row.state === "graded" || row.state === "completed"
                          ? "default"
                          : "secondary"
                    }
                  >
                    {statusLabel(row.state)}
                  </Badge>
                </div>
                <p className="mt-1 text-sm text-muted-foreground">
                  {row.courseTitle}
                </p>
                <p className="mt-2 line-clamp-2 text-sm text-muted-foreground">
                  {row.description}
                </p>
              </div>
              <div className="text-sm text-muted-foreground md:text-right">
                {row.dueAt ? (
                  <span className="inline-flex items-center gap-1.5">
                    <CalendarClock className="size-4" />
                    {formatDate(row.dueAt)}
                  </span>
                ) : (
                  <span>No deadline</span>
                )}
                {row.points != null ? (
                  <p className="mt-1">
                    {row.score != null ? row.score + " / " : ""}
                    {row.points} points
                  </p>
                ) : null}
              </div>
              <Button nativeButton={false} variant="outline" render={<Link href={row.href} />}>
                Open
                <ArrowRight className="size-4" />
              </Button>
            </article>
          ))}
        </div>
      )}
    </div>
  );
}
