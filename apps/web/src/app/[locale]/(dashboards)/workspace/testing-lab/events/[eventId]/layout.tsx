import {
  EditTestingEventDialog,
  TestingEventLifecycleActions,
} from "@/components/testing-lab/testing-event-management";
import { TestingEventWorkspaceNav } from "@/components/testing-lab/testing-event-workspace-nav";
import { TestingLabAccessIssues } from "@/components/testing-lab/testing-lab-state";
import { Link } from "@/i18n/navigation";
import {
  formatEventDateTime,
  isTestingEventReadOnly,
} from "@/lib/testing-lab/event-workspace";
import { getTestingEventWorkspaceData } from "@/lib/testing-lab/events-queries";
import { formatTestingEventStatus } from "@/lib/testing-lab/format";
import { Badge } from "@game-guild/ui/components/badge";
import { AlertTriangle, ArrowLeft, CalendarDays, Clock3 } from "lucide-react";
import { notFound } from "next/navigation";
import type { ReactNode } from "react";

export default async function TestingEventWorkspaceLayout({
  params,
  children,
}: {
  params: Promise<{ eventId: string }>;
  children: ReactNode;
}) {
  const { eventId } = await params;
  const detail = await getTestingEventWorkspaceData(eventId);

  if (!detail.event && detail.accessIssues.length === 0) notFound();
  if (!detail.event) {
    return (
      <div className="p-4 lg:p-6">
        <TestingLabAccessIssues issues={detail.accessIssues} />
      </div>
    );
  }

  const event = detail.event;
  const readOnly = isTestingEventReadOnly(event);
  const canManageWorkspace =
    detail.applicationAccess?.canManageApplications === true;
  const sourceTemplateId = event.configuration?.sourceTemplateId;
  const calendarName = sourceTemplateId ? "Template calendar" : "Testing events";

  return (
    <div className="space-y-5 p-4 lg:p-6">
      <header className="space-y-3 border-b pb-4">
        <nav aria-label="Back to Testing Lab" className="flex items-center gap-3 text-sm">
          <Link
            href="/workspace/testing-lab"
            className="inline-flex items-center gap-1.5 text-muted-foreground hover:text-foreground"
          >
            <ArrowLeft className="size-4" aria-hidden="true" />
            Calendar
          </Link>
          <Link
            href="/workspace/testing-lab/events"
            className="text-muted-foreground hover:text-foreground"
          >
            Sessions
          </Link>
        </nav>
        <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="truncate text-2xl font-semibold">
                {event.name ?? "Testing event"}
              </h1>
              <Badge>{formatTestingEventStatus(event.status)}</Badge>
            </div>
            <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted-foreground">
              <span className="inline-flex items-center gap-1.5">
                <CalendarDays className="size-4" aria-hidden="true" />
                {calendarName}
              </span>
              <span className="inline-flex items-center gap-1.5">
                <Clock3 className="size-4" aria-hidden="true" />
                {formatEventDateTime(event.startsAt)} to {formatEventDateTime(event.endsAt)}
              </span>
            </div>
          </div>
          {!readOnly && canManageWorkspace ? (
            <EditTestingEventDialog event={event} />
          ) : null}
        </div>
      </header>

      <TestingLabAccessIssues issues={detail.accessIssues} />

      <section className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant="outline">
            {formatTestingEventStatus(event.mode)}
          </Badge>
          <Badge variant="outline">
            {event.approvalMode === "Committee"
              ? "Committee review"
              : "Manager decision"}
          </Badge>
          {event.requiresFeedback ? (
            <Badge variant="secondary">Feedback required</Badge>
          ) : null}
          {readOnly ? (
            <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
              <AlertTriangle className="size-3.5" />
              This event is read-only. Its audit history remains available.
            </span>
          ) : null}
        </div>
        {canManageWorkspace ? (
          <TestingEventLifecycleActions event={event} />
        ) : null}
      </section>

      <TestingEventWorkspaceNav
        eventId={eventId}
        canManageWorkspace={canManageWorkspace}
      />

      {children}
    </div>
  );
}
