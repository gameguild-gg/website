'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@game-guild/ui/components/alert';
import { Button } from '@game-guild/ui/components/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@game-guild/ui/components/card';
import { Input } from '@game-guild/ui/components/input';
import { Label } from '@game-guild/ui/components/label';
import { AlertTriangle, Loader2, Pause, Play, RotateCcw } from 'lucide-react';
import { importAuditReplaySnapshot, type ImportedAuditReplaySnapshot } from '@/lib/audit/replay-snapshot-import';

/** Minimal admin wiring of the landed replay core (issue #169): capture a frozen range
 * through the authenticated proxy, verify the digest and build the chronological
 * timeline, then render events, per-session journeys and a basic accelerated playback. */

type CaptureResult = Readonly<{ json: string; sha256: string; tenantId: string | null }>;

function toUtcInputValue(date: Date): string {
  return date.toISOString().slice(0, 19);
}

function defaultWindow(): { start: string; end: string } {
  const end = new Date();
  const start = new Date(end.getTime() - 60 * 60 * 1000);
  return { start: toUtcInputValue(start), end: toUtcInputValue(end) };
}

export function AuditReplayConsole() {
  const initial = useRef(defaultWindow());
  const [start, setStart] = useState(initial.current.start);
  const [end, setEnd] = useState(initial.current.end);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [snapshot, setSnapshot] = useState<ImportedAuditReplaySnapshot | null>(null);
  const [userFilter, setUserFilter] = useState('');
  const [playing, setPlaying] = useState(false);
  const [playbackIndex, setPlaybackIndex] = useState<number | null>(null);

  const capture = useCallback(async () => {
    setError(null);
    setLoading(true);
    setPlaying(false);
    setPlaybackIndex(null);
    try {
      const parameters = new URLSearchParams({
        start: new Date(`${start}Z`).toISOString(),
        end: new Date(`${end}Z`).toISOString(),
      });
      const response = await fetch(`/api/audit/replay?${parameters.toString()}`, {
        credentials: 'same-origin',
        cache: 'no-store',
      });
      const body: unknown = await response.json();
      if (!response.ok || typeof body !== 'object' || body === null) {
        const message = (body as { error?: unknown }).error;
        throw new Error(typeof message === 'string' ? message : `Capture failed (${response.status}).`);
      }
      const captured = body as CaptureResult;
      // The landed import core verifies the digest against the original bytes and the
      // authorized tenant before any timeline exists; tampered input never renders.
      const imported = await importAuditReplaySnapshot(captured.json, captured.sha256, captured.tenantId);
      setSnapshot(imported);
    } catch (cause) {
      setSnapshot(null);
      setError(cause instanceof Error ? cause.message : 'Capture failed.');
    } finally {
      setLoading(false);
    }
  }, [start, end]);

  const journeys = useMemo(() => (snapshot ? snapshot.timeline.journeys() : []), [snapshot]);
  const events = useMemo(() => {
    if (!snapshot) return [];
    if (!userFilter.trim()) return snapshot.timeline.events;
    const needle = userFilter.trim().toLowerCase();
    return snapshot.timeline.events.filter((event) => event.userId?.toLowerCase().includes(needle) ?? false);
  }, [snapshot, userFilter]);

  const stepSize = Math.max(1, Math.ceil(events.length / 60));
  // Accelerated playback (v1): an interval advances the highlighted prefix. Real-time
  // pacing and frame-accurate seeking remain open for the full investigation UI.
  useEffect(() => {
    if (!playing || events.length < 2) return;
    const timer = setInterval(() => {
      setPlaybackIndex((current) => {
        const next = (current === null ? 0 : current) + stepSize;
        if (next >= events.length - 1) {
          setPlaying(false);
          return events.length - 1;
        }
        return next;
      });
    }, 250);
    return () => clearInterval(timer);
  }, [playing, stepSize, events.length]);

  const visible = playbackIndex === null ? events : events.slice(0, playbackIndex + 1);
  const atEnd = playbackIndex !== null && playbackIndex >= events.length - 1;

  const togglePlayback = useCallback(() => {
    if (playing) {
      setPlaying(false);
      return;
    }
    if (playbackIndex === null || playbackIndex >= events.length - 1) {
      setPlaybackIndex(events.length > 1 ? 1 : 0);
    }
    setPlaying(true);
  }, [playing, playbackIndex, events.length]);

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Capture audit replay snapshot</CardTitle>
          <CardDescription>
            Frozen selection of retained audit rows for one range, verified by digest before any timeline is built.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="replay-start">Start (UTC)</Label>
              <Input id="replay-start" type="datetime-local" value={start} onChange={(event) => setStart(event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="replay-end">End (UTC)</Label>
              <Input id="replay-end" type="datetime-local" value={end} onChange={(event) => setEnd(event.target.value)} />
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-3">
            <Button onClick={capture} disabled={loading}>
              {loading ? <Loader2 className="size-4 animate-spin" aria-hidden /> : null}
              Capture snapshot
            </Button>
            {snapshot ? (
              <span className="text-muted-foreground text-sm" data-testid="replay-digest">
                SHA-256 {snapshot.sha256.slice(0, 16)}… · {snapshot.totalCount} events · {journeys.length} journeys
              </span>
            ) : null}
          </div>
          {error ? (
            <Alert variant="destructive">
              <AlertTriangle className="size-4" aria-hidden />
              <AlertTitle>Capture failed</AlertTitle>
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          ) : null}
        </CardContent>
      </Card>

      {snapshot ? (
        <Card data-testid="replay-timeline">
          <CardHeader>
            <CardTitle>Reconstructed timeline</CardTitle>
            <CardDescription>
              Chronological events with per-user session journeys; playback advances the highlighted prefix.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex flex-wrap items-center gap-2">
              <Button variant="outline" size="sm" onClick={togglePlayback} disabled={events.length < 2}>
                {playing ? <Pause className="size-4" aria-hidden /> : <Play className="size-4" aria-hidden />}
                {playing ? 'Pause' : 'Play'}
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={() => {
                  setPlaying(false);
                  setPlaybackIndex(null);
                }}
                disabled={playbackIndex === null}
              >
                <RotateCcw className="size-4" aria-hidden />
                Reset
              </Button>
              <div className="w-64">
                <Input
                  aria-label="Filter by user ID"
                  placeholder="Filter by user ID"
                  value={userFilter}
                  onChange={(event) => {
                    setUserFilter(event.target.value);
                    setPlaybackIndex(null);
                    setPlaying(false);
                  }}
                />
              </div>
              <span className="text-muted-foreground text-sm" data-testid="replay-position">
                {visible.length}/{events.length} events{atEnd && playbackIndex !== null ? ' · end of recording' : ''}
              </span>
            </div>

            <ol className="divide-y rounded-md border" data-testid="replay-events">
              {visible.slice(-50).map((event) => (
                <li key={event.id} className="flex flex-wrap items-baseline gap-x-3 gap-y-1 px-3 py-2 text-sm">
                  <span className="font-mono text-xs text-muted-foreground">{event.createdAt}</span>
                  <span className="font-medium">{event.actionType}</span>
                  <span className="text-muted-foreground">{event.resourceType}</span>
                  <span className="font-mono text-xs">{event.userId ?? 'anonymous'}</span>
                  <span className="font-mono text-xs text-muted-foreground">{event.sessionId ?? 'no-session'}</span>
                  <span className={event.success ? 'text-muted-foreground' : 'font-semibold text-destructive'}>
                    {event.success ? 'ok' : 'failed'}
                  </span>
                </li>
              ))}
              {visible.length === 0 ? (
                <li className="px-3 py-2 text-sm text-muted-foreground">No events in the current view.</li>
              ) : null}
            </ol>

            <div>
              <h3 className="mb-2 text-sm font-semibold">Session journeys</h3>
              <ul className="grid gap-2 sm:grid-cols-2" data-testid="replay-journeys">
                {journeys.slice(0, 12).map((journey) => (
                  <li
                    key={`${journey.userId ?? 'anonymous'}:${journey.sessionId ?? 'no-session'}`}
                    className="rounded-md border px-3 py-2 text-sm"
                  >
                    <span className="font-mono text-xs">{journey.userId ?? 'anonymous'}</span>
                    <span className="text-muted-foreground"> in session </span>
                    <span className="font-mono text-xs">{journey.sessionId ?? 'no-session'}</span>
                    <span className="text-muted-foreground"> · {journey.events.length} events</span>
                  </li>
                ))}
              </ul>
            </div>
          </CardContent>
        </Card>
      ) : null}
    </div>
  );
}
