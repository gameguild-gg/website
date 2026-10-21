/**
 * Permission-change pattern analytics for the compliance console (issue #297).
 * Pure transformations over the audit query DSL execution result and the security
 * alert queue — no transport here; the route proxy and the console component own IO.
 */

export interface PermissionPatternTimeBucket {
  readonly dateUtc: string;
  readonly count: number;
}

export interface PermissionPatternActorVolume {
  readonly actor: string;
  readonly count: number;
}

export interface PermissionPatternSummary {
  readonly matchedCount: number;
  readonly failureCount: number;
  readonly successCount: number;
  readonly windowStartUtc: string | null;
  readonly windowEndUtc: string | null;
  readonly scanTruncated: boolean;
}

export interface PermissionPatternDataset {
  readonly summary: PermissionPatternSummary;
  readonly dailySeries: readonly PermissionPatternTimeBucket[];
  readonly byAction: readonly PermissionPatternActorVolume[];
  readonly topActors: readonly PermissionPatternActorVolume[];
}

/** Raw shapes accepted from the route proxy; rows are the data-masked audit page. */
export interface PermissionPatternsQueryPayload {
  aggregates?: {
    byAction?: Record<string, number> | null;
    failureCount?: number;
    matchedCount?: number;
    successCount?: number;
    timeBuckets?: readonly { count?: number; dateUtc?: string }[] | null;
  } | null;
  rows?: readonly Record<string, unknown>[] | null;
  scanTruncated?: boolean;
  windowEndUtc?: string;
  windowStartUtc?: string;
}

export interface PermissionPatternsAlertPayload {
  id?: string;
  title?: string | null;
  description?: string | null;
  ruleId?: string | null;
  severity?: string;
  status?: string;
  occurrenceCount?: number;
  firstSeenAtUtc?: string;
  lastSeenAtUtc?: string;
  subjectUserId?: string | null;
  sourceActionType?: string | null;
}

const MAX_DAYS = 366;
const MIN_DAYS = 1;
const DEFAULT_DAYS = 30;
const TOP_ACTOR_LIMIT = 8;

export function normalizePermissionWindowDays(days: number | null | undefined): number {
  if (!Number.isFinite(days) || days === null || days === undefined || days <= 0) return DEFAULT_DAYS;
  return Math.min(MAX_DAYS, Math.max(MIN_DAYS, Math.trunc(days)));
}

/** Light-syntax DSL query: every permission-resource mutation inside the window. */
export function buildPermissionChangeQueryText(days: number, now: Date = new Date()): string {
  const windowDays = normalizePermissionWindowDays(days);
  const after = new Date(now.getTime() - windowDays * 24 * 60 * 60 * 1000);
  const afterDate = after.toISOString().slice(0, 10);
  return `resource:Permission AFTER ${afterDate}`;
}

function readActorKey(row: Record<string, unknown>): string | null {
  const actor = row['actor'] ?? row['Actor'] ?? row['userId'] ?? row['UserId'];
  if (typeof actor === 'string' && actor.trim().length > 0) return actor;
  if (typeof actor === 'number') return String(actor);
  return null;
}

function toSortedVolumes(entries: Iterable<readonly [string, number]>): PermissionPatternActorVolume[] {
  return [...entries]
    .map(([actor, count]) => ({ actor, count }))
    .sort((a, b) => b.count - a.count || a.actor.localeCompare(b.actor));
}

/** Builds the dashboard dataset: summary + daily series + action mix + top actors. */
export function buildPermissionPatternDataset(payload: PermissionPatternsQueryPayload): PermissionPatternDataset {
  const aggregates = payload.aggregates ?? null;
  const dailySeries = (aggregates?.timeBuckets ?? [])
    .filter((bucket): bucket is { count?: number; dateUtc?: string } => Boolean(bucket))
    .map((bucket) => ({
      dateUtc: String(bucket.dateUtc ?? ''),
      count: Number(bucket.count ?? 0),
    }))
    .filter((bucket) => bucket.dateUtc.length > 0);

  const byAction = toSortedVolumes(Object.entries(aggregates?.byAction ?? {})
    .filter(([action]) => action.trim().length > 0));

  const actorCounts = new Map<string, number>();
  for (const row of payload.rows ?? []) {
    const actor = readActorKey(row);
    if (actor === null) continue;
    actorCounts.set(actor, (actorCounts.get(actor) ?? 0) + 1);
  }
  const topActors = toSortedVolumes(actorCounts.entries()).slice(0, TOP_ACTOR_LIMIT);

  const summary: PermissionPatternSummary = {
    matchedCount: Number(aggregates?.matchedCount ?? 0),
    failureCount: Number(aggregates?.failureCount ?? 0),
    successCount: Number(aggregates?.successCount ?? 0),
    windowStartUtc: payload.windowStartUtc ?? null,
    windowEndUtc: payload.windowEndUtc ?? null,
    scanTruncated: payload.scanTruncated === true,
  };

  return { summary, dailySeries, byAction, topActors };
}

/** Permission-rule alerts first (bulk-change and burst rules), then everything else. */
export function orderPermissionAlertsFirst(
  alerts: readonly PermissionPatternsAlertPayload[],
): PermissionPatternsAlertPayload[] {
  const rank = (alert: PermissionPatternsAlertPayload): number =>
    (alert.ruleId ?? '').startsWith('security.permission') ? 0 : 1;
  const severityRank = (alert: PermissionPatternsAlertPayload): number => {
    switch (alert.severity) {
      case 'Critical': return 0;
      case 'High': return 1;
      case 'Medium': return 2;
      default: return 3;
    }
  };
  return [...alerts].sort((a, b) =>
    rank(a) - rank(b) ||
    severityRank(a) - severityRank(b) ||
    (b.occurrenceCount ?? 0) - (a.occurrenceCount ?? 0));
}
