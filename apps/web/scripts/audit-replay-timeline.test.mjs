import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'

const source = await fs.readFile(new URL('../src/lib/audit/replay-timeline.ts', import.meta.url), 'utf8')
const compiled = stripTypeScriptTypes(source, { mode: 'strip' })
const { AuditReplayTimeline: Timeline, AuditReplayPlayback: Playback, parseReplayTimestamp: stamp, MAX_REPLAY_EVENTS, MAX_REPLAY_BOOKMARKS } =
  await import('data:text/javascript;base64,' + Buffer.from(compiled).toString('base64'))
const id = number => '10000000-0000-0000-0000-' + String(number).padStart(12, '0')
const tenant = id(90), user = id(91), session = id(92)
const epoch = '2026-10-10T00:00:00Z'
const event = (number, seconds = 0, extra = {}) => ({
  id: id(number), tenantId: tenant, createdAt: '2026-10-10T00:00:' + String(seconds).padStart(2, '0') + 'Z',
  actionType: 'Read', resourceType: 'Document', resourceId: 'doc-1', userId: user,
  sessionId: session, correlationId: 'request-1', success: true, ...extra,
})
const timeline = events => new Timeline(events, { tenantId: tenant, totalCount: events.length })
const ids = events => events.map(value => value.id)
const ns = seconds => BigInt(seconds) * 1_000_000_000n

