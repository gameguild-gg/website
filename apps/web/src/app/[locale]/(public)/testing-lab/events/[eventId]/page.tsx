import { TestingFeedbackSubmission } from '@/components/testing-lab/testing-feedback-submission';
import { TestingEventGames } from '@/components/testing-lab/testing-event-games';
import { TestingEventJoin } from '@/components/testing-lab/testing-event-join';
import { TestingEventDateRange } from '@/components/testing-lab/testing-event-date-range';
import { Link } from '@/i18n/navigation';
import { getPublicTestingEventExperience } from '@/lib/testing-lab/events-queries';
import { getPublicTestingEvent } from '@/lib/testing-lab/events-public-queries';
import { getTestingProjectVersionOptions } from '@/lib/testing-lab/queries';
import { getLocalizationPreference } from '@/lib/user-settings/queries';
import type {
  TestingLabPublicTestingEventSlotProjection,
} from '@game-guild/client';
import {
  ArrowLeft,
  CalendarDays,
  Gamepad2,
  MessageSquare,
  Users,
} from 'lucide-react';
import type { Metadata } from 'next';
import { notFound } from 'next/navigation';

// The public event body can include account-specific participation data.
// Always request a fresh render so one visitor's private state cannot leak to another.
export const dynamic = 'force-dynamic';

// Dynamic metadata for the public playtest page. Uses the public-only event
// lookup (no request auth) so crawlers get the event title/description without
// any account-specific participation state.
export async function generateMetadata({ params }: { params: Promise<{ eventId: string }> }): Promise<Metadata> {
  const { eventId } = await params;
  const event = await getPublicTestingEvent(eventId);

  if (!event) {
    return {
      title: 'Playtest Not Found',
      description: 'The requested playtest could not be found.',
      robots: { index: false, follow: false },
    };
  }

  const title = event.name?.trim() || 'Community playtest';
  const description =
    event.description?.trim().slice(0, 200) ||
    `${title} — join playtests, submit your game, and share feedback on GameGuild.`;

  return {
    title,
    description,
    openGraph: {
      title,
      description,
    },
  };
}

function safeTimeZone(timeZone?: string | null) {
  if (!timeZone) return 'UTC';
  try {
    new Intl.DateTimeFormat('en-US', { timeZone });
    return timeZone;
  } catch {
    return 'UTC';
  }
}

function getTimestamp(value?: string | null) {
  if (!value) return null;
  const timestamp = new Date(value).valueOf();
  return Number.isFinite(timestamp) ? timestamp : null;
}

