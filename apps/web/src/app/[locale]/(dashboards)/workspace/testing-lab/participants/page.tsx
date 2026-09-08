import { TestingLabPageHeader } from "@/components/testing-lab/testing-lab-page-header";
import { TestingParticipantFilters } from "@/components/testing-lab/testing-participant-filters";
import {
  TestingLabAccessIssues,
  TestingLabEmptyState,
} from "@/components/testing-lab/testing-lab-state";
import { Link } from "@/i18n/navigation";
import { getTestingParticipantDirectory } from "@/lib/testing-lab/events-queries";
import type {
  TestingLabTestingParticipantDirectoryItemProjection,
  TestingLabTestingSlotRegistrationStatus,
} from "@game-guild/client";
import {
  Avatar,
  AvatarFallback,
  AvatarImage,
} from "@game-guild/ui/components/avatar";
import { Badge } from "@game-guild/ui/components/badge";
import { Button } from "@game-guild/ui/components/button";
import {
  CalendarClock,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  CircleDot,
  MessageSquareText,
  Users,
} from "lucide-react";

const PAGE_SIZE = 25;
const registrationStatuses = new Set<TestingLabTestingSlotRegistrationStatus>([
  "Registered",
  "Waitlisted",
  "CheckedIn",
  "Attended",
  "Completed",
  "Cancelled",
  "NoShow",
]);

function parseStatus(value?: string) {
  return value &&
    registrationStatuses.has(value as TestingLabTestingSlotRegistrationStatus)
    ? (value as TestingLabTestingSlotRegistrationStatus)
    : undefined;
}

function formatStatus(status?: TestingLabTestingSlotRegistrationStatus) {
  if (!status) return "Unknown";
  if (status === "CheckedIn") return "Checked in";
  if (status === "NoShow") return "No-show";
  return status;
}

function statusClassName(status?: TestingLabTestingSlotRegistrationStatus) {
  if (
    status === "CheckedIn" ||
    status === "Attended" ||
    status === "Completed"
  ) {
    return "border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-300";
  }
  if (status === "Waitlisted")
    return "border-amber-500/30 bg-amber-500/10 text-amber-700 dark:text-amber-300";
  if (status === "NoShow" || status === "Cancelled")
    return "border-destructive/30 bg-destructive/10 text-destructive";
  return "border-border bg-muted/35 text-foreground";
}

function formatSchedule(value?: string) {
  if (!value) return "Schedule pending";
  return new Intl.DateTimeFormat("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
    timeZone: "UTC",
    timeZoneName: "short",
  }).format(new Date(value));
}

function initials(item: TestingLabTestingParticipantDirectoryItemProjection) {
  const source = item.userName?.trim() || item.userEmail?.trim() || "Member";
  return source
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part.charAt(0).toUpperCase())
    .join("");
}

function participantName(
  item: TestingLabTestingParticipantDirectoryItemProjection,
) {
  return (
    item.userName?.trim() ||
    item.userEmail?.trim() ||
    "Member profile unavailable"
  );
}

function sessionLocation(
  item: TestingLabTestingParticipantDirectoryItemProjection,
) {
  if (item.mode === "Online") return "Online session";
  return (
    [item.campusName, item.roomName].filter(Boolean).join(" · ") ||
    item.mode ||
    "Location pending"
  );
}

function ParticipantIdentity({
  item,
}: {
  item: TestingLabTestingParticipantDirectoryItemProjection;
}) {
  return (
    <div className="flex min-w-0 items-center gap-3">
      <Avatar className="size-9 shrink-0">
        {item.avatarUrl ? <AvatarImage src={item.avatarUrl} alt="" /> : null}
        <AvatarFallback>{initials(item)}</AvatarFallback>
      </Avatar>
      <div className="min-w-0">
        <p className="truncate font-medium">{participantName(item)}</p>
        {item.userEmail && item.userName ? (
          <p className="truncate text-xs text-muted-foreground">
            {item.userEmail}
          </p>
        ) : null}
      </div>
    </div>
  );
}

function ParticipantStatus({
  item,
}: {
  item: TestingLabTestingParticipantDirectoryItemProjection;
}) {
  return (
    <Badge className={statusClassName(item.status)}>
      {formatStatus(item.status)}
    </Badge>
  );
}

