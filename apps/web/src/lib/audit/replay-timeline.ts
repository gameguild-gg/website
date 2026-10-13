/** Pure replay state for the existing AuditLogDto projection. Server authorization,
 * durable replay sessions, persisted annotations and the UI remain separate seams. */
export type ReplayEventInput = Readonly<{
  id: string
  tenantId: string | null
  createdAt: string
  actionType: string
  resourceType: string
  resourceId?: string | null
  userId?: string | null
  sessionId?: string | null
  correlationId?: string | null
  success: boolean
}>
export type ReplayEvent = Readonly<{
  id: string
  tenantId: string | null
  createdAt: string
  timestampNs: bigint
  actionType: string
  resourceType: string
  resourceId: string | null
  userId: string | null
  sessionId: string | null
  correlationId: string | null
  success: boolean
}>
export type ReplayScope = Readonly<{ tenantId: string | null; totalCount: number }>
export type ReplayFilter = Readonly<{
  userIds?: readonly string[]
  sessionIds?: readonly string[]
  resourceIds?: readonly string[]
  resourceTypes?: readonly string[]
  actions?: readonly string[]
  start?: string
  end?: string
}>
export type ReplayBookmark = Readonly<{ id: string; eventId: string; note: string }>
export type ReplaySequenceStep = Readonly<{
  actions: readonly string[]
  success?: boolean
  minimumOccurrences?: number
}>
export type ReplaySequenceRule = Readonly<{
  id: string
  label: string
  groupBy: 'user' | 'session' | 'correlation' | 'resource'
  windowMs: number
  steps: readonly ReplaySequenceStep[]
}>
/** A configured sequence match is an investigation signal, not a confirmed attack.
 * eventIds identify the exact latest-start sequence ending at this event. */
export type ReplaySequenceFinding = Readonly<{
  key: string
  ruleId: string
  ruleLabel: string
  ruleDefinition: string
  groupBy: ReplaySequenceRule['groupBy']
  groupKey: string
  startedAt: string
  endedAt: string
  elapsedNs: string
  eventIds: readonly string[]
}>
export const MAX_REPLAY_SEQUENCE_RULES = 32
export const MAX_REPLAY_SEQUENCE_LENGTH = 64
export const MAX_REPLAY_SEQUENCE_FINDINGS = 1000

export const MAX_REPLAY_EVENTS = 10_000
export const MAX_REPLAY_BOOKMARKS = 1000
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
const EMPTY_GUID = '00000000-0000-0000-0000-000000000000'

function requiredItem<T>(items: readonly T[], index: number): T {
  const item = items.at(index)
  if (item === undefined) throw new RangeError('Replay collection index is outside its bounds.')
  return item
}

function guid(value: string, label: string): string {
  if (typeof value !== 'string' || !GUID.test(value) || value.toLowerCase() === EMPTY_GUID) {
    throw new TypeError(label + ' must be a non-empty GUID.')
  }
  return value.toLowerCase()
}
function optionalGuid(value: string | null | undefined, label: string): string | null {
  return value == null ? null : guid(value, label)
}
function text(value: string, label: string, maximum: number, allowEmpty = false): string {
  if (typeof value !== 'string' || value.length > maximum || (!allowEmpty && !value.trim())) {
    throw new TypeError(label + ' has an invalid length.')
  }
  return value
}
function optionalText(value: string | null | undefined, label: string, maximum: number): string | null {
  return value == null ? null : text(value, label, maximum)
}

/** ISO-8601 timestamps with explicit offsets and up to .NET's 100 ns precision.
 * Rejects normalized-invalid dates instead of accepting Date's calendar rollover. */