test('timestamps preserve all seven .NET fraction digits', () => {
  assert.equal(stamp('2026-10-10T00:00:00.0000001Z') - stamp(epoch), 100n)
  assert.equal(stamp('2026-10-10T00:00:00.9999999Z') - stamp(epoch), 999_999_900n)
})
test('offsets refer to the same instant', () => {
  assert.equal(stamp('2026-10-09T21:00:00-03:00'), stamp(epoch))
  assert.equal(stamp('2026-10-10T14:00:00+14:00'), stamp(epoch))
})
test('calendar validation accepts leap years and rejects rollovers', () => {
  assert.doesNotThrow(() => stamp('2000-02-29T12:00:00Z'))
  for (const value of ['1900-02-29T12:00:00Z', '2026-02-31T00:00:00Z', '2026-04-31T00:00:00Z']) {
    assert.throws(() => stamp(value), /calendar/)
  }
})
test('timestamps require explicit zones and bounded fractions', () => {
  for (const value of ['2026-10-10', '2026-10-10T00:00:00', '2026-10-10T00:00:00.00000001Z',
    '2026-10-10T00:00:60Z', '2026-10-10T24:00:00Z', '2026-10-10T00:00:00+14:01',
    '0000-01-01T00:00:00Z', '0001-01-01T00:00:00+14:00', '9999-12-31T23:59:59-14:00']) {
    assert.throws(() => stamp(value))
  }
  assert.throws(() => stamp(null), TypeError)
})
test('chronology sorts to 100 ns precision before deterministic ID tie breaking', () => {
  const value = timeline([event(3, 0, { createdAt: '2026-10-10T00:00:00.0000002Z' }),
    event(2, 0, { createdAt: '2026-10-10T00:00:00.0000001Z' }),
    event(1, 0, { createdAt: '2026-10-10T00:00:00.0000001Z' })])
  assert.deepEqual(ids(value.events), [id(1), id(2), id(3)])
  assert.equal(value.durationNs, 100n)
})
test('events normalize GUIDs and do not mutate supplied objects', () => {
  const supplied = event(1, 0, { tenantId: tenant.toUpperCase(), userId: user.toUpperCase() })
  const before = JSON.stringify(supplied), list = [supplied], value = timeline(list)
  assert.equal(JSON.stringify(supplied), before)
  supplied.actionType = 'Write'; list.length = 0
  assert.equal(value.events[0].actionType, 'Read')
  assert.ok(Object.isFrozen(value.events)); assert.ok(Object.isFrozen(value.events[0]))
})
test('tenant isolation is enforced before any filtering', () => {
  assert.throws(() => timeline([event(1), event(2, 1, { tenantId: id(93), userId: id(94) })]), /tenant/)
})
test('global events require an explicit null tenant on both input and scope', () => {
  const global = event(1, 0, { tenantId: null })
  assert.equal(new Timeline([global], { tenantId: null, totalCount: 1 }).tenantId, null)
  const { tenantId: ignored, ...missing } = global
  assert.throws(() => new Timeline([missing], { tenantId: null, totalCount: 1 }), /tenant/i)
  assert.throws(() => new Timeline([global], { totalCount: 1 }), /tenant/i)
})
test('incomplete, oversized and inconsistent counts fail explicitly', () => {
  for (const totalCount of [0, 2, -1, 1.5, Infinity, NaN, MAX_REPLAY_EVENTS + 1]) {
    assert.throws(() => new Timeline([event(1)], { tenantId: tenant, totalCount }), /complete/)
  }
})
test('duplicate event IDs are rejected after normalization', () => {
  assert.throws(() => timeline([event(1), event(1, 1)]), /Duplicate/)
})
test('invalid required fields and optional GUIDs fail explicitly', () => {
  for (const extra of [{ id: 'no' }, { tenantId: '' }, { success: 1 }, { actionType: '' },
    { resourceType: 'x'.repeat(101) }, { userId: id(0).replace('10000000', '00000000') },
    { sessionId: 'bad' }, { correlationId: 'x'.repeat(101) }, { resourceId: 'x'.repeat(501) }]) {
    assert.throws(() => timeline([event(1, 0, extra)]))
  }
})
test('omitted optional identifiers remain null', () => {
  const value = timeline([event(1, 0, { userId: undefined, sessionId: undefined, resourceId: undefined, correlationId: undefined })])
  assert.equal(value.events[0].userId, null); assert.equal(value.events[0].sessionId, null)
  assert.equal(value.events[0].resourceId, null); assert.equal(value.events[0].correlationId, null)
})
test('filter dimensions combine with AND semantics', () => {
  const value = timeline([event(1), event(2, 1, { userId: id(94) }), event(3, 2, { actionType: 'Write' }), event(4, 3, { resourceType: 'Folder' })])
  value.setFilter({ userIds: [user], sessionIds: [session], actions: ['Read'], resourceTypes: ['Document'], resourceIds: ['doc-1'] })
  assert.deepEqual(ids(value.events), [id(1)])
})
test('date filters are inclusive and preserve fractions', () => {
  const value = timeline([event(1), event(2, 1), event(3, 2)])
  value.setFilter({ start: event(2, 1).createdAt, end: event(3, 2).createdAt })
  assert.deepEqual(ids(value.events), [id(2), id(3)])
  assert.equal(value.durationNs, ns(1)); assert.equal(value.positionOf(id(2)), 0n)
})
test('explicit empty filter matches nothing and reset restores the source', () => {
  const value = timeline([event(1), event(2, 1)])
  value.setFilter({ userIds: [] }); assert.equal(value.events.length, 0)
  assert.equal(value.durationNs, 0n); assert.deepEqual(value.frameAt(0n), [])
  value.resetFilter(); assert.equal(value.events.length, 2)
})
test('invalid filter is transactional and cannot corrupt the selected view', () => {
  const value = timeline([event(1), event(2, 1)])
  const before = value.events
  assert.throws(() => value.setFilter({ start: event(2, 1).createdAt, end: epoch }), /start/)
  assert.throws(() => value.setFilter({ userIds: ['bad'] }))
  assert.equal(value.events, before)
})
test('filter selections are bounded instead of allocating unbounded input', () => {
  const value = timeline([event(1)])
  assert.throws(() => value.setFilter({ actions: Array(MAX_REPLAY_EVENTS + 1).fill('Read') }), /selection/i)
})
test('frames and viewports include exact boundaries and timestamp ties', () => {
  const value = timeline([event(2), event(1), event(3, 1), event(4, 2)])
  assert.deepEqual(ids(value.frameAt(0n)), [id(1), id(2)])
  assert.deepEqual(ids(value.frameAt(ns(1))), [id(1), id(2), id(3)])
  assert.deepEqual(ids(value.viewport(0n, ns(1))), [id(1), id(2), id(3)])
})
test('out-of-range positions, reversed viewports and missing IDs are rejected', () => {
  const value = timeline([event(1), event(2, 1)])
  for (const position of [-1n, ns(1) + 1n, 0, NaN]) assert.throws(() => value.frameAt(position), /position/)
  assert.throws(() => value.viewport(ns(1), 0n), /start/)
  assert.throws(() => value.positionOf(id(3)), /view/)
})
test('journeys group user and session together without merging different actors', () => {
  const value = timeline([event(1), event(2, 1, { userId: id(94) }), event(3, 2), event(4, 3, { sessionId: id(95) })])
  const groups = value.journeys()
  assert.deepEqual(groups.map(group => ids(group.events)), [[id(1), id(3)], [id(2)], [id(4)]])
  assert.ok(Object.isFrozen(groups)); assert.ok(Object.isFrozen(groups[0].events))
})
test('related events use correlation, session or the same typed resource', () => {
  const value = timeline([event(1),
    event(2, 1, { sessionId: null, resourceId: null }),
    event(3, 2, { correlationId: null, resourceId: null }),
    event(4, 3, { correlationId: null, sessionId: null }),
    event(5, 4, { correlationId: null, sessionId: null, resourceType: 'Folder' })])
  assert.deepEqual(ids(value.relatedEvents(id(1))), [id(1), id(2), id(3), id(4)])
})
test('null identifiers do not create correlations', () => {
  const empty = { correlationId: null, sessionId: null, resourceId: null }
  const value = timeline([event(1, 0, empty), event(2, 1, empty)])
  assert.deepEqual(ids(value.relatedEvents(id(1))), [id(1)])
})
test('bookmarks reject duplicate IDs and require a visible event', () => {
  const value = timeline([event(1), event(2, 1)])
  value.addBookmark(id(10), id(1))
  assert.throws(() => value.addBookmark(id(10), id(2)), /already exists/)
  assert.throws(() => value.addBookmark(id(11), id(3)), /view/)
})
test('bookmark updates preserve immutable earlier values and validate note length', () => {
  const value = timeline([event(1)]), first = value.addBookmark(id(10), id(1), 'before')
  const next = value.updateBookmark(id(10), 'after')
  assert.equal(first.note, 'before'); assert.equal(next.note, 'after'); assert.ok(Object.isFrozen(next))
  assert.throws(() => value.updateBookmark(id(10), 'x'.repeat(2001)), /length/)
  assert.throws(() => value.updateBookmark(id(11), 'none'), /not exist/)
})
test('bookmarks are filtered and sorted by event position without being deleted', () => {
  const value = timeline([event(1), event(2, 1)])
  value.addBookmark(id(11), id(2)); value.addBookmark(id(10), id(1))
  assert.deepEqual(value.bookmarks().map(bookmark => bookmark.id), [id(10), id(11)])
  value.setFilter({ start: event(2, 1).createdAt })
  assert.deepEqual(value.bookmarks().map(bookmark => bookmark.id), [id(11)])
  value.resetFilter(); assert.equal(value.bookmarks().length, 2)
  assert.equal(value.removeBookmark(id(10)), true); assert.equal(value.removeBookmark(id(10)), false)
})
test('export is deterministic and drops raw sensitive fields from DTOs', () => {
  const value = timeline([event(1, 0, { description: 'secret', ipAddress: 'private', userAgent: 'ua', errorMessage: 'credential', metadata: 'raw' })])
  value.addBookmark(id(10), id(1), 'review')
  const json = value.exportReport(), report = JSON.parse(json)
  assert.equal(json, value.exportReport()); assert.equal(report.selectedCount, 1); assert.equal(report.sourceCount, 1)
  assert.equal(report.events[0].timestampNs, stamp(epoch).toString())
  for (const field of ['description', 'ipAddress', 'userAgent', 'errorMessage', 'metadata']) {
    assert.equal(Object.hasOwn(report.events[0], field), false)
  }
  assert.deepEqual(report.journeys[0].eventIds, [id(1)]); assert.equal(report.bookmarks[0].note, 'review')
})
test('playback progresses in real time and automatically stops at the end', () => {
  const value = timeline([event(1), event(2, 2)]); let clock = 0; const play = new Playback(value, () => clock)
  play.play(); clock = 500
  assert.equal(play.snapshot().positionNs, 500_000_000n); assert.equal(play.snapshot().playing, true)
  clock = 3000; assert.equal(play.snapshot().positionNs, ns(2)); assert.equal(play.snapshot().playing, false)
})
test('accelerated playback changes speed without a position jump', () => {
  const value = timeline([event(1), event(2, 10)]); let clock = 0; const play = new Playback(value, () => clock)
  play.play(); clock = 1000; play.setSpeed(4)
  assert.equal(play.snapshot().positionNs, ns(1)); clock = 1500
  assert.equal(play.snapshot().positionNs, ns(3))
})
test('speed changes read the clock only once so no interval is lost', () => {
  const value = timeline([event(1), event(2, 10)]); const ticks = [0, 1000, 2000, 3000]; let reads = 0
  const play = new Playback(value, () => ticks[reads++])
  play.play(); play.setSpeed(2)
  assert.equal(reads, 2)
  assert.equal(play.snapshot().positionNs, ns(3))
})
test('pause freezes position and resumes from its current location', () => {
  const value = timeline([event(1), event(2, 10)]); let clock = 0; const play = new Playback(value, () => clock)
  play.play(); clock = 1000; play.pause(); clock = 5000
  assert.equal(play.snapshot().positionNs, ns(1)); play.play(); clock = 6000
  assert.equal(play.snapshot().positionNs, ns(2))
})
test('seeking during playback anchors time at the chosen position', () => {
  const value = timeline([event(1), event(2, 10)]); let clock = 0; const play = new Playback(value, () => clock)
  play.play(); clock = 1000; play.seek(ns(5)); clock = 2000
  assert.equal(play.snapshot().positionNs, ns(6))
})
test('event seek and stepping preserve distinct events at the same timestamp', () => {
  const value = timeline([event(2), event(1), event(3, 1)]), play = new Playback(value, () => 0)
  play.seekEvent(id(1)); assert.equal(play.snapshot().focusEventId, id(1))
  play.step(1); assert.equal(play.snapshot().focusEventId, id(2)); assert.equal(play.snapshot().positionNs, 0n)
  play.step(1); assert.equal(play.snapshot().focusEventId, id(3))
  play.step(-1); assert.equal(play.snapshot().focusEventId, id(2))
})
test('reset stops playback and restores the first event', () => {
  const value = timeline([event(1), event(2, 2)]); let clock = 0; const play = new Playback(value, () => clock)
  play.play(); clock = 1000; play.reset()
  assert.equal(play.snapshot().positionNs, 0n); assert.equal(play.snapshot().playing, false)
  assert.equal(play.snapshot().focusEventId, id(1))
})
test('changing a filter invalidates and resets playback automatically', () => {
  const value = timeline([event(1), event(2, 5), event(3, 10)]); let clock = 0; const play = new Playback(value, () => clock)
  play.play(); clock = 9000; play.snapshot(); value.setFilter({ end: event(2, 5).createdAt })
  assert.equal(play.snapshot().positionNs, 0n); assert.equal(play.snapshot().playing, false)
  value.setFilter({ actions: [] }); assert.deepEqual(play.snapshot().visibleEvents, [])
  value.resetFilter(); assert.equal(play.snapshot().focusEventId, id(1))
})
test('empty and zero-duration timelines never start a timer-driven replay', () => {
  for (const events of [[], [event(1)], [event(1), event(2)]]) {
    const play = new Playback(timeline(events), () => { throw Error('Clock should not be needed') })
    play.play(); assert.equal(play.snapshot().playing, false)
  }
})
test('invalid speeds and steps fail before changing the state', () => {
  const play = new Playback(timeline([event(1), event(2, 1)]), () => 0)
  for (const value of [0, 0.1, 65, Infinity, NaN]) assert.throws(() => play.setSpeed(value), /speed/)
  assert.throws(() => play.step(0), /direction/); assert.equal(play.snapshot().speed, 1)
})
test('clocks must be finite, nonnegative and monotonic on every read', () => {
  for (const invalid of [-1, Infinity, NaN]) {
    const play = new Playback(timeline([event(1), event(2, 10)]), () => invalid)
    assert.throws(() => play.play(), /clock/)
  }
  let clock = 0; const play = new Playback(timeline([event(1), event(2, 10)]), () => clock)
  play.play(); clock = 2000; play.snapshot(); clock = 1500
  assert.throws(() => play.snapshot(), /clock/)
})
test('clock overflow fails without changing the last successful position', () => {
  let clock = 0; const play = new Playback(timeline([event(1), event(2, 10)]), () => clock)
  play.play(); clock = Number.MAX_SAFE_INTEGER
  assert.throws(() => play.snapshot(), /range/)
  play.reset(); assert.equal(play.snapshot().positionNs, 0n)
})

