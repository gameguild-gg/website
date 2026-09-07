import { TestingLabPageHeader } from "@/components/testing-lab/testing-lab-page-header";
import {
  TestingLabAccessIssues,
  TestingLabEmptyState,
} from "@/components/testing-lab/testing-lab-state";
import { Link } from "@/i18n/navigation";
import {
  getTestingLabAnalytics,
  type TestingLabAnalyticsSummary,
} from "@/lib/testing-lab";
import {
  resolveTestingLabAnalyticsPeriod,
  type TestingLabAnalyticsPeriodParams,
} from "@/lib/testing-lab/analytics-period";
import { Badge } from "@game-guild/ui/components/badge";
import { Button } from "@game-guild/ui/components/button";
import { Input } from "@game-guild/ui/components/input";
import {
  ArrowRight,
  BarChart3,
  CalendarRange,
  Download,
  MapPin,
  Minus,
  TrendingDown,
  TrendingUp,
  Users,
} from "lucide-react";

interface PageProps {
  searchParams: Promise<TestingLabAnalyticsPeriodParams>;
}

interface MetricDefinition {
  label: string;
  value: string;
  current: number | null;
  previous: number | null;
  detail: string;
}

const numberFormatter = new Intl.NumberFormat("en-US", {
  maximumFractionDigits: 1,
});
const dateFormatter = new Intl.DateTimeFormat("en-US", {
  month: "short",
  day: "numeric",
  year: "numeric",
  timeZone: "UTC",
});
const shortDateFormatter = new Intl.DateTimeFormat("en-US", {
  month: "short",
  day: "numeric",
  timeZone: "UTC",
});

function comparisonLabel(current: number | null, previous: number | null) {
  if (current === null || previous === null)
    return {
      text: "No previous-period value",
      Icon: Minus,
      tone: "text-muted-foreground",
    };
  const difference = Number((current - previous).toFixed(1));
  if (difference === 0)
    return {
      text: "No change vs previous period",
      Icon: Minus,
      tone: "text-muted-foreground",
    };
  return {
    text: `${difference > 0 ? "+" : ""}${numberFormatter.format(difference)} vs previous period`,
    Icon: difference > 0 ? TrendingUp : TrendingDown,
    tone:
      difference > 0
        ? "text-emerald-600 dark:text-emerald-400"
        : "text-amber-600 dark:text-amber-400",
  };
}

function Metric({ metric }: { metric: MetricDefinition }) {
  const comparison = comparisonLabel(metric.current, metric.previous);
  const Icon = comparison.Icon;
  return (
    <div className="border-b p-4 last:border-b-0 sm:odd:border-r sm:[&:nth-last-child(-n+2)]:border-b-0 xl:border-b-0 xl:border-r xl:last:border-r-0">
      <p className="text-sm font-medium text-muted-foreground">
        {metric.label}
      </p>
      <p className="mt-1 text-2xl font-semibold">{metric.value}</p>
      <p className="mt-1 text-xs text-muted-foreground">{metric.detail}</p>
      <p
        className={`mt-2 flex items-center gap-1 text-xs font-medium ${comparison.tone}`}
      >
        <Icon className="size-3.5" aria-hidden="true" />
        {comparison.text}
      </p>
    </div>
  );
}

function formatTrendDate(value: string) {
  const candidate = /^\d{4}-\d{2}-\d{2}$/.test(value)
    ? `${value}T00:00:00.000Z`
    : value;
  const date = new Date(candidate);

  return Number.isNaN(date.getTime())
    ? value
    : shortDateFormatter.format(date);
}

function ActivityTrend({
  trend,
}: {
  trend: Awaited<ReturnType<typeof getTestingLabAnalytics>>["trend"];
}) {
  const maximum = Math.max(
    1,
    ...trend.flatMap((point) => [
      point.applications,
      point.registrations,
      point.feedback,
    ]),
  );
  return (
    <div className="overflow-x-auto pb-2">
      <div
        className="flex h-56 min-w-[560px] items-end gap-2"
        role="img"
        aria-label="Applications, registrations, and feedback by day"
      >
        {trend.map((point) => (
          <div
            key={point.date}
            className="flex min-w-8 flex-1 flex-col items-center gap-2"
          >
            <div className="flex h-44 w-full items-end justify-center gap-0.5 rounded-sm bg-muted/30 px-1">
              {[
                ["Applications", point.applications, "bg-blue-500"],
                ["Registrations", point.registrations, "bg-violet-500"],
                ["Feedback", point.feedback, "bg-emerald-500"],
              ].map(([label, value, color]) => (
                <div
                  key={label}
                  className={`min-h-0.5 w-1/3 rounded-t-sm ${color}`}
                  style={{
                    height: `${Math.max(2, (Number(value) / maximum) * 100)}%`,
                  }}
                  title={`${label}: ${value}`}
                />
              ))}
            </div>
            <time
              className="text-[11px] text-muted-foreground"
              dateTime={point.date}
            >
              {formatTrendDate(point.date)}
            </time>
          </div>
        ))}
      </div>
    </div>
  );
}

