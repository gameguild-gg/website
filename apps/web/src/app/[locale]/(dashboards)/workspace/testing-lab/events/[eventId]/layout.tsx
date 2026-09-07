import {
  EditTestingEventDialog,
  TestingEventLifecycleActions,
} from "@/components/testing-lab/testing-event-management";
import { TestingEventWorkspaceNav } from "@/components/testing-lab/testing-event-workspace-nav";
import { TestingLabPageHeader } from "@/components/testing-lab/testing-lab-page-header";
import { TestingLabAccessIssues } from "@/components/testing-lab/testing-lab-state";
import { isTestingEventReadOnly } from "@/lib/testing-lab/event-workspace";
import { getTestingEventWorkspaceData } from "@/lib/testing-lab/events-queries";
import { formatTestingEventStatus } from "@/lib/testing-lab/format";
import { Badge } from "@game-guild/ui/components/badge";
import { AlertTriangle, FlaskConical } from "lucide-react";
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

  return (
    <div className="space-y-5 p-4 lg:p-6">
      <TestingLabPageHeader
        icon={FlaskConical}
        title={event.name ?? "Testing event"}
        description={
          event.description ??
          "Project applications, tester schedules, attendance, and required feedback."
        }
        actions={
          !readOnly && canManageWorkspace ? (
            <EditTestingEventDialog event={event} />
          ) : undefined
        }
      />

      <TestingLabAccessIssues issues={detail.accessIssues} />

      <section className="flex flex-col gap-3 rounded-md border bg-muted/10 p-3 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex flex-wrap items-center gap-2">
          <Badge>{formatTestingEventStatus(event.status)}</Badge>
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