test('pre-epoch timestamps retain positive fractions without truncation', () => {
  assert.equal(stamp('1969-12-31T23:59:59.9999999Z'), -100n)
  assert.equal(stamp('1970-01-01T00:00:00.0000001Z'), 100n)
})
test('letter-containing GUIDs normalize and duplicate checks are case insensitive', () => {
  const alpha = 'a0000000-bbbb-cccc-dddd-eeeeeeeeeeee'
  const value = timeline([event(1, 0, { id: alpha.toUpperCase(), userId: alpha.toUpperCase() })])
  assert.equal(value.events[0].id, alpha); assert.equal(value.events[0].userId, alpha)
  assert.throws(() => timeline([event(1, 0, { id: alpha }), event(2, 1, { id: alpha.toUpperCase() })]), /Duplicate/)
})
test('a fully declared maximum-sized dataset is retained without silent truncation', () => {
  const value = timeline(Array.from({ length: MAX_REPLAY_EVENTS }, (_, index) => event(index + 1)))
  assert.equal(value.sourceCount, MAX_REPLAY_EVENTS)
  assert.equal(value.events.length, MAX_REPLAY_EVENTS); assert.equal(value.frameAt(0n).length, MAX_REPLAY_EVENTS)
})
test('binary-search frames match a reference filter across unsorted fractional events', () => {
  const input = Array.from({ length: 400 }, (_, index) => event(index + 1, 0, {
    createdAt: '2026-10-10T00:00:00.' + String((index * 3571) % 10000).padStart(7, '0') + 'Z',
  })).reverse()
  const value = timeline(input), first = value.events[0].timestampNs
  for (let position = 0n; position <= value.durationNs; position += 10_000n) {
    assert.deepEqual(ids(value.frameAt(position)), ids(value.events.filter(item => item.timestampNs <= first + position)))
  }
})
test('rejected filter updates do not increment the revision or reset playback', () => {
  const value = timeline([event(1), event(2, 10)]); let clock = 0; const play = new Playback(value, () => clock)
  play.play(); clock = 1000; const revision = value.revision
  assert.throws(() => value.setFilter({ actions: null }))
  assert.equal(value.revision, revision); assert.equal(play.snapshot().positionNs, ns(1)); assert.equal(play.snapshot().playing, true)
})
test('filter reset followed by play uses the new timeline origin', () => {
  const value = timeline([event(1), event(2, 5), event(3, 10)]); let clock = 0; const play = new Playback(value, () => clock)
  play.play(); clock = 8000; value.setFilter({ start: event(2, 5).createdAt })
  play.play(); clock = 9000
  assert.equal(play.snapshot().positionNs, ns(1)); assert.equal(play.snapshot().focusEventId, id(2))
})
test('seeking to the end stops playback immediately and replay restarts from the beginning', () => {
  const value = timeline([event(1), event(2, 2)]); let clock = 0; const play = new Playback(value, () => clock)
  play.play(); clock = 500; play.seek(ns(2))
  assert.equal(play.snapshot().playing, false); play.play(); clock = 1000
  assert.equal(play.snapshot().positionNs, 500_000_000n)
})
test('invalid clock while seeking does not mutate the last known position', () => {
  const value = timeline([event(1), event(2, 10)]); let clock = 1000; const play = new Playback(value, () => clock)
  play.play(); clock = 2000; play.snapshot(); clock = 1500
  assert.throws(() => play.seek(ns(5)), /clock/)
  clock = 2000; assert.equal(play.snapshot().positionNs, ns(1))
})