function EventRows({
  events,
}: {
  events: Awaited<ReturnType<typeof getTestingLabAnalytics>>["events"];
}) {
  return (
    <div className="overflow-hidden rounded-md border">
      <div className="hidden grid-cols-[minmax(16rem,2fr)_repeat(5,minmax(6rem,1fr))_2rem] gap-3 border-b bg-muted/30 px-4 py-2 text-xs font-medium text-muted-foreground md:grid">
        <span>Event</span>
        <span>Projects</span>
        <span>Testers</span>
        <span>Attendance</span>
        <span>Feedback</span>
        <span>Fill rate</span>
        <span className="sr-only">Open</span>
      </div>
      <div className="divide-y">
        {events.map((event) => (
          <Link
            key={event.eventId}
            href={`/workspace/testing-lab/events/${event.eventId}/overview`}
            className="group block p-4 transition-colors hover:bg-muted/30 md:grid md:grid-cols-[minmax(16rem,2fr)_repeat(5,minmax(6rem,1fr))_2rem] md:items-center md:gap-3"
          >
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <span className="truncate font-medium">{event.name}</span>
                <Badge variant="outline">{event.status}</Badge>
              </div>
              <p className="mt-1 flex items-center gap-1 text-xs text-muted-foreground">
                {event.mode === "InPerson" ? (
                  <MapPin className="size-3" aria-hidden="true" />
                ) : (
                  <Users className="size-3" aria-hidden="true" />
                )}
                {dateFormatter.format(new Date(event.startsAt))} ·{" "}
                {event.mode === "InPerson" ? "In person" : "Online"}
              </p>
            </div>
            <dl className="mt-4 grid grid-cols-2 gap-3 text-sm md:contents">
              {[
                ["Projects", `${event.approvedProjects}/${event.applications}`],
                ["Testers", event.registeredTesters],
                ["Attendance", event.attendedTesters],
                ["Feedback", event.feedback],
                [
                  "Fill rate",
                  event.capacity > 0 ? `${event.fillRate}%` : "Unlimited",
                ],
              ].map(([label, value]) => (
                <div key={label} className="md:block">
                  <dt className="text-xs text-muted-foreground md:sr-only">
                    {label}
                  </dt>
                  <dd>{value}</dd>
                </div>
              ))}
            </dl>
            <ArrowRight
              className="hidden size-4 text-muted-foreground transition-transform group-hover:translate-x-0.5 md:block"
              aria-hidden="true"
            />
          </Link>
        ))}
      </div>
    </div>
  );
}

function buildMetrics(
  current: TestingLabAnalyticsSummary,
  previous: TestingLabAnalyticsSummary | null,
): MetricDefinition[] {
  return [
    {
      label: "Events",
      value: numberFormatter.format(current.events),
      current: current.events,
      previous: previous?.events ?? null,
      detail: `${current.completedEvents} completed`,
    },
    {
      label: "Approved projects",
      value: `${current.approvedProjects}/${current.applications}`,
      current: current.approvedProjects,
      previous: previous?.approvedProjects ?? null,
      detail: "Approved from submitted applications",
    },
    {
      label: "Tester attendance",
      value: `${current.attendedTesters}/${current.registeredTesters}`,
      current: current.attendedTesters,
      previous: previous?.attendedTesters ?? null,
      detail: "Attended from registered testers",
    },
    {
      label: "Average rating",
      value:
        current.averageRating === null
          ? "-"
          : `${numberFormatter.format(current.averageRating)}/10`,
      current: current.averageRating,
      previous: previous?.averageRating ?? null,
      detail: `${current.feedback} submitted feedback records`,
    },
  ];
}

