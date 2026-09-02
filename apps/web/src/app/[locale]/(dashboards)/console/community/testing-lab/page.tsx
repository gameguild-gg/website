import { TestingLabCalendar } from '@/components/testing-lab/testing-lab-calendar';
import { CreateTestingEventDialog } from '@/components/testing-lab/testing-event-management';
import { TestingLabPageHeader } from '@/components/testing-lab/testing-lab-page-header';
import { TestingLabAccessIssues } from '@/components/testing-lab/testing-lab-state';
import { Link } from '@/i18n/navigation';
import {
  getTestingLabAnalytics,
  getTestingLabDashboard,
  normalizeTestingRequestStatus,
  normalizeTestingSessionStatus,
} from '@/lib/testing-lab';
import { getTestingApplicationsDirectory, getTestingEventsDirectory } from '@/lib/testing-lab/events-queries';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import {
  ArrowUpRight,
  CalendarCheck2,
  CheckCircle2,
  ClipboardCheck,
  FlaskConical,
  Gauge,
  MessageSquareText,
} from 'lucide-react';

export default async function TestingLabPage() {
  const [directory, analytics, events, pendingApplications] = await Promise.all([
    getTestingLabDashboard(),
    getTestingLabAnalytics(),
    getTestingEventsDirectory({ take: 100 }),
    getTestingApplicationsDirectory({ status: 'Pending' }),
  ]);
  const issues = [
    ...directory.accessIssues,
    ...analytics.accessIssues,
    ...events.accessIssues,
    ...pendingApplications.accessIssues,
  ];
  const hasEvents = events.events.length > 0;
  const applicationsOpen = events.events.filter((event) => event.status === 'ApplicationsOpen').length;
  const draftEvents = events.events.filter((event) => event.status === 'Draft').length;
  const upcomingSessions = directory.sessions.filter(
    (session) => !session.isDeleted && normalizeTestingSessionStatus(session.status) === 'Scheduled',
  );
  const incompleteSessions = upcomingSessions.filter(
    (session) => !session.sessionDate || !session.startTime || !session.endTime || !session.location,
  ).length;
  const capacityMetric =
    analytics.current.capacity > 0
      ? {
          value: `${analytics.current.fillRate}%`,
          detail: `${analytics.current.registeredTesters}/${analytics.current.capacity} seats`,
        }
      : analytics.current.registeredTesters > 0
        ? {
            value: 'Unlimited',
            detail: `${analytics.current.registeredTesters} registered`,
          }
        : { value: '-', detail: 'No capacity configured' };

  return (
    <div className="space-y-6 p-4 lg:p-6">
      <TestingLabPageHeader
        icon={FlaskConical}
        title="Testing Lab"
        description="Manage applications, testing sessions, and participant feedback."
        actions={
          <>
            {hasEvents ? <CreateTestingEventDialog /> : null}
            <Button asChild variant="outline" className="h-11 sm:h-9">
              <Link href="/testing-lab">
                View public lab
                <ArrowUpRight className="ml-2 size-4" />
              </Link>
            </Button>
          </>
        }
      />

      <TestingLabAccessIssues issues={[...new Set(issues)]} />

      {!hasEvents ? (
        <section className="rounded-xl bg-muted/30 p-5 md:p-6" aria-labelledby="testing-lab-onboarding-title">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Launch checklist</p>
              <h2 id="testing-lab-onboarding-title" className="mt-1 text-xl font-semibold">
                Launch your first testing cycle
              </h2>
              <p className="mt-1 max-w-2xl text-sm text-muted-foreground">
                Set up the event once, then open it to eligible project teams and testers.
              </p>
            </div>
            <CreateTestingEventDialog />
          </div>
          <ol className="mt-6 grid gap-1 sm:grid-cols-2 sm:gap-3 xl:grid-cols-4">
            {[
              ['01', 'Create an event', 'Set the application and testing window.'],
              ['02', 'Add rules and instructions', 'Tell project teams and testers what to prepare.'],
              ['03', 'Configure slots and capacity', 'Define when and how many people can participate.'],
              ['04', 'Open applications', 'Publish the event when every requirement is ready.'],
            ].map(([step, title, detail]) => (
              <li key={step} className="flex gap-3 py-2 sm:block sm:rounded-lg sm:bg-background/70 sm:p-4">
                <span className="shrink-0 text-xs font-semibold text-muted-foreground">{step}</span>
                <div>
                  <p className="text-sm font-semibold sm:mt-3">{title}</p>
                  <p className="mt-1 text-sm text-muted-foreground">{detail}</p>
                </div>
              </li>
            ))}
          </ol>
        </section>
      ) : (
        <section aria-labelledby="testing-lab-attention-title">
          <div className="mb-3">
            <h2 id="testing-lab-attention-title" className="text-lg font-semibold">
              Needs attention
            </h2>
            <p className="text-sm text-muted-foreground">The next decisions blocking a testing cycle.</p>
          </div>
          {pendingApplications.entries.length + draftEvents + incompleteSessions === 0 ? (
            <div className="flex items-center gap-3 rounded-lg bg-muted/25 p-4 text-sm">
              <CheckCircle2 className="size-5 text-primary" aria-hidden="true" />
              <div>
                <p className="font-medium">Nothing is blocking the lab</p>
                <p className="text-muted-foreground">Applications, event drafts, and upcoming sessions are ready.</p>
              </div>
            </div>
          ) : (
            <div className="grid gap-2 md:grid-cols-3">
              {[
                {
                  count: pendingApplications.entries.length,
                  label: `${pendingApplications.entries.length} pending application${pendingApplications.entries.length === 1 ? '' : 's'}`,
                  detail: 'Review project eligibility and testing materials.',
                  href: '/console/community/testing-lab/applications?status=Pending',
                  Icon: ClipboardCheck,
                },
                {
                  count: draftEvents,
                  label: `${draftEvents} draft event${draftEvents === 1 ? '' : 's'} to finish`,
                  detail: 'Complete rules, instructions, slots, and capacity.',
                  href: '/console/community/testing-lab/events?status=Draft',
                  Icon: CalendarCheck2,
                },
                {
                  count: incompleteSessions,
                  label: `${incompleteSessions} incomplete session${incompleteSessions === 1 ? '' : 's'}`,
                  detail: 'Add the missing schedule or location details.',
                  href: '/console/community/testing-lab/sessions',
                  Icon: Gauge,
                },
              ]
                .filter((item) => item.count > 0)
                .map(({ label, detail, href, Icon }) => (
                  <Link
                    key={href}
                    href={href}
                    className="group rounded-lg bg-muted/30 p-4 transition-colors hover:bg-muted/50"
                  >
                    <Icon
                      className="size-5 text-muted-foreground transition-colors group-hover:text-foreground"
                      aria-hidden="true"
                    />
                    <p className="mt-4 text-sm font-semibold">{label}</p>
                    <p className="mt-1 text-sm text-muted-foreground">{detail}</p>
                  </Link>
                ))}
            </div>
          )}
        </section>
      )}

      <section aria-label="Testing Lab metrics" className="grid grid-cols-2 gap-2 xl:grid-cols-4">
        {[
          {
            label: 'Applications',
            value: analytics.current.applications,
            detail: `${pendingApplications.entries.length} awaiting review`,
            href: '/console/community/testing-lab/applications',
            Icon: ClipboardCheck,
          },
          {
            label: 'Applications open',
            value: applicationsOpen,
            detail: `${analytics.current.events} total events`,
            href: '/console/community/testing-lab/events?status=ApplicationsOpen',
            Icon: CalendarCheck2,
          },
          {
            label: 'Capacity fill',
            value: capacityMetric.value,
            detail: capacityMetric.detail,
            href: '/console/community/testing-lab/participants',
            Icon: Gauge,
          },
          {
            label: 'Feedback responses',
            value: analytics.current.feedback,
            detail:
              analytics.current.averageRating === null
                ? 'No ratings yet'
                : `${analytics.current.averageRating}/10 average`,
            href: '/console/community/testing-lab/feedback',
            Icon: MessageSquareText,
          },
        ].map(({ label, value, detail, href, Icon }) => (
          <Link
            key={label}
            href={href}
            className="group min-w-0 rounded-lg bg-muted/30 p-4 transition-colors hover:bg-muted/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <div className="flex items-center justify-between gap-2">
              <p className="truncate text-sm font-medium text-muted-foreground">{label}</p>
              <Icon
                className="size-4 shrink-0 text-muted-foreground transition-colors group-hover:text-foreground"
                aria-hidden="true"
              />
            </div>
            <p className="mt-1 text-2xl font-semibold">{value}</p>
            <p className="mt-1 text-sm text-muted-foreground">{detail}</p>
          </Link>
        ))}
      </section>

      <TestingLabCalendar events={events.events} eventAnalytics={analytics.events} />

      <section className="grid gap-6 xl:grid-cols-2">
        <div>
          <div className="mb-3 flex items-center justify-between">
            <h2 className="text-lg font-semibold">Recent testing projects</h2>
            <Button asChild variant="ghost" size="sm">
              <Link href="/console/community/testing-lab/projects">View all</Link>
            </Button>
          </div>
          {directory.requests.length === 0 ? (
            <div className="flex min-h-40 flex-col justify-center rounded-lg bg-muted/25 p-5">
              <p className="font-semibold">No testing projects yet</p>
              <p className="mt-1 text-sm text-muted-foreground">
                Eligible project builds will appear here when developers prepare them for testing.
              </p>
            </div>
          ) : (
            <div className="divide-y divide-border/50 overflow-hidden rounded-lg bg-muted/25">
              {directory.requests.slice(0, 5).map((request) => (
                <Link
                  key={request.id}
                  href={`/console/community/testing-lab/projects/${request.id}`}
                  className="flex items-center justify-between gap-4 p-3 hover:bg-muted/30"
                >
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium">{request.title}</p>
                    <p className="truncate text-xs text-muted-foreground">
                      {request.description ?? 'No objective provided'}
                    </p>
                  </div>
                  <Badge variant="outline">{normalizeTestingRequestStatus(request.status)}</Badge>
                </Link>
              ))}
            </div>
          )}
        </div>
        <div>
          <div className="mb-3 flex items-center justify-between">
            <h2 className="text-lg font-semibold">Upcoming sessions</h2>
            <Button asChild variant="ghost" size="sm">
              <Link href="/console/community/testing-lab/sessions">View all</Link>
            </Button>
          </div>
          {directory.sessions.length === 0 ? (
            <div className="flex min-h-40 flex-col justify-center rounded-lg bg-muted/25 p-5">
              <p className="font-semibold">No upcoming sessions</p>
              <p className="mt-1 text-sm text-muted-foreground">
                Scheduled testing slots will appear here when an event is ready.
              </p>
            </div>
          ) : (
            <div className="divide-y divide-border/50 overflow-hidden rounded-lg bg-muted/25">
              {directory.sessions.slice(0, 5).map((session) => (
                <Link
                  key={session.id}
                  href={`/console/community/testing-lab/sessions/${session.id}`}
                  className="flex items-center justify-between gap-4 p-3 hover:bg-muted/30"
                >
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium">{session.sessionName}</p>
                    <p className="truncate text-xs text-muted-foreground">
                      {session.location?.name ?? 'Location not assigned'}
                    </p>
                  </div>
                  <Badge variant="outline">{normalizeTestingSessionStatus(session.status)}</Badge>
                </Link>
              ))}
            </div>
          )}
        </div>
      </section>
    </div>
  );
}