const sequenceRule = extra => ({ id: 'failed-then-login', label: 'Repeated failures before sign-in', groupBy: 'user', windowMs: 5000,
  steps: [{ actions: ['LoginFailed'], success: false, minimumOccurrences: 2 }, { actions: ['Login'], success: true }], ...extra })
const failed = (number, seconds = 0, extra = {}) => event(number, seconds, { actionType: 'LoginFailed', success: false, ...extra })
const signedIn = (number, seconds = 0, extra = {}) => event(number, seconds, { actionType: 'Login', success: true, ...extra })

test('sequence analysis identifies the exact ordered evidence and rule definition', () => {
  const value = timeline([signedIn(3, 3), failed(1), failed(2, 1)])
  const findings = value.sequenceFindings([sequenceRule()])
  assert.equal(findings.length, 1); assert.deepEqual(findings[0].eventIds, [id(1), id(2), id(3)])
  assert.equal(findings[0].groupKey, user); assert.equal(findings[0].elapsedNs, ns(3).toString())
  assert.equal(findings[0].startedAt, failed(1).createdAt); assert.equal(findings[0].endedAt, signedIn(3, 3).createdAt)
  assert.equal(JSON.parse(findings[0].ruleDefinition).steps[0].minimumOccurrences, 2)
})
test('configured signals require enough distinct events and the specified order', () => {
  for (const events of [[failed(1), signedIn(2, 1)], [signedIn(1), failed(2, 1), failed(3, 2)], [failed(1), failed(2, 1)]]) {
    assert.deepEqual(timeline(events).sequenceFindings([sequenceRule()]), [])
  }
})
test('inclusive windows preserve .NET sub-millisecond boundaries', () => {
  const events = [failed(1), failed(2, 1), signedIn(3, 5)]
  assert.equal(timeline(events).sequenceFindings([sequenceRule()]).length, 1)
  events[2] = signedIn(3, 5, { createdAt: '2026-10-10T00:00:05.0000001Z' })
  assert.deepEqual(timeline(events).sequenceFindings([sequenceRule()]), [])
})
test('analysis picks the latest viable start so an older prefix cannot hide a match', () => {
  const findings = timeline([failed(1), failed(2, 1), failed(3, 4), failed(4, 5), signedIn(5, 8)])
    .sequenceFindings([sequenceRule()])
  assert.deepEqual(findings.map(f => f.eventIds), [[id(3), id(4), id(5)]])
})
test('unrelated interleaved actions do not erase sequence progress', () => {
  const findings = timeline([failed(1), event(2, 1), failed(3, 2), event(4, 3), signedIn(5, 4)]).sequenceFindings([sequenceRule()])
  assert.deepEqual(findings[0].eventIds, [id(1), id(3), id(5)])
})
test('different users never supply each other failed sign-in prefixes', () => {
  assert.deepEqual(timeline([failed(1), failed(2, 1, { userId: id(99) }), signedIn(3, 2)])
    .sequenceFindings([sequenceRule()]), [])
})
test('null user identifiers never become a shared anonymous actor', () => {
  assert.deepEqual(timeline([failed(1, 0, { userId: null }), failed(2, 1, { userId: null }), signedIn(3, 2, { userId: null })])
    .sequenceFindings([sequenceRule()]), [])
})
for (const [groupBy, field] of [['session', 'sessionId'], ['correlation', 'correlationId']]) {
  test(groupBy + ' grouping is explicit and isolated, including missing identifiers', () => {
    const different = groupBy === 'session' ? id(99) : 'other-request'
    assert.deepEqual(timeline([failed(1), failed(2, 1, { [field]: different }), signedIn(3, 2)])
      .sequenceFindings([sequenceRule({ groupBy })]), [])
    assert.deepEqual(timeline([failed(1, 0, { [field]: null }), failed(2, 1, { [field]: null }), signedIn(3, 2, { [field]: null })])
      .sequenceFindings([sequenceRule({ groupBy })]), [])
    assert.equal(timeline([failed(1), failed(2, 1), signedIn(3, 2)])
      .sequenceFindings([sequenceRule({ groupBy })]).length, 1)
  })
}
test('resource grouping includes type and does not join missing resource IDs', () => {
  assert.deepEqual(timeline([failed(1), failed(2, 1, { resourceType: 'Folder' }), signedIn(3, 2)])
    .sequenceFindings([sequenceRule({ groupBy: 'resource' })]), [])
  assert.deepEqual(timeline([failed(1, 0, { resourceId: null }), failed(2, 1, { resourceId: null }), signedIn(3, 2, { resourceId: null })])
    .sequenceFindings([sequenceRule({ groupBy: 'resource' })]), [])
  const findings = timeline([failed(1), failed(2, 1), signedIn(3, 2)]).sequenceFindings([sequenceRule({ groupBy: 'resource' })])
  assert.equal(findings[0].groupKey, JSON.stringify(['Document', 'doc-1']))
})
test('an event cannot fulfill multiple overlapping sequence steps', () => {
  const rule = sequenceRule({ steps: [{ actions: ['Read'] }, { actions: ['Read'] }] })
  assert.deepEqual(timeline([event(1)]).sequenceFindings([rule]), [])
  assert.deepEqual(timeline([event(1), event(2, 1), event(3, 2)]).sequenceFindings([rule])
    .map(f => f.eventIds), [[id(1), id(2)], [id(2), id(3)]])
})
test('single-step thresholds identify bursts without fabricating success conditions', () => {
  const rule = sequenceRule({ steps: [{ actions: ['Read'], minimumOccurrences: 3 }] })
  assert.deepEqual(timeline([event(1), event(2, 1), event(3, 2), event(4, 3)]).sequenceFindings([rule])
    .map(f => f.eventIds), [[id(1), id(2), id(3)], [id(2), id(3), id(4)]])
})
test('selectors match exact configured actions and success values', () => {
  assert.deepEqual(timeline([failed(1), failed(2, 1, { success: true }), signedIn(3, 2)]).sequenceFindings([sequenceRule()]), [])
  assert.deepEqual(timeline([failed(1), failed(2, 1, { actionType: 'loginfailed' }), signedIn(3, 2)]).sequenceFindings([sequenceRule()]), [])
  const rule = sequenceRule({ steps: [{ actions: ['LoginFailed', 'ChallengeFailed'], success: false, minimumOccurrences: 2 }, { actions: ['Login'], success: true }] })
  assert.equal(timeline([failed(1), failed(2, 1, { actionType: 'ChallengeFailed' }), signedIn(3, 2)]).sequenceFindings([rule]).length, 1)
})
test('sequences use the current filter and reset restores analysis inputs', () => {
  const value = timeline([failed(1), failed(2, 1), signedIn(3, 2)])
  value.setFilter({ actions: ['Login'] }); assert.deepEqual(value.sequenceFindings([sequenceRule()]), [])
  value.resetFilter(); assert.equal(value.sequenceFindings([sequenceRule()]).length, 1)
})
test('empty rules and views produce immutable empty findings', () => {
  const empty = timeline([]).sequenceFindings([sequenceRule()]), noRules = timeline([event(1)]).sequenceFindings([])
  assert.deepEqual(empty, []); assert.deepEqual(noRules, []); assert.ok(Object.isFrozen(empty)); assert.ok(Object.isFrozen(noRules))
})
test('rule validation is complete even when no source events exist', () => {
  for (const extra of [{ id: '' }, { label: ' ' }, { groupBy: 'ip' }, { windowMs: 0 }, { windowMs: NaN },
    { windowMs: 1.5 }, { windowMs: 86400001 }, { steps: [] }, { steps: Array(17).fill({ actions: ['Read'] }) }]) {
    assert.throws(() => timeline([]).sequenceFindings([sequenceRule(extra)]))
  }
  assert.throws(() => timeline([]).sequenceFindings(null))
})
test('rule IDs are normalized and duplicates and excessive rule counts fail', () => {
  assert.throws(() => timeline([]).sequenceFindings([sequenceRule(), sequenceRule({ id: ' failed-then-login ' })]), /unique/)
  assert.throws(() => timeline([]).sequenceFindings(Array(33).fill(sequenceRule())), /32/)
})
test('step selectors validate lengths, names and boolean outcomes', () => {
  for (const step of [{ actions: [] }, { actions: Array(51).fill('Read') }, { actions: [''] },
    { actions: ['x'.repeat(101)] }, { actions: ['Read'], success: null }, { actions: ['Read'], success: 1 }]) {
    assert.throws(() => timeline([]).sequenceFindings([sequenceRule({ steps: [step] })]))
  }
})
test('sequence thresholds and expanded lengths are bounded', () => {
  for (const minimumOccurrences of [0, -1, 1.5, NaN, Infinity, null, 65]) {
    assert.throws(() => timeline([]).sequenceFindings([sequenceRule({ steps: [{ actions: ['Read'], minimumOccurrences }] })]))
  }
  assert.throws(() => timeline([]).sequenceFindings([sequenceRule({ steps: [
    { actions: ['Read'], minimumOccurrences: 64 }, { actions: ['Write'] }] })]), /64/)
})
test('rule inputs remain unchanged and findings expose only fixed evidence fields', () => {
  const rule = sequenceRule(), before = JSON.stringify(rule), extra = { description: 'secret', metadata: 'secret', ipAddress: 'secret' }
  const finding = timeline([failed(1, 0, extra), failed(2, 1, extra), signedIn(3, 2, extra)]).sequenceFindings([rule])[0]
  assert.equal(JSON.stringify(rule), before); assert.ok(Object.isFrozen(finding)); assert.ok(Object.isFrozen(finding.eventIds))
  assert(!JSON.stringify(finding).includes('secret'))
  assert.throws(() => finding.eventIds.push(id(9)), TypeError)
})
test('multiple rules sort findings by completion time and stable identity', () => {
  const events = [failed(1), failed(2, 1), signedIn(3, 2), failed(4, 3), failed(5, 4), signedIn(6, 5)]
  const rules = [sequenceRule({ id: 'z' }), sequenceRule({ id: 'a' })]
  const result = timeline(events).sequenceFindings(rules)
  assert.deepEqual(result.map(f => [f.ruleId, f.eventIds.at(-1)]), [['a', id(3)], ['z', id(3)], ['a', id(6)], ['z', id(6)]])
  assert.deepEqual(timeline([...events].reverse()).sequenceFindings([...rules].reverse()), result)
})
test('finding overflow rejects the complete result instead of silently dropping evidence', () => {
  const events = Array.from({ length: 1001 }, (_, index) => event(index + 1))
  const rule = sequenceRule({ steps: [{ actions: ['Read'] }] })
  assert.throws(() => timeline(events).sequenceFindings([rule]), /1000.*No partial/)
  assert.equal(timeline(events.slice(0, 1000)).sequenceFindings([rule]).length, 1000)
})
test('100 ns ties preserve deterministic sequence evidence and distinct events', () => {
  const events = [signedIn(3), failed(2), failed(1)]
  assert.deepEqual(timeline(events).sequenceFindings([sequenceRule()])[0].eventIds, [id(1), id(2), id(3)])
})
test('a late terminal event cannot revive an expired sequence', () => {
  const events = [failed(1), failed(2, 1), event(3, 30), signedIn(4, 31)]
  assert.deepEqual(timeline(events).sequenceFindings([sequenceRule()]), [])
})
test('group interleaving preserves independent completed evidence chains', () => {
  const events = [failed(1), failed(2, 1, { userId: id(99) }), failed(3, 2), failed(4, 3, { userId: id(99) }),
    signedIn(5, 4, { userId: id(99) }), signedIn(6, 5)]
  assert.deepEqual(timeline(events).sequenceFindings([sequenceRule()]).map(f => f.eventIds), [[id(2), id(4), id(5)], [id(1), id(3), id(6)]])
})
test('dynamic prefixes match exhaustive small-sequence existence and newest-start evidence', () => {
  let random = 1729
  const next = () => { random = (Math.imul(random, 1664525) + 1013904223) >>> 0; return random }
  for (let sample = 0; sample < 200; sample++) {
    const events = Array.from({ length: 8 }, (_, index) => event(index + 1, index, { actionType: ['A', 'B', 'C'][next() % 3] }))
    const actions = Array.from({ length: 3 }, () => ['A', 'B', 'C'][next() % 3])
    const rule = sequenceRule({ windowMs: 4000, steps: actions.map(action => ({ actions: [action] })) })
    const expected = new Map()
    for (let a = 0; a < events.length; a++) for (let b = a + 1; b < events.length; b++) for (let c = b + 1; c < events.length; c++) {
      if ([events[a].actionType, events[b].actionType, events[c].actionType].every((value, index) => value === actions[index]) && c - a <= 4) {
        expected.set(events[c].id, Math.max(expected.get(events[c].id) ?? -1, a))
      }
    }
    const findings = timeline(events).sequenceFindings([rule])
    assert.deepEqual(findings.map(f => f.eventIds.at(-1)), [...expected.keys()].sort())
    for (const finding of findings) assert.equal(finding.eventIds[0], events[expected.get(finding.eventIds.at(-1))].id)
  }
})

