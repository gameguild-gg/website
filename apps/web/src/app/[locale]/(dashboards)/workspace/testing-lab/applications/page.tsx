import { TestingLabPageHeader } from '@/components/testing-lab/testing-lab-page-header';
import { TestingLabAccessIssues, TestingLabEmptyState } from '@/components/testing-lab/testing-lab-state';
import { Link } from '@/i18n/navigation';
import { getMembers } from '@/lib/community/queries/members';
import { getTestingApplicationsDirectory } from '@/lib/testing-lab/events-queries';
import { formatTestingEventStatus } from '@/lib/testing-lab/format';
import { getTestingProjectOptions } from '@/lib/testing-lab/queries';
import type { TestingLabTestingApplicationStatus } from '@game-guild/client';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Input } from '@game-guild/ui/components/input';
import { CalendarDays, ClipboardCheck, FolderKanban, Search, UserRound } from 'lucide-react';

const statuses: Array<{ label: string; value?: TestingLabTestingApplicationStatus }> = [
  { label: 'All' },
  { label: 'Pending', value: 'Pending' },
  { label: 'In review', value: 'UnderReview' },
  { label: 'Approved', value: 'Approved' },
  { label: 'Waitlisted', value: 'Waitlisted' },
  { label: 'Rejected', value: 'Rejected' },
  { label: 'Withdrawn', value: 'Withdrawn' },
];

function pageHref(page: number, q: string, status?: TestingLabTestingApplicationStatus) {
  const params = new URLSearchParams();
  if (q) params.set('q', q);
  if (status) params.set('status', status);
  params.set('page', String(page));
  return `/workspace/testing-lab/applications?${params}`;
}

export default async function TestingLabApplicationsPage({
  searchParams,
}: {
  searchParams: Promise<{ q?: string; status?: string; page?: string }>;
}) {
  const query = await searchParams;
  const status = statuses.find((candidate) => candidate.value === query.status)?.value;
  const q = query.q?.trim().toLocaleLowerCase() ?? '';
  const requestedPage = Math.max(1, Number.parseInt(query.page ?? '1', 10) || 1);
  const pageSize = 25;
  const [directory, projects, memberDirectory] = await Promise.all([
    getTestingApplicationsDirectory({ status }),
    getTestingProjectOptions(),
    getMembers({ page: 1, limit: 100 }),
  ]);
  const projectLabels = Object.fromEntries(projects.map((project) => [project.id, project.title]));
  const memberLabels = Object.fromEntries(
    memberDirectory.members.map((member) => [
      member.id,
      member.displayName && member.email
        ? `${member.displayName} / ${member.email}`
        : member.displayName || member.email || 'Member details unavailable',
    ]),
  );
  const filtered = directory.entries.filter(({ event, application }) => {
    if (!q) return true;
    const projectLabel = application.projectId ? projectLabels[application.projectId] : '';
    return `${event.name ?? ''} ${projectLabel ?? ''} ${application.preferredAvailability ?? ''}`
      .toLocaleLowerCase()
      .includes(q);
  });
  const pageCount = Math.max(1, Math.ceil(filtered.length / pageSize));
  const page = Math.min(requestedPage, pageCount);
  const entries = filtered.slice((page - 1) * pageSize, page * pageSize);

  return (
    <div className="space-y-6 p-4 lg:p-6">
      <TestingLabPageHeader
        icon={ClipboardCheck}
        title="Project applications"
        description="Review Project-owned applications across every managed Testing Lab event. Decisions remain in the event workspace."
      />
      <TestingLabAccessIssues issues={directory.accessIssues} />

      <nav aria-label="Application status" className="flex flex-wrap gap-2">
        {statuses.map((candidate) => (
          <Button
            key={candidate.label}
            asChild
            size="sm"
            variant={candidate.value === status || (!candidate.value && !status) ? 'default' : 'outline'}
          >
            <Link href={candidate.value ? `/workspace/testing-lab/applications?status=${candidate.value}` : '/workspace/testing-lab/applications'}>
              {candidate.label}
            </Link>
          </Button>
        ))}
      </nav>

      <form method="get" className="flex max-w-2xl flex-col gap-2 sm:flex-row">
        {status ? <input type="hidden" name="status" value={status} /> : null}
        <label className="relative flex-1">
          <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            aria-label="Search project applications"
            className="pl-9"
            defaultValue={query.q ?? ''}
            name="q"
            placeholder="Search event, Project, or availability"
            type="search"
          />
        </label>
        <Button type="submit" variant="outline">Search</Button>
      </form>

      {entries.length === 0 ? (
        <TestingLabEmptyState
          title={directory.entries.length === 0 ? 'No project applications' : 'No applications match these filters'}
          description={
            directory.entries.length === 0
              ? 'Applications submitted to managed events will appear here.'
              : 'Change the search or status filter and try again.'
          }
        />
      ) : (
        <section className="divide-y rounded-md border" aria-label="Project application directory">
          {entries.map(({ event, application }) => {
            const projectLabel = application.projectId
              ? projectLabels[application.projectId] ?? `Project ${application.projectId}`
              : 'Project details unavailable';
            const memberLabel = application.submittedByUserId
              ? memberLabels[application.submittedByUserId] ?? `Member ${application.submittedByUserId}`
              : 'Submitter unavailable';
            const eventLabel = event.name ?? 'Untitled testing event';
            const reviewHref = `/workspace/testing-lab/events/${event.id}/applications${
              application.status ? `?applicationStatus=${application.status}` : ''
            }`;

            return (
              <article key={application.id ?? `${event.id}-${application.projectId}`} className="grid gap-4 p-4 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-center">
                <div className="min-w-0 space-y-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="font-semibold">{eventLabel}</h2>
                    <Badge variant="outline">{formatTestingEventStatus(application.status)}</Badge>
                  </div>
                  <p className="flex items-center gap-2 text-sm"><FolderKanban className="size-4 text-muted-foreground" />{projectLabel}</p>
                  <div className="flex flex-wrap gap-x-5 gap-y-1 text-xs text-muted-foreground">
                    <span className="flex items-center gap-1.5"><UserRound className="size-3.5" />{memberLabel}</span>
                    <span className="flex items-center gap-1.5"><CalendarDays className="size-3.5" />{application.preferredAvailability || 'No preferred availability'}</span>
                  </div>
                </div>
                {event.id ? (
                  <Button asChild variant="outline">
                    <Link href={reviewHref}>Review in {eventLabel}</Link>
                  </Button>
                ) : null}
              </article>
            );
          })}
        </section>
      )}

      {pageCount > 1 ? (
        <nav aria-label="Application pages" className="flex items-center justify-end gap-2">
          <Button asChild size="sm" variant="outline" disabled={page <= 1}>
            <Link href={pageHref(Math.max(1, page - 1), q, status)}>Previous</Link>
          </Button>
          <span className="text-sm text-muted-foreground">Page {page} of {pageCount}</span>
          <Button asChild size="sm" variant="outline" disabled={page >= pageCount}>
            <Link href={pageHref(Math.min(pageCount, page + 1), q, status)}>Next</Link>
          </Button>
        </nav>
      ) : null}
    </div>
  );
}