export function parseReplayTimestamp(value: string): bigint {
  if (typeof value !== 'string') throw new TypeError('Timestamp must be a string.')
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?(Z|[+-]\d{2}:\d{2})$/.exec(value)
  if (!match) throw new TypeError('Timestamp requires an explicit ISO-8601 timezone and at most seven fraction digits.')
  const year = Number(requiredItem(match, 1)), month = Number(requiredItem(match, 2)), day = Number(requiredItem(match, 3))
  const hour = Number(requiredItem(match, 4)), minute = Number(requiredItem(match, 5)), second = Number(requiredItem(match, 6))
  if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1 ||
      hour > 23 || minute > 59 || second > 59) {
    throw new RangeError('Timestamp contains an invalid calendar value.')
  }
  const local = new Date(0)
  local.setUTCFullYear(year, month - 1, day)
  local.setUTCHours(hour, minute, second, 0)
  if (local.getUTCFullYear() !== year || local.getUTCMonth() !== month - 1 ||
      local.getUTCDate() !== day || local.getUTCHours() !== hour ||
      local.getUTCMinutes() !== minute || local.getUTCSeconds() !== second) {
    throw new RangeError('Timestamp contains an invalid calendar date.')
  }
  const offset = requiredItem(match, 8)
  let offsetMinutes = 0
  if (offset !== 'Z') {
    const hours = Number(offset.slice(1, 3)), minutes = Number(offset.slice(4, 6))
    if (hours > 14 || minutes > 59 || (hours === 14 && minutes !== 0)) {
      throw new RangeError('Timestamp offset exceeds the supported .NET range.')
    }
    offsetMinutes = (hours * 60 + minutes) * (offset[0] === '+' ? 1 : -1)
  }
  const epochMilliseconds = local.getTime() - offsetMinutes * 60_000
  const utcYear = new Date(epochMilliseconds).getUTCFullYear()
  if (utcYear < 1 || utcYear > 9999) throw new RangeError('UTC timestamp is outside the supported calendar.')
  return BigInt(epochMilliseconds) * 1_000_000n + BigInt((match[7] ?? '').padEnd(7, '0')) * 100n
}

function normalize(event: ReplayEventInput, tenantId: string | null): ReplayEvent {
  if (event.tenantId === undefined) throw new TypeError('Event tenant must be explicit, including null for global events.')
  const eventTenant = optionalGuid(event.tenantId, 'Event tenant')
  if (eventTenant !== tenantId) throw new RangeError('Every replay event must match the selected tenant.')
  if (typeof event.success !== 'boolean') throw new TypeError('Event success must be a boolean.')
  return Object.freeze({
    id: guid(event.id, 'Event ID'), tenantId: eventTenant, createdAt: event.createdAt,
    timestampNs: parseReplayTimestamp(event.createdAt),
    actionType: text(event.actionType, 'Action type', 100),
    resourceType: text(event.resourceType, 'Resource type', 100),
    resourceId: optionalText(event.resourceId, 'Resource ID', 500),
    userId: optionalGuid(event.userId, 'User ID'),
    sessionId: optionalGuid(event.sessionId, 'Session ID'),
    correlationId: optionalText(event.correlationId, 'Correlation ID', 100),
    success: event.success,
  })
}
const compareEvents = (left: ReplayEvent, right: ReplayEvent): number =>
  left.timestampNs < right.timestampNs ? -1 : left.timestampNs > right.timestampNs ? 1 :
    left.id < right.id ? -1 : left.id > right.id ? 1 : 0


type SequenceToken = Readonly<{ actions: ReadonlySet<string>; success: boolean | undefined }>
type SequenceChain = Readonly<{ event: ReplayEvent; previous: SequenceChain | null; startedAtNs: bigint }>

