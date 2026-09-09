import { TestingLabCalendar } from "@/components/testing-lab/testing-lab-calendar";
import { CreateTestingEventDialog } from "@/components/testing-lab/testing-event-management";
import { TestingLabAccessIssues } from "@/components/testing-lab/testing-lab-state";
import { Link } from "@/i18n/navigation";
import {
  getTestingLabAnalytics,
  getTestingLabDashboard,
  getTestingLabSettings,
  normalizeTestingSessionStatus,
} from "@/lib/testing-lab";
import {
  getTestingApplicationsDirectory,
  getTestingEventsDirectory,
} from "@/lib/testing-lab/events-queries";
import { buttonVariants } from "@game-guild/ui/components/button";
import {
  CalendarClock,
  ClipboardCheck,
  ExternalLink,
  FlaskConical,
  TriangleAlert,
} from "lucide-react";

export default async function TestingLabPage() {
  const [directory, analytics, events, pendingApplications, labSettings] =
    await Promise.all([
      getTestingLabDashboard(),
      getTestingLabAnalytics(),
      getTestingEventsDirectory({ take: 100 }),
      getTestingApplicationsDirectory({ status: "Pending" }),
      getTestingLabSettings(),
    ]);
  const issues = [
    ...directory.accessIssues,
    ...analytics.accessIssues,
    ...events.accessIssues,
    ...pendingApplications.accessIssues,
    ...labSettings.accessIssues,
  ];
  const draftEvents = events.events.filter(
    (event) => event.status === "Draft",
  ).length;
  const incompleteSessions = directory.sessions.filter(
    (session) =>
      !session.isDeleted &&
      normalizeTestingSessionStatus(session.status) === "Scheduled" &&
      (!session.sessionDate ||
        !session.startTime ||
        !session.endTime ||
        !session.location),
  ).length;
  const attentionItems = [
    {
      count: pendingApplications.entries.length,
      label: `${pendingApplications.entries.length} pending application${pendingApplications.entries.length === 1 ? "" : "s"}`,
      href: "/workspace/testing-lab/applications?status=Pending",
      Icon: ClipboardCheck,
    },
    {
      count: draftEvents,
      label: `${draftEvents} draft event${draftEvents === 1 ? "" : "s"}`,
      href: "/workspace/testing-lab/events?status=Draft",
      Icon: FlaskConical,
    },
    {
      count: incompleteSessions,
      label: `${incompleteSessions} incomplete session${incompleteSessions === 1 ? "" : "s"}`,
      href: "/workspace/testing-lab/sessions",
      Icon: CalendarClock,
    },
  ].filter((item) => item.count > 0);

  return (
    <div className="-m-4 flex h-[calc(100dvh-4rem)] min-h-[38rem] flex-col overflow-hidden sm:-m-6">
      {issues.length > 0 || attentionItems.length > 0 ? (
        <div className="px-4 lg:px-6">
          <TestingLabAccessIssues issues={[...new Set(issues)]} />

          {attentionItems.length > 0 ? (
            <section
              aria-label="Testing Lab attention"
              className="flex flex-col gap-2 border-b py-3 sm:flex-row sm:items-center"
            >
              <div className="flex shrink-0 items-center gap-2 text-sm font-medium">
                <TriangleAlert
                  className="size-4 text-muted-foreground"
                  aria-hidden="true"
                />
                Needs attention
              </div>
              <div className="flex flex-wrap items-center gap-x-1 gap-y-1 sm:ml-auto">
                {attentionItems.map(({ label, href, Icon }) => (
                  <Link
                    key={href}
                    href={href}
                    className="inline-flex min-h-9 items-center gap-2 rounded-md px-2.5 text-sm text-muted-foreground transition-colors hover:bg-muted/60 hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                  >
                    <Icon className="size-4" aria-hidden="true" />
                    {label}
                  </Link>
                ))}
              </div>
            </section>
          ) : null}
        </div>
      ) : null}

      <div className="min-h-0 flex-1">
        <TestingLabCalendar
          events={events.events}
          eventAnalytics={analytics.events}
          defaultTimeZone={labSettings.settings?.timezone ?? "UTC"}
          toolbarStart={
            <div className="flex items-center gap-2">
              <div className="flex size-8 items-center justify-center rounded-md bg-muted/60">
                <FlaskConical className="size-4" aria-hidden="true" />
              </div>
              <h1 className="text-lg font-semibold">Testing Lab</h1>
            </div>
          }
          toolbarEnd={
            <>
              <Link
                href="/testing-lab"
                aria-label="Open public Testing Lab"
                className={buttonVariants({ variant: "outline", size: "sm" })}
              >
                <ExternalLink data-icon="inline-start" aria-hidden="true" />
                Public page
              </Link>
              <CreateTestingEventDialog
                defaultTimeZone={labSettings.settings?.timezone ?? "UTC"}
              />
            </>
          }
        />
      </div>
    </div>
  );
}
