import { CreateTestingEventDialog, RestoreTestingEventDialog } from '@/components/testing-lab/testing-event-management';
import { formatTestingEventStatus } from '@/lib/testing-lab/format';
import { TestingLabPageHeader } from '@/components/testing-lab/testing-lab-page-header';
import { TestingLabAccessIssues, TestingLabEmptyState } from '@/components/testing-lab/testing-lab-state';
import { Link } from '@/i18n/navigation';
import { getTestingLabSettings } from '@/lib/testing-lab';
import { getArchivedTestingEventsDirectory, getTestingEventTemplates, getTestingEventsDirectory } from '@/lib/testing-lab/events-queries';
import type { TestingLabTestingEventStatus } from '@game-guild/client';
import { Badge } from '@game-guild/ui/components/badge';
import { Button, buttonVariants } from '@game-guild/ui/components/button';
import { Input } from '@game-guild/ui/components/input';
import { CalendarDays, ChevronRight, FlaskConical, Layers3 } from 'lucide-react';

const statuses: Array<{ value?: TestingLabTestingEventStatus; label: string }> = [
  { label: 'All' },
  { value: 'Draft', label: 'Draft' },
  { value: 'ApplicationsOpen', label: 'Applications open' },
  { value: 'ApplicationsClosed', label: 'Applications closed' },
  { value: 'Scheduled', label: 'Scheduled' },
  { value: 'Active', label: 'Active' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Cancelled', label: 'Cancelled' },
];

function eventDate(value?: string | null) {
  if (!value) return 'Date not set';
  const date = new Date(value);
  if (Number.isNaN(date.valueOf())) return 'Date not set';
  const formatted = new Intl.DateTimeFormat('en', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'UTC',
  }).format(date);
  return `${formatted} UTC`;
}

