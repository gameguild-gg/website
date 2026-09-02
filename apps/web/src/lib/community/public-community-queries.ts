import type {
  PublicActivity,
  PublicMemberSpotlight,
  PublicPlaytest,
} from '@/lib/community/public-community';
import { getPublishedProjects } from '@/lib/projects/public-projects';
import { getPublicTestingEventsDirectory } from '@/lib/testing-lab/events-public-queries';
import type { TestingLabPublicTestingEventProjection } from '@game-guild/client';

export interface PublicCommunityGroup {
  name: string;
  description: string;
  projectCount: number;
}

const eventDateFormat = new Intl.DateTimeFormat('en-US', {
  weekday: 'long',
  hour: 'numeric',
  minute: '2-digit',
  timeZone: 'UTC',
});

function eventStartTimestamp(event: TestingLabPublicTestingEventProjection): number {
  const timestamps = [
    ...(event.slots ?? []).map((slot) => slot.startsAt),
    event.startsAt,
  ]
    .filter((value): value is string => Boolean(value))
    .map((value) => new Date(value).getTime())
    .filter((timestamp) => !Number.isNaN(timestamp));

  return timestamps.length > 0 ? Math.min(...timestamps) : Number.POSITIVE_INFINITY;
}

function formatEventDate(event: TestingLabPublicTestingEventProjection): string {
  const timestamp = eventStartTimestamp(event);
  if (!Number.isFinite(timestamp)) return 'Schedule pending';
  return `${eventDateFormat.format(new Date(timestamp))} UTC`;
}

function eventSeats(event: TestingLabPublicTestingEventProjection): string {
  const slots = event.slots ?? [];
  if (slots.length === 0 || slots.some((slot) => slot.availableTesterCount == null)) {
    return 'Registration open';
  }
  const available = slots.reduce((total, slot) => total + (slot.availableTesterCount ?? 0), 0);
  return available > 0 ? `${available} open seats` : 'Waiting list';
}

export async function getPublicPlaytests(take = 3): Promise<PublicPlaytest[]> {
  const directory = await getPublicTestingEventsDirectory({ take: 25 });

  return directory.events
    .slice()
    .sort((left, right) => eventStartTimestamp(left) - eventStartTimestamp(right))
    .slice(0, take)
    .map((event) => ({
      title: event.name?.trim() || 'Community playtest',
      date: formatEventDate(event),
      format: event.mode === 'InPerson' ? 'In person session' : 'Online session',
      seats: eventSeats(event),
      href: `/testing-lab/events/${event.id ?? ''}`,
    }));
}

function creatorHandle(name: string): string {
  return `@${name.trim().toLowerCase().replace(/[^a-z0-9]+/g, '')}`;
}

export async function getPublicMemberSpotlights(take = 3): Promise<PublicMemberSpotlight[]> {
  const projects = await getPublishedProjects();
  const seen = new Set<string>();
  const spotlights: PublicMemberSpotlight[] = [];

  for (const project of projects) {
    const creator = project.creator.trim();
    if (!creator || seen.has(creator.toLowerCase())) continue;

    seen.add(creator.toLowerCase());
    spotlights.push({
      id: project.creatorId,
      name: creator,
      handle: creatorHandle(creator),
      role: project.creatorRole,
      focus: project.coursePath,
      contribution: project.summary,
    });

    if (spotlights.length >= take) break;
  }

  return spotlights;
}

export async function getPublicActivities(take = 3): Promise<PublicActivity[]> {
  const projects = await getPublishedProjects();

  return projects.slice(0, take).map((project) => ({
    actor: project.creator,
    action: 'recently updated the project',
    target: project.title,
    href: `/projects/${project.slug}`,
  }));
}

export async function getPublicCommunityGroups(take = 3): Promise<PublicCommunityGroup[]> {
  const projects = await getPublishedProjects();
  const projectCountByCategory = new Map<string, number>();

  for (const project of projects) {
    const category = project.coursePath.trim() || 'Independent projects';
    projectCountByCategory.set(category, (projectCountByCategory.get(category) ?? 0) + 1);
  }

  return [...projectCountByCategory.entries()]
    .sort((left, right) => right[1] - left[1])
    .slice(0, take)
    .map(([name, projectCount]) => ({
      name,
      projectCount,
      description:
        projectCount === 1
          ? 'One published community project and growing.'
          : `${projectCount} published community projects and growing.`,
    }));
}
