import { TestingEventCommittee } from '@/components/testing-lab/testing-event-management';
import { TestingEventConfigurationEditor } from '@/components/testing-lab/testing-event-configuration-editor';
import { TestingLabPageHeader } from '@/components/testing-lab/testing-lab-page-header';
import { Link } from '@/i18n/navigation';
import { getMembers } from '@/lib/community/queries/members';
import {
  countLabel,
  formatEventDateTime,
  isTestingEventReadOnly,
} from '@/lib/testing-lab/event-workspace';
import { getTestingEventWorkspaceData } from '@/lib/testing-lab/events-queries';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import {
  ArrowRight,
  CalendarClock,
  ClipboardList,
  UsersRound,
} from 'lucide-react';
import { notFound } from 'next/navigation';

export default async function TestingEventOverviewPage({
  params,
}: {
  params: Promise<{ eventId: string }>;
}) {
  const { eventId } = await params;
  const [detail, memberDirectory] = await Promise.all([
    getTestingEventWorkspaceData(eventId),
    getMembers({ page: 1, limit: 100 }),
  ]);

  if (!detail.event) notFound();
  const event = detail.event;
  const registrations = Object.values(detail.registrationsBySlot).flat();
  const pendingApplications = detail.applications.filter((item) =>
    ['Pending', 'UnderReview', 'Waitlisted'].includes(item.status ?? 'Pending'),
  ).length;
  const readOnly = isTestingEventReadOnly(event);

  const metrics = [
    {
      label: countLabel(detail.applications.length, 'project application'),
      note: countLabel(pendingApplications, 'awaiting decision'),
      icon: ClipboardList,
    },
    {
      label: countLabel(detail.slots.length, 'testing slot'),
      note: detail.slots.length ? 'Independent capacity windows' : 'Schedule not configured',
      icon: CalendarClock,
    },
    {
      label: countLabel(registrations.length, 'registered tester'),
      note: 'Across every event slot',
      icon: UsersRound,
    },
  ];

  return (
    <div className="space-y-6">
      <TestingLabPageHeader
        headingLevel={2}
        icon={CalendarClock}
        title="Event overview"
        description="Review operational readiness, application demand, capacity, and governance before the event starts."
      />

      <section aria-label="Event metrics" className="grid gap-3 md:grid-cols-3">
        {metrics.map(({ label, note, icon: Icon }) => (
          <article key={label} className="rounded-md border p-4">
            <div className="flex items-center gap-2 text-muted-foreground">
              <Icon className="size-4" />
              <p className="text-sm">{label}</p>
            </div>
            <p className="mt-3 text-sm font-medium">{note}</p>
          </article>
        ))}
      </section>

      <section className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_minmax(320px,0.7fr)]">
        <article className="rounded-md border p-4">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <h2 className="font-semibold">Timeline and delivery</h2>
              <p className="mt-1 text-sm text-muted-foreground">
                Application intake and event delivery are managed independently.
              </p>
            </div>
            <Badge variant="outline">{event.mode}</Badge>
          </div>
          <dl className="mt-4 grid gap-4 sm:grid-cols-2">
            <div>
              <dt className="text-xs font-medium uppercase text-muted-foreground">Applications</dt>
              <dd className="mt-1 text-sm">
                {formatEventDateTime(event.applicationsOpenAt)} to {formatEventDateTime(event.applicationsCloseAt)}
              </dd>
            </div>
            <div>
              <dt className="text-xs font-medium uppercase text-muted-foreground">Event window</dt>
              <dd className="mt-1 text-sm">
                {formatEventDateTime(event.startsAt)} to {formatEventDateTime(event.endsAt)}
              </dd>
            </div>
          </dl>
          <div className="mt-4 flex flex-wrap gap-2 border-t pt-4">
            <Button asChild size="sm" variant="outline">
              <Link href={`/workspace/testing-lab/events/${eventId}/applications`}>
                Review applications <ArrowRight className="ml-2 size-4" />
              </Link>
            </Button>
            <Button asChild size="sm" variant="outline">
              <Link href={`/workspace/testing-lab/events/${eventId}/schedule`}>
                Manage schedule <ArrowRight className="ml-2 size-4" />
              </Link>
            </Button>
          </div>
        </article>

        <article className="rounded-md border p-4">
          <TestingEventCommittee
            event={event}
            members={memberDirectory.members.map((member) => ({
              id: member.id,
              label: `${member.displayName} / ${member.email}`,
            }))}
            committee={detail.committee}
            readOnly={readOnly}
          />
        </article>
      </section>

      <TestingEventConfigurationEditor
        eventId={eventId}
        status={event.status}
        configuration={event.configuration}
      />

      <section className="rounded-md border p-4">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="font-semibold">Learning evidence</h2>
            <p className="mt-1 text-sm text-muted-foreground">
              {event.courseId
                ? 'Attendance and feedback can publish evidence to the connected course activity.'
                : 'No course activity is connected. Testing Lab evidence remains available in this event.'}
            </p>
          </div>
          <Button asChild size="sm" variant="outline">
            <Link href={`/workspace/testing-lab/events/${eventId}/learning`}>
              Open learning setup <ArrowRight className="ml-2 size-4" />
            </Link>
          </Button>
        </div>
      </section>
    </div>
  );
}