function buildPageHref(
  page: number,
  search?: string,
  status?: TestingLabTestingSlotRegistrationStatus,
) {
  const params = new URLSearchParams();
  if (search) params.set("q", search);
  if (status) params.set("status", status);
  params.set("page", String(page));
  return `/workspace/testing-lab/participants?${params.toString()}`;
}

export default async function TestingLabParticipantsPage({
  searchParams,
}: {
  searchParams: Promise<{ page?: string; q?: string; status?: string }>;
}) {
  const params = await searchParams;
  const search = params.q?.trim() || undefined;
  const status = parseStatus(params.status);
  const parsedPage = Number.parseInt(params.page ?? "1", 10);
  const page = Number.isFinite(parsedPage) && parsedPage > 0 ? parsedPage : 1;
  const result = await getTestingParticipantDirectory({
    search,
    status,
    skip: (page - 1) * PAGE_SIZE,
    take: PAGE_SIZE,
  });
  const directory = result.directory;
  const items = directory?.items ?? [];
  const total = directory?.totalCount ?? 0;
  const hasNextPage = page * PAGE_SIZE < total;
  const resolvedOutcomes =
    (directory?.attendedCount ?? 0) + (directory?.completedCount ?? 0);

  return (
    <div className="min-w-0 space-y-5 p-4 lg:p-6">
      <TestingLabPageHeader
        icon={Users}
        title="Testing Lab participants"
        description="Track registrations, waitlists, attendance, and feedback obligations across testing events."
      />

      <TestingLabAccessIssues issues={result.accessIssues} />

      <section
        aria-label="Participant status summary"
        className="grid grid-cols-2 divide-x divide-y overflow-hidden rounded-md border bg-card lg:grid-cols-4 lg:divide-y-0"
      >
        <div className="min-w-0 p-4">
          <div className="flex items-center gap-2 text-xs font-medium text-muted-foreground">
            <Users className="size-4" />
            All registrations
          </div>
          <p className="mt-2 text-2xl font-semibold tabular-nums">{total}</p>
          <p className="text-xs text-muted-foreground">
            {directory?.registeredCount ?? 0} awaiting arrival
          </p>
        </div>
        <div className="min-w-0 p-4">
          <div className="flex items-center gap-2 text-xs font-medium text-muted-foreground">
            <CircleDot className="size-4" />
            Waitlist
          </div>
          <p className="mt-2 text-2xl font-semibold tabular-nums">
            {directory?.waitlistedCount ?? 0}
          </p>
          <p className="text-xs text-muted-foreground">
            Waiting for an approved seat
          </p>
        </div>
        <div className="min-w-0 p-4">
          <div className="flex items-center gap-2 text-xs font-medium text-muted-foreground">
            <CalendarClock className="size-4" />
            Checked in
          </div>
          <p className="mt-2 text-2xl font-semibold tabular-nums">
            {directory?.checkedInCount ?? 0}
          </p>
          <p className="text-xs text-muted-foreground">
            Currently active at a session
          </p>
        </div>
        <div className="min-w-0 p-4">
          <div className="flex items-center gap-2 text-xs font-medium text-muted-foreground">
            <CheckCircle2 className="size-4" />
            Outcomes
          </div>
          <p className="mt-2 text-2xl font-semibold tabular-nums">
            {resolvedOutcomes}
          </p>
          <p className="text-xs text-muted-foreground">
            {directory?.noShowCount ?? 0} no-show
          </p>
        </div>
      </section>

      <section
        aria-labelledby="participant-directory-heading"
        className="overflow-hidden rounded-md border bg-card"
      >
        <div className="flex items-center justify-between gap-4 px-4 py-3">
          <div>
            <h2 id="participant-directory-heading" className="font-semibold">
              Participant directory
            </h2>
            <p className="text-xs text-muted-foreground">
              One tenant-scoped view of every event registration.
            </p>
          </div>
          <span className="shrink-0 text-sm tabular-nums text-muted-foreground">
            {total} total
          </span>
        </div>

        <TestingParticipantFilters search={search} status={status} />

        {items.length === 0 ? (
          <div className="p-4">
            <TestingLabEmptyState
              title="No participants match this view"
              description="Clear the filters or wait for members to reserve an approved testing seat."
            />
          </div>
        ) : (
          <>
            <div className="hidden overflow-x-auto md:block">
              <table className="w-full min-w-[900px] text-sm">
                <thead className="border-b bg-muted/25 text-left text-xs text-muted-foreground">
                  <tr>
                    <th scope="col" className="px-4 py-3 font-medium">
                      Member
                    </th>
                    <th scope="col" className="px-4 py-3 font-medium">
                      Event and location
                    </th>
                    <th scope="col" className="px-4 py-3 font-medium">
                      Schedule
                    </th>
                    <th scope="col" className="px-4 py-3 font-medium">
                      Status
                    </th>
                    <th
                      scope="col"
                      className="px-4 py-3 text-right font-medium"
                    >
                      Feedback
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {items.map((item) => (
                    <tr
                      key={item.registrationId}
                      className="transition-colors hover:bg-muted/20"
                    >
                      <td className="px-4 py-3">
                        <ParticipantIdentity item={item} />
                      </td>
                      <td className="px-4 py-3">
                        {item.eventId ? (
                          <Link
                            href={`/workspace/testing-lab/events/${item.eventId}/testers`}
                            className="font-medium hover:underline"
                          >
                            {item.eventName || "Testing event"}
                          </Link>
                        ) : (
                          <span className="font-medium">
                            {item.eventName || "Testing event"}
                          </span>
                        )}
                        <p className="mt-0.5 text-xs text-muted-foreground">
                          {sessionLocation(item)}
                        </p>
                      </td>
                      <td className="px-4 py-3 text-muted-foreground">
                        {formatSchedule(item.startsAt)}
                      </td>
                      <td className="px-4 py-3">
                        <ParticipantStatus item={item} />
                      </td>
                      <td className="px-4 py-3 text-right">
                        <span className="inline-flex items-center gap-1.5 text-muted-foreground">
                          <MessageSquareText className="size-4" />
                          {item.pendingFeedbackCount ?? 0} pending
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="divide-y md:hidden">
              {items.map((item) => (
                <article key={item.registrationId} className="space-y-3 p-4">
                  <div className="flex items-start justify-between gap-3">
                    <ParticipantIdentity item={item} />
                    <ParticipantStatus item={item} />
                  </div>
                  <div>
                    {item.eventId ? (
                      <Link
                        href={`/workspace/testing-lab/events/${item.eventId}/testers`}
                        className="font-medium hover:underline"
                      >
                        {item.eventName || "Testing event"}
                      </Link>
                    ) : (
                      <p className="font-medium">
                        {item.eventName || "Testing event"}
                      </p>
                    )}
                    <p className="mt-1 text-sm text-muted-foreground">
                      {sessionLocation(item)}
                    </p>
                    <p className="text-sm text-muted-foreground">
                      {formatSchedule(item.startsAt)}
                    </p>
                  </div>
                  <p className="flex items-center gap-1.5 text-xs text-muted-foreground">
                    <MessageSquareText className="size-4" />
                    {item.pendingFeedbackCount ?? 0} feedback obligation(s)
                    pending
                  </p>
                </article>
              ))}
            </div>
          </>
        )}

        {total > 0 ? (
          <div className="flex items-center justify-between gap-4 border-t px-4 py-3">
            <p className="text-xs text-muted-foreground">
              Page {page} · showing {items.length} of {total}
            </p>
            <div className="flex items-center gap-2">
              {page > 1 ? (
                <Button asChild variant="outline" size="sm">
                  <Link href={buildPageHref(page - 1, search, status)}>
                    <ChevronLeft className="size-4" />
                    Previous
                  </Link>
                </Button>
              ) : (
                <Button variant="outline" size="sm" disabled>
                  <ChevronLeft className="size-4" />
                  Previous
                </Button>
              )}
              {hasNextPage ? (
                <Button asChild variant="outline" size="sm">
                  <Link href={buildPageHref(page + 1, search, status)}>
                    Next
                    <ChevronRight className="size-4" />
                  </Link>
                </Button>
              ) : (
                <Button variant="outline" size="sm" disabled>
                  Next
                  <ChevronRight className="size-4" />
                </Button>
              )}
            </div>
          </div>
        ) : null}
      </section>
    </div>
  );
}