export default async function TestingEventsPage({
  searchParams,
}: {
  searchParams: Promise<{ status?: string; q?: string; page?: string; archived?: string }>;
}) {
  const query = await searchParams;
  const archived = query.archived === 'true';
  const selectedStatus = statuses.find((status) => status.value === query.status)?.value;
  const directory = archived
    ? await getArchivedTestingEventsDirectory({ skip: 0, take: 100 })
    : await getTestingEventsDirectory({ status: selectedStatus, skip: 0, take: 100 });
  const templates = archived ? { templates: [], accessIssues: [] } : await getTestingEventTemplates();
  const labSettings = await getTestingLabSettings();
  const searchTerm = query.q?.trim().toLocaleLowerCase() ?? '';
  const filteredEvents = directory.events.filter((event) =>
    searchTerm
      ? `${event.name ?? ''} ${event.description ?? ''} ${event.mode ?? ''}`.toLocaleLowerCase().includes(searchTerm)
      : true,
  );
  const pageSize = 25;
  const page = Math.max(1, Number.parseInt(query.page ?? '1', 10) || 1);
  const pageCount = Math.max(1, Math.ceil(filteredEvents.length / pageSize));
  const visibleEvents = filteredEvents.slice((Math.min(page, pageCount) - 1) * pageSize, Math.min(page, pageCount) * pageSize);
  const querySuffix = `${archived ? '&archived=true' : ''}${selectedStatus ? `&status=${selectedStatus}` : ''}${query.q ? `&q=${encodeURIComponent(query.q)}` : ''}`;

  return (
    <div className="space-y-6 p-4 lg:p-6">
      <TestingLabPageHeader
        icon={CalendarDays}
        title="Testing events"
        description="Open application windows, review existing community projects, reserve capacity after approval, and operate each tester slot."
        actions={
          <CreateTestingEventDialog
            templates={templates.templates}
            defaultTimeZone={labSettings.settings?.timezone ?? 'UTC'}
          />
        }
      />
      <TestingLabAccessIssues issues={[...directory.accessIssues, ...templates.accessIssues, ...labSettings.accessIssues]} />
      <nav aria-label="Filter testing events" className="flex flex-wrap gap-2">
        {statuses.map((status) => {
          const active = !archived && (status.value === selectedStatus || (!status.value && !selectedStatus));
          return (
            <Link
              key={status.label}
              className={buttonVariants({ size: 'sm', variant: active ? 'default' : 'outline' })}
              href={status.value ? `/console/community/testing-lab/events?status=${status.value}` : '/console/community/testing-lab/events'}
            >
              {status.label}
            </Link>
          );
        })}
        <Link
          className={buttonVariants({ size: 'sm', variant: archived ? 'default' : 'outline' })}
          href="/console/community/testing-lab/events?archived=true"
        >
          Archived
        </Link>
      </nav>
      <form method="get" className="flex max-w-2xl flex-col gap-2 sm:flex-row">
        {selectedStatus ? <input type="hidden" name="status" value={selectedStatus} /> : null}
        {archived ? <input type="hidden" name="archived" value="true" /> : null}
        <Input
          name="q"
          type="search"
          defaultValue={query.q ?? ''}
          placeholder="Search events by name, brief, or mode"
          aria-label="Search testing events"
        />
        <Button type="submit" variant="outline">Search</Button>
      </form>
      {visibleEvents.length === 0 ? (
        <TestingLabEmptyState
          title={searchTerm ? 'No matching events' : archived ? 'No archived events' : 'No testing events'}
          description={searchTerm ? 'Adjust the search or status filter.' : archived ? 'Completed and cancelled events can be archived from their management workspace.' : 'Create an event to collect project applications and organize independent online or campus test slots.'}
          action={
            archived ? undefined : (
              <CreateTestingEventDialog
                templates={templates.templates}
                defaultTimeZone={labSettings.settings?.timezone ?? 'UTC'}
              />
            )
          }
        />
      ) : (
        <section className="divide-y rounded-md border" aria-label="Testing event directory">
          {visibleEvents.map((event) => (
            <article key={event.id} className="grid gap-4 p-4 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-center">
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <h2 className="truncate font-semibold">{event.name ?? 'Untitled testing event'}</h2>
                  <Badge variant="outline">{formatTestingEventStatus(event.status)}</Badge>
                  <Badge variant="secondary">{event.mode ?? 'Online'}</Badge>
                </div>
                <p className="mt-1 line-clamp-2 text-sm text-muted-foreground">{event.description ?? 'No event brief.'}</p>
                <div className="mt-3 flex flex-wrap gap-x-5 gap-y-1 text-xs text-muted-foreground">
                  <span className="flex items-center gap-1.5"><CalendarDays className="size-3.5" />{eventDate(event.startsAt)}</span>
                  <span className="flex items-center gap-1.5"><Layers3 className="size-3.5" />{event.slotCount ?? 0} slots</span>
                  <span className="flex items-center gap-1.5"><FlaskConical className="size-3.5" />{event.applicationCount ?? 0} applications</span>
                </div>
              </div>
              {archived ? (
                <RestoreTestingEventDialog event={event} />
              ) : event.id ? (
                  <Button asChild variant="outline">
                    <Link href={`/console/community/testing-lab/events/${event.id}`}>
                      Manage event<ChevronRight className="ml-2 size-4" />
                    </Link>
                  </Button>
                ) : null}
            </article>
          ))}
        </section>
      )}
      {pageCount > 1 ? (
        <nav aria-label="Testing event pages" className="flex items-center justify-end gap-2">
          <Button asChild size="sm" variant="outline" disabled={page <= 1}>
            <Link href={`/console/community/testing-lab/events?page=${Math.max(1, page - 1)}${querySuffix}`}>Previous</Link>
          </Button>
          <span className="text-sm text-muted-foreground">Page {Math.min(page, pageCount)} of {pageCount}</span>
          <Button asChild size="sm" variant="outline" disabled={page >= pageCount}>
            <Link href={`/console/community/testing-lab/events?page=${Math.min(pageCount, page + 1)}${querySuffix}`}>Next</Link>
          </Button>
        </nav>
      ) : null}
    </div>
  );
}