function sequenceRules(rules: readonly ReplaySequenceRule[]) {
  if (!Array.isArray(rules) || rules.length > MAX_REPLAY_SEQUENCE_RULES) {
    throw new RangeError('Sequence analysis accepts at most 32 rules.')
  }
  const seen = new Set<string>()
  return Array.from(rules, (rule: ReplaySequenceRule) => {
    const id = text(rule.id, 'Rule ID', 100).trim(), label = text(rule.label, 'Rule label', 200).trim()
    if (seen.has(id)) throw new RangeError('Sequence rule IDs must be unique.')
    seen.add(id)
    if (!['user', 'session', 'correlation', 'resource'].includes(rule.groupBy)) {
      throw new TypeError('Sequence grouping must identify a user, session, correlation or typed resource.')
    }
    if (!Number.isSafeInteger(rule.windowMs) || rule.windowMs < 1 || rule.windowMs > 86_400_000) {
      throw new RangeError('Sequence window must be a whole number of milliseconds between 1 and 86400000.')
    }
    if (!Array.isArray(rule.steps) || rule.steps.length < 1 || rule.steps.length > 16) {
      throw new RangeError('Sequence analysis requires between 1 and 16 steps.')
    }
    const tokens: SequenceToken[] = []
    const steps = Array.from(rule.steps, (step: ReplaySequenceStep) => {
      if (!Array.isArray(step.actions) || step.actions.length < 1 || step.actions.length > 50) {
        throw new RangeError('Sequence step requires between 1 and 50 exact action names.')
      }
      const actions = [...new Set(Array.from(step.actions, (action: string) => text(action, 'Step action', 100)))]
      if (step.success !== undefined && typeof step.success !== 'boolean') {
        throw new TypeError('Sequence success selector must be boolean or omitted.')
      }
      const occurrences = step.minimumOccurrences === undefined ? 1 : step.minimumOccurrences
      if (!Number.isSafeInteger(occurrences) || occurrences < 1 ||
          occurrences > MAX_REPLAY_SEQUENCE_LENGTH || tokens.length + occurrences > MAX_REPLAY_SEQUENCE_LENGTH) {
        throw new RangeError('Expanded sequence must contain between 1 and 64 distinct events.')
      }
      const token = { actions: new Set(actions), success: step.success }
      for (let count = 0; count < occurrences; count++) tokens.push(token)
      return { actions, success: step.success, minimumOccurrences: occurrences }
    })
    return { id, label, groupBy: rule.groupBy, windowNs: BigInt(rule.windowMs) * 1_000_000n, tokens,
      definition: JSON.stringify({ id, label, groupBy: rule.groupBy, windowMs: rule.windowMs, steps }) }
  })
}

function sequenceGroup(event: ReplayEvent, groupBy: ReplaySequenceRule['groupBy']): string | null {
  switch (groupBy) {
    case 'user': return event.userId
    case 'session': return event.sessionId
    case 'correlation': return event.correlationId
    case 'resource': return event.resourceId === null ? null : JSON.stringify([event.resourceType, event.resourceId])
  }
}

/** Dynamic prefixes retain the latest start for each sequence length. Descending
 * updates ensure an event is never reused in two steps; the latest start is the
 * best possible candidate for an inclusive window. Unrelated events may interleave.
 * Work is bounded by rules x events x 64, with no regular expressions or timers. */
function analyzeReplaySequences(events: readonly ReplayEvent[], rules: readonly ReplaySequenceRule[]): readonly ReplaySequenceFinding[] {
  const validated = sequenceRules(rules), findings: { end: bigint; finding: ReplaySequenceFinding }[] = []
  for (const rule of validated) {
    const groups = new Map<string, (SequenceChain | undefined)[]>()
    for (const event of events) {
      const groupKey = sequenceGroup(event, rule.groupBy)
      if (groupKey === null) continue
      let prefixes = groups.get(groupKey)
      if (!prefixes) { prefixes = []; groups.set(groupKey, prefixes) }
      const cutoff = event.timestampNs - rule.windowNs
      for (let index = rule.tokens.length - 1; index >= 0; index--) {
        const prefix = prefixes[index]
        if (prefix && prefix.startedAtNs < cutoff) prefixes[index] = undefined
        const token = requiredItem(rule.tokens, index)
        if (!token.actions.has(event.actionType) || (token.success !== undefined && event.success !== token.success)) continue
        const previous = index === 0 ? null : prefixes[index - 1]
        if (previous === undefined || (previous !== null && previous.startedAtNs < cutoff)) continue
        const chain: SequenceChain = { event, previous, startedAtNs: previous?.startedAtNs ?? event.timestampNs }
        const existing = prefixes[index]
        if (!existing || chain.startedAtNs >= existing.startedAtNs) prefixes[index] = chain
        if (index !== rule.tokens.length - 1) continue
        if (findings.length >= MAX_REPLAY_SEQUENCE_FINDINGS) {
          throw new RangeError('Sequence analysis exceeds 1000 findings; narrow the replay selection or rules. No partial result is returned.')
        }
        const evidence: ReplayEvent[] = []
        for (let current: SequenceChain | null = chain; current !== null; current = current.previous) evidence.push(current.event)
        evidence.reverse()
        const finding = Object.freeze({ key: JSON.stringify([rule.id, groupKey, event.id]), ruleId: rule.id,
          ruleLabel: rule.label, ruleDefinition: rule.definition, groupBy: rule.groupBy, groupKey,
          startedAt: requiredItem(evidence, 0).createdAt, endedAt: event.createdAt,
          elapsedNs: (event.timestampNs - chain.startedAtNs).toString(), eventIds: Object.freeze(evidence.map(item => item.id)) })
        findings.push({ end: event.timestampNs, finding })
      }
    }
  }
  findings.sort((a, b) => a.end < b.end ? -1 : a.end > b.end ? 1 :
    a.finding.key < b.finding.key ? -1 : a.finding.key > b.finding.key ? 1 : 0)
  return Object.freeze(findings.map(item => item.finding))
}

