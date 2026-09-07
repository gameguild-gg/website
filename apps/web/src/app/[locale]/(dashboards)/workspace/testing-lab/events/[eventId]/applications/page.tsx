import { TestingEventApplications } from "@/components/testing-lab/testing-event-management";
import { getMembers } from "@/lib/community/queries/members";
import { TestingLabPageHeader } from "@/components/testing-lab/testing-lab-page-header";
import { TestingLabAccessIssues } from "@/components/testing-lab/testing-lab-state";
import { Link } from "@/i18n/navigation";
import { isTestingEventReadOnly } from "@/lib/testing-lab/event-workspace";
import { getTestingEventWorkspaceData } from "@/lib/testing-lab/events-queries";
import { getTestingProjectOptions } from "@/lib/testing-lab/queries";
import { cn } from "@game-guild/ui/lib/utils";
import type { TestingLabTestingApplicationStatus } from "@game-guild/client";
import { ClipboardList } from "lucide-react";
import { notFound } from "next/navigation";

const statuses: Array<{
  label: string;
  value?: TestingLabTestingApplicationStatus;
}> = [
  { label: "All" },
  { label: "Pending", value: "Pending" },
  { label: "In review", value: "UnderReview" },
  { label: "Approved", value: "Approved" },
  { label: "Waitlisted", value: "Waitlisted" },
  { label: "Rejected", value: "Rejected" },
];

export default async function TestingEventApplicationsPage({
  params,
  searchParams,
}: {
  params: Promise<{ eventId: string }>;
  searchParams: Promise<{ applicationStatus?: string }>;
}) {
  const [{ eventId }, query] = await Promise.all([params, searchParams]);
  const [detail, memberDirectory, projects] = await Promise.all([
    getTestingEventWorkspaceData(eventId),
    getMembers({ page: 1, limit: 100 }),
    getTestingProjectOptions(),
  ]);
  if (!detail.event) {
    if (detail.accessIssues.length > 0) {
      return (
        <div className="space-y-5">
          <TestingLabPageHeader
            headingLevel={2}
            icon={ClipboardList}
            title="Project applications"
            description="Review project candidates, record committee decisions, and reserve capacity only when a project is approved."
          />
          <TestingLabAccessIssues issues={detail.accessIssues} />
        </div>
      );
    }
    notFound();
  }
  const projectLabels = Object.fromEntries(
    projects.map((project) => [project.id, project.title]),
  );
  const memberLabels = Object.fromEntries(
    memberDirectory.members.map((member) => [
      member.id,
      member.displayName && member.email
        ? `${member.displayName} / ${member.email}`
        : member.displayName || member.email || "Member details unavailable",
    ]),
  );

  const selected = statuses.find(
    (status) => status.value === query.applicationStatus,
  )?.value;
  const applications = selected
    ? detail.applications.filter(
        (application) => application.status === selected,
      )
    : detail.applications;

  return (
    <div className="space-y-5">
      <TestingLabPageHeader
        headingLevel={2}
        icon={ClipboardList}
        title="Project applications"
        description="Review project candidates, record committee decisions, and reserve capacity only when a project is approved."
      />

      <TestingLabAccessIssues issues={detail.accessIssues} />

      <nav aria-label="Application status" className="flex flex-wrap gap-2">
        {statuses.map((status) => {
          const active =
            status.value === selected || (!status.value && !selected);
          const href = status.value
            ? `/workspace/testing-lab/events/${eventId}/applications?applicationStatus=${status.value}`
            : `/workspace/testing-lab/events/${eventId}/applications`;
          return (
            <Link
              key={status.label}
              href={href}
              aria-current={active ? "page" : undefined}
              className={cn(
                "rounded-md border px-3 py-1.5 text-sm text-muted-foreground hover:text-foreground",
                active && "border-foreground/20 bg-muted text-foreground",
              )}
            >
              {status.label}
            </Link>
          );
        })}
      </nav>

      <TestingEventApplications
        eventId={eventId}
        access={detail.applicationAccess}
        applications={applications}
        slots={detail.slots}
        readOnly={isTestingEventReadOnly(detail.event)}
        projectLabels={projectLabels}
        memberLabels={memberLabels}
      />
    </div>
  );
}