test('global tenant selection still requires a concrete grouping identifier', () => {
  const events = [failed(1, 0, { tenantId: null }), failed(2, 1, { tenantId: null }), signedIn(3, 2, { tenantId: null })]
  const value = new Timeline(events, { tenantId: null, totalCount: events.length })
  assert.equal(value.sequenceFindings([sequenceRule()]).length, 1)
})
test('maximum sequence length identifies every distinct event in order', () => {
  const events = Array.from({ length: 64 }, (_, index) => event(index + 1))
  const rule = sequenceRule({ steps: [{ actions: ['Read'], minimumOccurrences: 64 }] })
  const findings = timeline(events).sequenceFindings([rule])
  assert.equal(findings.length, 1); assert.deepEqual(findings[0].eventIds, events.map(e => e.id))
})
test('a full 10000-event dataset with no terminal match remains bounded and complete', () => {
  const events = Array.from({ length: MAX_REPLAY_EVENTS }, (_, index) => event(index + 1))
  const rule = sequenceRule({ steps: [{ actions: ['Read'], minimumOccurrences: 63 }, { actions: ['Write'] }] })
  assert.deepEqual(timeline(events).sequenceFindings([rule]), [])
})

test('sparse rule, step and action selections fail instead of bypassing validation', () => {
  assert.throws(() => timeline([]).sequenceFindings(Array(1)))
  assert.throws(() => timeline([]).sequenceFindings([sequenceRule({ steps: Array(1) })]))
  assert.throws(() => timeline([]).sequenceFindings([sequenceRule({ steps: [{ actions: Array(1) }] })]))
})