/** Holds a validated complete supplied dataset. This is not a server access check or
 * proof that the caller's declared total count represents an immutable DB snapshot. */
export class AuditReplayTimeline {
  readonly tenantId: string | null
  readonly sourceCount: number
  private readonly source: readonly ReplayEvent[]
  private readonly bookmarkMap = new Map<string, ReplayBookmark>()
  private selected: readonly ReplayEvent[]
  private viewRevision = 0

  constructor(events: readonly ReplayEventInput[], scope: ReplayScope) {
    if (scope.tenantId === undefined) throw new TypeError('Replay tenant must be explicit, including null for a global view.')
    this.tenantId = optionalGuid(scope.tenantId, 'Replay tenant')
    if (!Number.isSafeInteger(scope.totalCount) || scope.totalCount < 0 ||
        scope.totalCount > MAX_REPLAY_EVENTS || events.length !== scope.totalCount) {
      throw new RangeError('Replay requires the complete supplied result set, limited to 10000 events.')
    }
    this.sourceCount = events.length
    const normalized = Array.from(events, (event: ReplayEventInput) => normalize(event, this.tenantId))
    if (new Set(normalized.map(event => event.id)).size !== normalized.length) {
      throw new RangeError('Duplicate replay event IDs are not allowed.')
    }
    this.source = Object.freeze(normalized.sort(compareEvents))
    this.selected = this.source
  }
  get events(): readonly ReplayEvent[] { return this.selected }
  get revision(): number { return this.viewRevision }
  get durationNs(): bigint {
    return this.selected.length < 2 ? 0n :
      requiredItem(this.selected, this.selected.length - 1).timestampNs - requiredItem(this.selected, 0).timestampNs
  }
  private event(id: string): ReplayEvent {
    const key = guid(id, 'Event ID')
    const found = this.selected.find(event => event.id === key)
    if (!found) throw new RangeError('Event is not in the current replay view.')
    return found
  }
  positionOf(id: string): bigint {
    return this.event(id).timestampNs - requiredItem(this.selected, 0).timestampNs
  }
  /** Inclusive range, matching the existing date-range audit API semantics. An
   * explicitly empty selection matches no events; an omitted selection matches all. */
  setFilter(filter: ReplayFilter): void {
    const start = filter.start === undefined ? null : parseReplayTimestamp(filter.start)
    const end = filter.end === undefined ? null : parseReplayTimestamp(filter.end)
    if (start !== null && end !== null && start > end) throw new RangeError('Replay start must not follow its end.')
    const makeSet = (values: readonly string[] | undefined, label: string, ids: boolean, maximum: number) => {
      if (values === undefined) return null
      if (!Array.isArray(values) || values.length > MAX_REPLAY_EVENTS) throw new RangeError('Replay filter selection must contain at most 10000 values.')
      return new Set(Array.from(values, (value: string) => ids ? guid(value, label) : text(value, label, maximum)))
    }
    const users = makeSet(filter.userIds, 'User ID', true, 36)
    const sessions = makeSet(filter.sessionIds, 'Session ID', true, 36)
    const resources = makeSet(filter.resourceIds, 'Resource ID', false, 500)
    const types = makeSet(filter.resourceTypes, 'Resource type', false, 100)
    const actions = makeSet(filter.actions, 'Action type', false, 100)
    this.selected = Object.freeze(this.source.filter(event =>
      (start === null || event.timestampNs >= start) && (end === null || event.timestampNs <= end) &&
      (users === null || (event.userId !== null && users.has(event.userId))) &&
      (sessions === null || (event.sessionId !== null && sessions.has(event.sessionId))) &&
      (resources === null || (event.resourceId !== null && resources.has(event.resourceId))) &&
      (types === null || types.has(event.resourceType)) && (actions === null || actions.has(event.actionType))))
    this.viewRevision++
  }
  resetFilter(): void { this.selected = this.source; this.viewRevision++ }
  frameAt(positionNs: bigint): readonly ReplayEvent[] {
    this.validatePosition(positionNs)
    if (!this.selected.length) return Object.freeze([])
    const deadline = requiredItem(this.selected, 0).timestampNs + positionNs
    let low = 0, high = this.selected.length
    while (low < high) {
      const middle = (low + high) >>> 1
      if (requiredItem(this.selected, middle).timestampNs <= deadline) low = middle + 1
      else high = middle
    }
    return Object.freeze(this.selected.slice(0, low))
  }
  validatePosition(positionNs: bigint): void {
    if (typeof positionNs !== 'bigint' || positionNs < 0n || positionNs > this.durationNs) {
      throw new RangeError('Replay position must be within the selected timeline.')
    }
  }
  viewport(startNs: bigint, endNs: bigint): readonly ReplayEvent[] {
    this.validatePosition(startNs); this.validatePosition(endNs)
    if (startNs > endNs) throw new RangeError('Viewport start must not follow its end.')
    if (!this.selected.length) return Object.freeze([])
    const first = requiredItem(this.selected, 0).timestampNs
    return Object.freeze(this.selected.filter(event => event.timestampNs >= first + startNs && event.timestampNs <= first + endNs))
  }
  journeys(): readonly Readonly<{ userId: string | null; sessionId: string | null; events: readonly ReplayEvent[] }>[] {
    const grouped = new Map<string, ReplayEvent[]>()
    for (const event of this.selected) {
      const key = JSON.stringify([event.userId, event.sessionId])
      if (!grouped.has(key)) grouped.set(key, [])
      grouped.get(key)!.push(event)
    }
    return Object.freeze([...grouped.values()].map(events => Object.freeze({
      userId: requiredItem(events, 0).userId, sessionId: requiredItem(events, 0).sessionId, events: Object.freeze(events),
    })))
  }
  relatedEvents(id: string): readonly ReplayEvent[] {
    const selected = this.event(id)
    return Object.freeze(this.selected.filter(event =>
      event.id === selected.id ||
      (selected.correlationId !== null && event.correlationId === selected.correlationId) ||
      (selected.sessionId !== null && event.sessionId === selected.sessionId) ||
      (selected.resourceId !== null && event.resourceType === selected.resourceType && event.resourceId === selected.resourceId)))
  }
  /** Applies explicit investigator rules only to the current selected view. Null
   * grouping identifiers never correlate unrelated events. A filtered signal
   * cannot prove attack completeness, identity attribution or source integrity. */
  sequenceFindings(rules: readonly ReplaySequenceRule[]): readonly ReplaySequenceFinding[] {
    return analyzeReplaySequences(this.selected, rules)
  }
  addBookmark(id: string, eventId: string, note = ''): ReplayBookmark {
    const key = guid(id, 'Bookmark ID'), selected = this.event(eventId)
    if (this.bookmarkMap.has(key)) throw new RangeError('Bookmark ID already exists.')
    if (this.bookmarkMap.size >= MAX_REPLAY_BOOKMARKS) throw new RangeError('Replay accepts at most 1000 bookmarks.')
    const bookmark = Object.freeze({ id: key, eventId: selected.id, note: text(note, 'Note', 2000, true) })
    this.bookmarkMap.set(key, bookmark)
    return bookmark
  }
  updateBookmark(id: string, note: string): ReplayBookmark {
    const key = guid(id, 'Bookmark ID'), previous = this.bookmarkMap.get(key)
    if (!previous) throw new RangeError('Bookmark does not exist.')
    const bookmark = Object.freeze({ ...previous, note: text(note, 'Note', 2000, true) })
    this.bookmarkMap.set(key, bookmark)
    return bookmark
  }
  removeBookmark(id: string): boolean { return this.bookmarkMap.delete(guid(id, 'Bookmark ID')) }
  bookmarks(): readonly ReplayBookmark[] {
    const positions = new Map(this.selected.map((event, index) => [event.id, index]))
    return Object.freeze([...this.bookmarkMap.values()].filter(bookmark => positions.has(bookmark.eventId)).sort((a, b) =>
      positions.get(a.eventId)! - positions.get(b.eventId)! || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0)))
  }
  exportReport(): string {
    return JSON.stringify({
      schemaVersion: 1, tenantId: this.tenantId, sourceCount: this.sourceCount, selectedCount: this.selected.length,
      events: this.selected.map(({ timestampNs, ...event }) => ({ ...event, timestampNs: timestampNs.toString() })),
      journeys: this.journeys().map(journey => ({ userId: journey.userId, sessionId: journey.sessionId, eventIds: journey.events.map(event => event.id) })),
      bookmarks: this.bookmarks(),
    }, null, 2)
  }
}