export default async function Page({
  params,
  searchParams,
}: {
  params: Promise<{ eventId: string }>;
  searchParams?: Promise<{ projectId?: string; submitGame?: string; applicationId?: string; joinAs?: string; slotId?: string }>;
}) {
  const { eventId } = await params;
  const { projectId, submitGame, applicationId, joinAs, slotId } = searchParams ? await searchParams : {};
  const experience = await getPublicTestingEventExperience(eventId);
  if (!experience.event && experience.accessIssues.length === 0) notFound();

  if (!experience.event) {
    const eventNotFound = experience.accessIssues.some((issue) =>
      /\b404\b|PublicEventNotFound/i.test(issue),
    );
    console.error(
      `[testing-lab] public event ${eventId} could not be loaded`,
      experience.accessIssues,
    );
    return (
      <div className="mx-auto w-full max-w-4xl px-4 py-16 sm:px-6">
        <div className="rounded-lg border border-destructive/30 bg-destructive/10 p-6">
          <h1 className="text-2xl font-semibold text-foreground">
            {eventNotFound ? 'Playtest not found' : 'Event temporarily unavailable'}
          </h1>
          <p className="mt-2 text-sm leading-6 text-muted-foreground">
            {eventNotFound
              ? 'We could not find a public playtest at this link. It may have ended or the URL may be incorrect.'
              : 'We could not load this playtest right now. Your place in the community is still here—try again shortly.'}
          </p>
          <Link href="/testing-lab" className="mt-5 inline-flex items-center gap-2 text-sm font-medium text-primary hover:text-primary/80">
            <ArrowLeft className="size-4" aria-hidden="true" /> Browse other playtests
          </Link>
        </div>
      </div>
    );
  }

  const event = experience.event;
  // This route is force-dynamic; registration eligibility must use request-time state.
  // eslint-disable-next-line react-hooks/purity
  const now = Date.now();
  const openingAt = getTimestamp(event.applicationsOpenAt);
  const closingAt = getTimestamp(event.applicationsCloseAt);
  const eventEndsAt = getTimestamp(event.endsAt);
  const eventEnded = event.status === 'Completed' || event.status === 'Cancelled' || (eventEndsAt !== null && eventEndsAt <= now);
  const withinApplicationWindow = (openingAt === null || openingAt <= now) && (closingAt === null || closingAt >= now);
  const acceptsApplications = !eventEnded && event.status === 'ApplicationsOpen' && withinApplicationWindow;
  const slots = event.slots ?? [];
  const testerRegistrationStatus = ['ApplicationsClosed', 'Scheduled', 'Active'].includes(event.status ?? '');
  const testerConfigurationReady = Boolean(
    event.configuration?.frozenAt && event.configuration.testerRegistrationSchema,
  );
  const canRegisterForSlot = (slot: TestingLabPublicTestingEventSlotProjection) => {
    const slotEnd = getTimestamp(slot.endsAt);
    // Full sessions still accept registrations through the API waitlist.
    return !eventEnded && testerRegistrationStatus && testerConfigurationReady && (slotEnd === null || slotEnd > now);
  };
  const testerRegistrationOpen = slots.some(canRegisterForSlot);
  const selectedTesterSlot = slots.find((slot) => slot.id === slotId);
  const requestedApplication = experience.applications.find((application) =>
    (applicationId && application.id === applicationId)
    || (projectId && application.projectId === projectId),
  );
  const initialJoinMode = joinAs === 'developer' && (acceptsApplications || requestedApplication)
    ? 'developer'
    : (submitGame === '1' || Boolean(projectId) || Boolean(applicationId)) && (acceptsApplications || requestedApplication)
      ? 'developer'
    : joinAs === 'tester' && selectedTesterSlot && canRegisterForSlot(selectedTesterSlot)
      ? 'tester'
      : undefined;
  const allTesterSpotsFull = slots.length > 0 && slots.every((slot) => {
    if (slot.maxTesters == null) return false;
    const available = slot.availableTesterCount ?? Math.max(0, slot.maxTesters - (slot.registeredTesterCount ?? 0));
    return available <= 0;
  });
  const games = event.games ?? [];
  const slotCapacity = (field: 'maxProjects' | 'maxTesters') => {
    if (slots.length === 0 || slots.some((slot) => slot[field] == null)) return null;
    return slots.reduce((total, slot) => total + (slot[field] ?? 0), 0);
  };
  const projectCapacity = slotCapacity('maxProjects');
  const testerCapacity = slotCapacity('maxTesters');
  const registeredTesters = slots.reduce((total, slot) => total + (slot.registeredTesterCount ?? 0), 0);
  const projectCapacityLabel = projectCapacity === null ? `${games.length} / No limit` : `${games.length} of ${projectCapacity}`;
  const testerCapacityLabel = testerCapacity === null ? `${registeredTesters} / No limit` : `${registeredTesters} of ${testerCapacity}`;
  const testerCapacityStateLabel = testerRegistrationOpen
    ? allTesterSpotsFull ? `${testerCapacityLabel} · Waitlist open` : testerCapacityLabel
    : testerCapacity === null
      ? 'Closed'
      : `Closed · ${registeredTesters}/${testerCapacity} used`;
  const testerRegistrationClosedCopy = event.status === 'Cancelled'
    ? 'This playtest was cancelled. Tester sign-up is closed.'
    : eventEnded
      ? 'This playtest has ended. Tester sign-up is closed.'
      : acceptsApplications
          ? 'Tester sign-up is not open. Game submissions are open.'
          : !testerRegistrationStatus
            ? 'Tester sign-up is not open for this playtest.'
          : !testerConfigurationReady
            ? 'Tester sign-up is not available for this playtest yet.'
            : allTesterSpotsFull
              ? 'All tester spots are filled. You can still join the waitlist.'
            : 'Tester sign-up is closed for this playtest.';
  const showGameSubmission = acceptsApplications || submitGame === '1' || Boolean(projectId) || Boolean(applicationId);
  const modeLabel = event.mode === 'InPerson' ? 'In person' : (event.mode ?? 'Online');
  const [projectVersions, localization] = await Promise.all([
    experience.isAuthenticated && showGameSubmission ? getTestingProjectVersionOptions() : Promise.resolve([]),
    experience.isAuthenticated ? getLocalizationPreference().catch(() => null) : Promise.resolve(null),
  ]);
  const applicationSummaries = experience.applications.map((application, index) => ({
    id: application.id ?? application.projectId ?? `application-${index}`,
    projectId: application.projectId,
    title: games.find((game) => game.projectId === application.projectId)?.title?.trim()
      || projectVersions.find((version) => version.projectId === application.projectId)?.projectTitle.trim()
      || `Game submission ${index + 1}`,
    status: application.status?.trim() || 'Under review',
  }));
  const projectApplications = experience.applications.flatMap((application) => application.id ? [{
    id: application.id,
    projectId: application.projectId,
    projectVersionId: application.projectVersionId,
    preferredAvailability: application.preferredAvailability,
    status: application.status,
    decisionRationale: application.decisionRationale,
    brief: application.brief,
    eventApplicationResponse: application.eventApplicationResponse,
    feedbackQuestionnaire: application.feedbackQuestionnaire,
    rulesAcceptedAt: application.rulesAcceptedAt,
    submittedAssetReferenceIds: application.submittedAssetReferenceIds,
  }] : []);
  const timeZone = safeTimeZone(localization?.timezone ?? event.timeZoneId);
  const dateLocale = localization?.language ?? 'en-US';
  const hour12 = localization?.timeFormat !== '24h';

  return (
    <div className="grid w-full grid-cols-1 lg:grid-cols-[19rem_minmax(0,1fr)]">
      <aside
        id="event-info"
        aria-label="Playtest details"
        className="order-2 min-w-0 space-y-4 border-y border-border bg-muted/20 px-4 pb-5 pt-5 sm:px-6 lg:order-none lg:sticky lg:top-0 lg:col-start-1 lg:row-start-1 lg:h-[calc(100svh-4rem)] lg:self-start lg:overflow-y-auto lg:border-y-0 lg:border-r lg:px-5 lg:pb-5 lg:pt-5"
      >
        <div>
          <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Testing Lab</p>
          <header className="mt-2 space-y-3">
            <h1 id="event-title" className="text-xl font-semibold tracking-tight">
              {event.name}
            </h1>
            {event.description?.trim() ? (
              <p className="text-sm leading-6 text-muted-foreground">
                {event.description}
              </p>
            ) : null}
          </header>
        </div>

        <section id="schedule" aria-labelledby="schedule-heading" className="scroll-mt-8 border-t border-border pt-4">
          <h2 id="schedule-heading" className="text-sm font-semibold">Schedule</h2>
          {slots.length === 0 && event.startsAt ? (
            <div className="mt-3 flex items-start gap-3">
              <CalendarDays className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
              <div className="min-w-0">
                <p className="text-sm font-medium">{modeLabel} playtest</p>
                <div className="mt-1"><TestingEventDateRange startsAt={event.startsAt} endsAt={event.endsAt} timeZone={timeZone} locale={dateLocale} hour12={hour12} /></div>
              </div>
            </div>
          ) : null}
          {slots.length > 0 ? (
            <div className="mt-2 divide-y divide-border">
              {slots.map((slot: TestingLabPublicTestingEventSlotProjection) => (
                <div key={slot.id} id={slot.id ? `session-${slot.id}` : undefined} className="scroll-mt-24 py-3 first:pt-2 last:pb-0">
                  <div className="flex items-start gap-3">
                    <CalendarDays className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
                    <div className="min-w-0 flex-1">
                      <p className="text-sm font-medium">{slot.mode === 'InPerson' ? 'In person' : (slot.mode ?? 'Online')} playtest</p>
                      <div className="mt-1"><TestingEventDateRange startsAt={slot.startsAt} endsAt={slot.endsAt} timeZone={timeZone} locale={dateLocale} hour12={hour12} /></div>
                      {[slot.campusName, slot.roomName].filter(Boolean).length > 0 ? (
                        <p className="mt-1 flex items-center gap-2 text-xs text-muted-foreground">
                          <span aria-hidden="true">·</span>{[slot.campusName, slot.roomName].filter(Boolean).join(' · ')}
                        </p>
                      ) : null}
                      {experience.registrations.find((registration) => registration.slotId === slot.id && registration.status !== 'Cancelled')?.status ? (
                        <p className="mt-1 text-xs text-muted-foreground">Your status: {experience.registrations.find((registration) => registration.slotId === slot.id && registration.status !== 'Cancelled')?.status}</p>
                      ) : null}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          ) : null}
        </section>

        <section className="border-t border-border pt-5" aria-labelledby="capacity-heading">
          <h2 id="capacity-heading" className="text-sm font-semibold">Spots &amp; feedback</h2>
          <dl className="mt-2 space-y-1 text-sm">
            <div className="flex items-center justify-between gap-3 py-1">
              <dt className="flex items-center gap-2 text-muted-foreground">
                <Gamepad2 className="size-4" aria-hidden="true" />
                Games
              </dt>
              <dd className="whitespace-nowrap text-right font-semibold tabular-nums" aria-label={`${games.length} ${games.length === 1 ? 'game' : 'games'} of ${projectCapacity ?? 'unlimited'} game spots`}>{projectCapacityLabel}</dd>
            </div>
            <div className="flex items-center justify-between gap-3 py-1">
              <dt className="flex items-center gap-2 text-muted-foreground">
                <Users className="size-4" aria-hidden="true" />
                Tester spots
              </dt>
              <dd className="whitespace-nowrap text-right font-semibold tabular-nums" aria-label={testerRegistrationOpen
                ? `${registeredTesters} ${registeredTesters === 1 ? 'tester' : 'testers'} of ${testerCapacity ?? 'unlimited'} tester spots`
                : `Tester sign-up closed; ${registeredTesters} ${registeredTesters === 1 ? 'tester' : 'testers'} of ${testerCapacity ?? 'unlimited'} spots used`}>{testerCapacityStateLabel}</dd>
            </div>
            {event.requiresFeedback ? (
              <div className="flex items-center justify-between gap-3 py-1">
                <dt className="flex items-center gap-2 text-muted-foreground">
                  <MessageSquare className="size-4" aria-hidden="true" />
                  Feedback
                </dt>
                <dd className="whitespace-nowrap text-right font-semibold">Required</dd>
              </div>
            ) : null}
          </dl>
        </section>

        <section className="border-t border-border pt-5">
          <h3 className="text-sm font-semibold">Rules for testers</h3>
          {event.configuration?.generalRules?.trim() || event.configuration?.testerInstructions?.trim() ? (
            <div className="mt-3 space-y-3 text-sm leading-6 text-muted-foreground">
              {event.configuration?.generalRules?.trim() ? (
                <p className="whitespace-pre-wrap">{event.configuration.generalRules}</p>
              ) : null}
              {event.configuration?.testerInstructions?.trim() ? (
                <p className="whitespace-pre-wrap">{event.configuration.testerInstructions}</p>
              ) : null}
            </div>
          ) : (
            <p className="mt-2 text-sm leading-6 text-muted-foreground">No tester rules have been posted yet.</p>
          )}
        </section>

        {experience.accessIssues.length > 0 ? (
          <p className="border-t border-border pt-4 text-sm text-destructive" role="status">
            Some account-specific participation details could not be loaded. The public event information is still available.
          </p>
        ) : null}

        {experience.feedbackObligations.length > 0 ? (
          <section className="border-t border-border pt-5" aria-labelledby="feedback-heading">
            <h2 id="feedback-heading" className="text-sm font-semibold">Your feedback</h2>
            <div className="mt-2">
              <TestingFeedbackSubmission
                eventId={eventId}
                isAuthenticated={experience.isAuthenticated}
                obligations={experience.feedbackObligations}
              />
            </div>
          </section>
        ) : null}

        <TestingEventJoin
            eventId={eventId}
            isAuthenticated={experience.isAuthenticated}
            testerSessions={slots.flatMap((slot) => slot.id ? [{
              id: slot.id,
              label: `${slot.mode === 'InPerson' ? 'In person' : (slot.mode ?? 'Online')} · ${getTimestamp(slot.startsAt) === null
                ? 'Playtest session'
                : new Intl.DateTimeFormat(dateLocale, { dateStyle: 'medium', timeStyle: 'short', timeZone, hour12 }).format(new Date(slot.startsAt!))}`,
              slot: {
                ...slot,
                availableTesterCount: slot.maxTesters == null
                  ? null
                  : slot.availableTesterCount ?? Math.max(0, slot.maxTesters - (slot.registeredTesterCount ?? 0)),
              },
              registration: experience.registrations.find((registration) => registration.slotId === slot.id),
              registrationOpen: canRegisterForSlot(slot),
            }] : [])}
            testerUnavailableReason={testerRegistrationClosedCopy}
            testerRegistrationSchema={event.configuration?.testerRegistrationSchema}
            generalRules={event.configuration?.generalRules}
            testerInstructions={event.configuration?.testerInstructions}
            timeZoneId={timeZone}
            locale={dateLocale}
            hour12={hour12}
            projectApplication={{
              acceptsApplications,
              projectVersions,
              applications: projectApplications,
              applicationSchema: event.configuration?.projectApplicationSchema,
              generalRules: event.configuration?.generalRules,
              candidateInstructions: event.configuration?.candidateInstructions,
              requiresFeedback: event.requiresFeedback ?? false,
            }}
            initialMode={initialJoinMode}
            initialSlotId={initialJoinMode === 'tester' ? selectedTesterSlot?.id : undefined}
            initialProjectId={projectId}
            selectedApplicationId={applicationId}
          />
      </aside>

      <TestingEventGames
        games={games}
        eventId={eventId}
        applications={applicationSummaries}
        emptyState={
          <div className="max-w-md">
            <p className="font-medium">
              {eventEnded ? 'No games were published for this playtest.' : 'No games have been announced yet.'}
            </p>
            <p className="mt-2 text-sm leading-6 text-muted-foreground">
              {acceptsApplications
                ? 'Bring a project to the lab and give testers something new to play.'
                : 'Check back when the playtest lineup is published.'}
            </p>
          </div>
        }
      />
    </div>
  );
}
