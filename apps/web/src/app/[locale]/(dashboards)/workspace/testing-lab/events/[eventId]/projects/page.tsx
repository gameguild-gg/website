import { TestingLabPageHeader } from "@/components/testing-lab/testing-lab-page-header";
import { TestingLabEmptyState } from "@/components/testing-lab/testing-lab-state";
import { Link } from "@/i18n/navigation";
import { getTestingEventWorkspaceData } from "@/lib/testing-lab/events-queries";
import { getTestingProjectOptions } from "@/lib/testing-lab/queries";
import { formatTestingEventStatus } from "@/lib/testing-lab/format";
import { Badge } from "@game-guild/ui/components/badge";
import { Button } from "@game-guild/ui/components/button";
import { ArrowRight, FolderKanban } from "lucide-react";
import { notFound } from "next/navigation";

export default async function TestingEventProjectsPage({
  params,
}: {
  params: Promise<{ eventId: string }>;
}) {
  const { eventId } = await params;
  const [detail, projects] = await Promise.all([
    getTestingEventWorkspaceData(eventId),
    getTestingProjectOptions(),
  ]);

  if (!detail.event) notFound();

  const projectById = new Map(projects.map((project) => [project.id, project]));
  const applications = [...detail.applications].sort((left, right) => {
    if (left.status === right.status) return 0;
    return left.status === "Approved" ? -1 : right.status === "Approved" ? 1 : 0;
  });

  return (
    <div className="space-y-5">
      <TestingLabPageHeader
        headingLevel={2}
        icon={FolderKanban}
        title="Projects"
        description="Projects connected to this event, from application through testing readiness."
      />

      {applications.length === 0 ? (
        <TestingLabEmptyState
          title="No projects yet"
          description="Projects appear here after they apply to this event."
          action={
            <Button asChild variant="outline">
              <Link href={`/workspace/testing-lab/events/${eventId}/applications`}>
                Review applications
              </Link>
            </Button>
          }
        />
      ) : (
        <section className="divide-y" aria-label="Event projects">
          {applications.map((application) => {
            const project = application.projectId
              ? projectById.get(application.projectId)
              : undefined;
            return (
              <article
                key={application.id}
                className="flex flex-col gap-3 py-4 sm:flex-row sm:items-center sm:justify-between"
              >
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="truncate font-medium">
                      {project?.title ?? "Project details unavailable"}
                    </h2>
                    <Badge variant="outline">
                      {formatTestingEventStatus(application.status)}
                    </Badge>
                  </div>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {application.assignedSlotId
                      ? "Assigned to a testing slot"
                      : "No testing slot assigned"}
                  </p>
                </div>
                <Button asChild size="sm" variant="ghost">
                  <Link
                    href={`/workspace/testing-lab/events/${eventId}/applications`}
                  >
                    Open application
                    <ArrowRight aria-hidden="true" />
                  </Link>
                </Button>
              </article>
            );
          })}
        </section>
      )}
    </div>
  );
}