export default async function TestingLabAnalyticsPage({
  searchParams,
}: PageProps) {
  const period = resolveTestingLabAnalyticsPeriod(await searchParams);
  const analytics = await getTestingLabAnalytics({
    fromDate: period.fromDate,
    toDate: period.toDate,
    includeComparison: true,
  });
  const hasActivity =
    analytics.current.events +
      analytics.current.applications +
      analytics.current.registeredTesters +
      analytics.current.feedback >
    0;
  const exportHref = `/api/testing-lab/analytics/export?from=${period.fromInput}&to=${period.toInput}`;

  return (
    <div className="space-y-6 p-4 lg:p-6">
      <TestingLabPageHeader
        icon={BarChart3}
        title="Testing Lab analytics"
        description="Compare demand, approvals, capacity, attendance, and feedback across Testing Lab events."
        actions={
          <Button asChild variant="outline">
            <a href={exportHref}>
              <Download className="mr-2 size-4" aria-hidden="true" />
              Export CSV
            </a>
          </Button>
        }
      />

      <section
        aria-label="Analytics period"
        className="flex flex-col gap-3 border-y py-3 xl:flex-row xl:items-end xl:justify-between"
      >
        <div>
          <div className="flex items-center gap-2 font-medium">
            <CalendarRange className="size-4" aria-hidden="true" />
            Reporting period
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            {dateFormatter.format(new Date(period.fromDate))} -{" "}
            {dateFormatter.format(
              new Date(new Date(period.toDate).getTime() - 1),
            )}
          </p>
        </div>
        <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
          <div className="flex gap-1" aria-label="Preset periods">
            {[7, 30, 90].map((days) => (
              <Button
                key={days}
                asChild
                size="sm"
                variant={period.range === String(days) ? "default" : "outline"}
              >
                <Link href={`/workspace/testing-lab/analytics?range=${days}`}>
                  {days} days
                </Link>
              </Button>
            ))}
          </div>
          <form
            className="flex flex-col gap-2 sm:flex-row sm:items-end"
            method="get"
          >
            <label className="grid gap-1 text-xs text-muted-foreground">
              From
              <Input
                type="date"
                name="from"
                defaultValue={period.fromInput}
                className="w-full sm:w-36"
              />
            </label>
            <label className="grid gap-1 text-xs text-muted-foreground">
              To
              <Input
                type="date"
                name="to"
                defaultValue={period.toInput}
                className="w-full sm:w-36"
              />
            </label>
            <Button type="submit" size="sm" variant="secondary">
              Apply
            </Button>
          </form>
        </div>
      </section>

      <TestingLabAccessIssues issues={analytics.accessIssues} />

      {!hasActivity && analytics.accessIssues.length === 0 ? (
        <TestingLabEmptyState
          title="No Testing Lab activity in this period"
          description="Choose another period or create an event to begin collecting applications, attendance, and feedback."
          action={
            <Button asChild>
              <Link href="/workspace/testing-lab/events">Manage events</Link>
            </Button>
          }
        />
      ) : (
        <>
          <section
            aria-label="Testing Lab performance"
            className="grid overflow-hidden rounded-md border sm:grid-cols-2 xl:grid-cols-4"
          >
            {buildMetrics(analytics.current, analytics.previous).map(
              (metric) => (
                <Metric key={metric.label} metric={metric} />
              ),
            )}
          </section>

          <section className="grid gap-6 xl:grid-cols-[minmax(0,2fr)_minmax(16rem,1fr)]">
            <div className="min-w-0">
              <div className="mb-3">
                <h2 className="font-semibold">Activity trend</h2>
                <p className="text-sm text-muted-foreground">
                  Daily applications, tester registrations, and submitted
                  feedback.
                </p>
              </div>
              {analytics.trend.length > 0 ? (
                <ActivityTrend trend={analytics.trend} />
              ) : (
                <p className="text-sm text-muted-foreground">
                  No daily activity recorded.
                </p>
              )}
              <div
                className="mt-2 flex flex-wrap gap-4 text-xs text-muted-foreground"
                aria-label="Activity trend legend"
              >
                <span className="flex items-center gap-1.5">
                  <span className="size-2 rounded-sm bg-blue-500" />
                  Applications
                </span>
                <span className="flex items-center gap-1.5">
                  <span className="size-2 rounded-sm bg-violet-500" />
                  Registrations
                </span>
                <span className="flex items-center gap-1.5">
                  <span className="size-2 rounded-sm bg-emerald-500" />
                  Feedback
                </span>
              </div>
            </div>
            <div className="border-t pt-4 xl:border-l xl:border-t-0 xl:pl-6 xl:pt-0">
              <h2 className="font-semibold">Operational health</h2>
              <dl className="mt-4 divide-y text-sm">
                {[
                  [
                    "Capacity fill",
                    analytics.current.capacity > 0
                      ? `${analytics.current.fillRate}%`
                      : analytics.current.registeredTesters > 0
                        ? "Unlimited"
                        : "-",
                  ],
                  [
                    "Recommendation rate",
                    analytics.current.recommendationRate === null
                      ? "-"
                      : `${analytics.current.recommendationRate}%`,
                  ],
                  [
                    "Active locations",
                    `${analytics.locations.active}/${analytics.locations.total}`,
                  ],
                  ["Approved projects", analytics.current.approvedProjects],
                ].map(([label, value]) => (
                  <div
                    key={label}
                    className="flex items-center justify-between gap-4 py-3 first:pt-0"
                  >
                    <dt className="text-muted-foreground">{label}</dt>
                    <dd className="font-medium">{value}</dd>
                  </div>
                ))}
              </dl>
            </div>
          </section>

          <section>
            <div className="mb-3 flex items-end justify-between gap-4">
              <div>
                <h2 className="font-semibold">Event performance</h2>
                <p className="text-sm text-muted-foreground">
                  Open an event to review applications, schedule, testers, and
                  feedback.
                </p>
              </div>
              <span className="text-sm text-muted-foreground">
                {analytics.events.length} events
              </span>
            </div>
            {analytics.events.length > 0 ? (
              <EventRows events={analytics.events} />
            ) : (
              <p className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
                No events started in this period.
              </p>
            )}
          </section>
        </>
      )}
    </div>
  );
}