/** Clock-driven playback. The adapter owns animation frames/timers. A changed
 * filter resets playback automatically; no browser or timer is started here. */
export class AuditReplayPlayback {
  private position = 0n
  private anchorPosition = 0n
  private anchorClock = 0
  private lastClock: number | null = null
  private running = false
  private playbackSpeed = 1
  private focus: string | null
  private seenRevision: number
  private readonly timeline: AuditReplayTimeline
  private readonly clock: () => number
  constructor(timeline: AuditReplayTimeline, clock: () => number) {
    this.timeline = timeline; this.clock = clock
    this.focus = timeline.events[0]?.id ?? null
    this.seenRevision = timeline.revision
  }
  private synchronize(): void {
    if (this.seenRevision !== this.timeline.revision) this.reset()
  }
  private now(): number {
    const value = this.clock()
    if (!Number.isFinite(value) || value < 0 || (this.lastClock !== null && value < this.lastClock)) {
      throw new RangeError('Playback clock must be finite, nonnegative and monotonic.')
    }
    this.lastClock = value
    return value
  }
  private advance(): number | null {
    this.synchronize()
    if (!this.running) return null
    const at = this.now(), elapsed = at - this.anchorClock
    const nanoseconds = Math.round(elapsed * 1_000_000 * this.playbackSpeed)
    if (!Number.isSafeInteger(nanoseconds)) throw new RangeError('Playback clock exceeded its exact range.')
    this.position = this.anchorPosition + BigInt(nanoseconds)
    if (this.position >= this.timeline.durationNs) { this.position = this.timeline.durationNs; this.running = false }
    this.focus = this.timeline.frameAt(this.position).at(-1)?.id ?? null
    return at
  }
  play(): void {
    const at = this.advance()
    if (this.running || !this.timeline.events.length || this.timeline.durationNs === 0n) return
    if (this.position === this.timeline.durationNs) this.reset()
    const anchor = at ?? this.now()
    this.anchorPosition = this.position; this.anchorClock = anchor; this.running = true
  }
  pause(): void { this.advance(); this.running = false }
  setSpeed(speed: number): void {
    if (!Number.isFinite(speed) || speed < 0.25 || speed > 64) throw new RangeError('Playback speed must be between 0.25 and 64.')
    const at = this.advance()
    this.playbackSpeed = speed
    if (this.running) { this.anchorPosition = this.position; this.anchorClock = at! }
  }
  seek(positionNs: bigint): void {
    this.synchronize()
    this.timeline.validatePosition(positionNs)
    const at = this.running ? this.now() : null
    this.position = positionNs
    this.focus = this.timeline.frameAt(positionNs).at(-1)?.id ?? null
    if (this.running) {
      this.anchorPosition = positionNs; this.anchorClock = at!
      if (positionNs === this.timeline.durationNs) this.running = false
    }
  }
  seekEvent(id: string): void {
    this.seek(this.timeline.positionOf(id)); this.focus = guid(id, 'Event ID')
  }
  step(direction: -1 | 1): void {
    if (direction !== -1 && direction !== 1) throw new RangeError('Step direction must be -1 or 1.')
    this.pause()
    if (!this.timeline.events.length) return
    const index = this.focus === null ? -1 : this.timeline.events.findIndex(event => event.id === this.focus)
    const next = Math.max(0, Math.min(this.timeline.events.length - 1, index + direction))
    this.seekEvent(requiredItem(this.timeline.events, next).id)
  }
  reset(): void {
    this.position = 0n; this.anchorPosition = 0n; this.running = false
    this.focus = this.timeline.events[0]?.id ?? null
    this.seenRevision = this.timeline.revision
  }
  snapshot(): Readonly<{ positionNs: bigint; speed: number; playing: boolean; focusEventId: string | null; visibleEvents: readonly ReplayEvent[] }> {
    this.advance()
    return Object.freeze({ positionNs: this.position, speed: this.playbackSpeed, playing: this.running,
      focusEventId: this.focus, visibleEvents: this.timeline.frameAt(this.position) })
  }
}