test('sparse singleton event arrays cannot masquerade as a complete supplied dataset', () => {
  assert.throws(() => new Timeline(Array(1), { tenantId: tenant, totalCount: 1 }), TypeError)
})
for (const selector of ['userIds', 'sessionIds', 'resourceIds', 'resourceTypes', 'actions']) {
  test('sparse ' + selector + ' filters fail atomically instead of silently hiding evidence', () => {
    const value = timeline([event(1), event(2, 1)])
    value.addBookmark(id(80), id(1), 'preserved')
    const revision = value.revision, previous = value.exportReport()
    assert.throws(() => value.setFilter({ [selector]: Array(1) }), TypeError)
    assert.equal(value.revision, revision)
    assert.equal(value.exportReport(), previous)
  })
}
test('bookmarks expose a bounded in-memory capacity', () => {
  assert.equal(MAX_REPLAY_BOOKMARKS, 1000)
})
test('bookmark capacity overflow rejects atomically; edits and removal remain available', () => {
  const value = timeline([event(1)])
  for (let index = 0; index < 1000; index++) value.addBookmark(id(1000 + index), id(1), 'note')
  assert.equal(value.bookmarks().length, 1000)
  const previous = value.exportReport()
  assert.throws(() => value.addBookmark(id(2000), id(1), 'overflow'), /1000 bookmarks/)
  assert.equal(value.exportReport(), previous)
  assert.equal(value.updateBookmark(id(1000), 'edited at capacity').note, 'edited at capacity')
  assert.equal(value.removeBookmark(id(1000)), true)
  value.addBookmark(id(2000), id(1), 'reused capacity')
  assert.equal(value.bookmarks().length, 1000)
})
