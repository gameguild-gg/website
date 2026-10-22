import { describe, expect, it } from 'vitest';
import {
  buildPermissionChangeQueryText,
  buildPermissionPatternDataset,
  normalizePermissionWindowDays,
  orderPermissionAlertsFirst,
  type PermissionPatternsQueryPayload,
} from './permission-patterns';

describe('normalizePermissionWindowDays', () => {
  it('clamps to the supported window', () => {
    expect(normalizePermissionWindowDays(null)).toBe(30);
    expect(normalizePermissionWindowDays(0)).toBe(30);
    expect(normalizePermissionWindowDays(-5)).toBe(30);
    expect(normalizePermissionWindowDays(7.9)).toBe(7);
    expect(normalizePermissionWindowDays(10_000)).toBe(366);
  });
});

describe('buildPermissionChangeQueryText', () => {
  it('targets permission-resource mutations after the window start', () => {
    const now = new Date('2026-10-20T12:00:00Z');
    expect(buildPermissionChangeQueryText(14, now)).toBe('resource:Permission AFTER 2026-10-06');
    expect(buildPermissionChangeQueryText(1, now)).toBe('resource:Permission AFTER 2026-10-19');
  });
});

describe('buildPermissionPatternDataset', () => {
  const payload: PermissionPatternsQueryPayload = {
    aggregates: {
      byAction: { 'Permission.Revoked': 7, 'Permission.Granted': 21, '': 3 },
      failureCount: 2,
      matchedCount: 28,
      successCount: 26,
      timeBuckets: [
        { count: 4, dateUtc: '2026-10-18' },
        { count: 0, dateUtc: null },
        { count: 9, dateUtc: '2026-10-19' },
      ],
    },
    rows: [
      { actor: 'alice' },
      { actor: 'alice' },
      { actor: 'bob' },
      { Actor: 'carol' },
      { userId: 'dave' },
      { actor: '   ' },
      { somethingElse: 1 },
    ],
    scanTruncated: true,
    windowStartUtc: '2026-10-01T00:00:00Z',
    windowEndUtc: '2026-10-20T00:00:00Z',
  };

  it('derives the summary from aggregates and the window', () => {
    const dataset = buildPermissionPatternDataset(payload);
    expect(dataset.summary.matchedCount).toBe(28);
    expect(dataset.summary.failureCount).toBe(2);
    expect(dataset.summary.successCount).toBe(26);
    expect(dataset.summary.scanTruncated).toBe(true);
    expect(dataset.summary.windowStartUtc).toBe('2026-10-01T00:00:00Z');
  });

  it('keeps only dated buckets and sorts the action mix by volume', () => {
    const dataset = buildPermissionPatternDataset(payload);
    expect(dataset.dailySeries).toEqual([
      { dateUtc: '2026-10-18', count: 4 },
      { dateUtc: '2026-10-19', count: 9 },
    ]);
    expect(dataset.byAction.map((entry) => entry.actor)).toEqual(['Permission.Granted', 'Permission.Revoked']);
  });

  it('aggregates top actors across actor/userId row shapes and skips blanks', () => {
    const dataset = buildPermissionPatternDataset(payload);
    expect(dataset.topActors).toEqual([
      { actor: 'alice', count: 2 },
      { actor: 'bob', count: 1 },
      { actor: 'carol', count: 1 },
      { actor: 'dave', count: 1 },
    ]);
  });

  it('tolerates a missing aggregates block', () => {
    const dataset = buildPermissionPatternDataset({ rows: null });
    expect(dataset.summary.matchedCount).toBe(0);
    expect(dataset.dailySeries).toEqual([]);
    expect(dataset.topActors).toEqual([]);
  });
});

describe('orderPermissionAlertsFirst', () => {
  it('prioritizes permission rules, then severity, then occurrence count', () => {
    const ordered = orderPermissionAlertsFirst([
      { ruleId: 'security.failed-authentication-burst', severity: 'Critical', occurrenceCount: 40 },
      { ruleId: 'security.permission-change-burst', severity: 'High', occurrenceCount: 12 },
      { ruleId: 'security.permission-bulk-change', severity: 'High', occurrenceCount: 30 },
      { ruleId: 'security.permission-change-burst', severity: 'Critical', occurrenceCount: 15 },
    ]);
    expect(ordered.map((alert) => alert.ruleId)).toEqual([
      'security.permission-change-burst',
      'security.permission-bulk-change',
      'security.permission-change-burst',
      'security.failed-authentication-burst',
    ]);
    expect(ordered[0]?.occurrenceCount).toBe(15);
    expect(ordered[2]?.occurrenceCount).toBe(12);
  });
});
