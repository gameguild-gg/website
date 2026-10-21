'use client';

import { buildPermissionPatternDataset, orderPermissionAlertsFirst, type PermissionPatternDataset, type PermissionPatternsAlertPayload, type PermissionPatternsQueryPayload } from '@/lib/audit/permission-patterns';
import { Alert, AlertDescription, AlertTitle } from '@game-guild/ui/components/alert';
import { Badge } from '@game-guild/ui/components/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@game-guild/ui/components/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@game-guild/ui/components/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@game-guild/ui/components/table';
import { AlertTriangle, Activity, ShieldAlert, Users } from 'lucide-react';
import { useCallback, useState } from 'react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

/** Permission-change pattern dashboard (issue #297): volume trends, action mix, top
 * actors and the permission-rule alert queue, aggregated from the guarded audit query
 * DSL over the masked audit store. The server page loads the initial dataset; this
 * console refetches through the session-scoped proxy when the window changes.
 * Authorization and masking stay server-side. */

const WINDOW_OPTIONS = [
  { value: '7', label: 'Last 7 days' },
  { value: '30', label: 'Last 30 days' },
  { value: '90', label: 'Last 90 days' },
] as const;

const ACTION_COLORS = ['#2563eb', '#dc2626', '#16a34a', '#9333ea', '#ea580c', '#0891b2', '#4f46e5', '#65a30d'];

interface PermissionPatternConsoleProps {
  readonly initialDays: number;
  readonly initialDataset: PermissionPatternDataset;
  readonly initialAlerts: readonly PermissionPatternsAlertPayload[];
}

interface ConsolePayload {
  days: number;
  queryText: string;
  query: PermissionPatternsQueryPayload;
  alerts: PermissionPatternsAlertPayload[];
}

function severityVariant(severity: string | undefined): 'default' | 'destructive' | 'secondary' | 'outline' {
  switch (severity) {
    case 'Critical': return 'destructive';
    case 'High': return 'default';
    case 'Medium': return 'secondary';
    default: return 'outline';
  }
}

function shortAction(action: string): string {
  return action.startsWith('Permission.') ? action.slice('Permission.'.length) : action;
}

export function PermissionPatternConsole({ initialDays, initialDataset, initialAlerts }: PermissionPatternConsoleProps) {
  const [days, setDays] = useState(String(initialDays));
  const [dataset, setDataset] = useState<PermissionPatternDataset>(initialDataset);
  const [alerts, setAlerts] = useState<readonly PermissionPatternsAlertPayload[]>(initialAlerts);
  const [queryText, setQueryText] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const changeWindow = useCallback(async (windowDays: string) => {
    setDays(windowDays);
    setPending(true);
    setError(null);
    try {
      const response = await fetch(`/api/audit/permission-patterns?days=${encodeURIComponent(windowDays)}`, {
        cache: 'no-store',
      });
      if (!response.ok) {
        setError(`The audit store rejected the dashboard query (HTTP ${response.status}).`);
        return;
      }
      const payload = (await response.json()) as ConsolePayload;
      setQueryText(payload.queryText);
      setDataset(buildPermissionPatternDataset(payload.query));
      setAlerts(orderPermissionAlertsFirst(payload.alerts));
    } catch {
      setError('The permission-pattern dashboard could not be loaded.');
    } finally {
      setPending(false);
    }
  }, []);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Select value={days} onValueChange={(value) => { void changeWindow(value ?? '30'); }}>
          <SelectTrigger aria-label="Window" className="w-40">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {WINDOW_OPTIONS.map((option) => (
              <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        {pending && <p className="text-xs text-muted-foreground">Loading…</p>}
        {queryText && <p className="font-mono text-xs text-muted-foreground">query: {queryText}</p>}
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertTriangle className="size-4" />
          <AlertTitle>Query failed</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="pb-2"><CardDescription className="flex items-center gap-2"><Activity className="size-4" /> Permission changes</CardDescription><CardTitle className="text-2xl">{dataset.summary.matchedCount}</CardTitle></CardHeader>
          <CardContent className="text-xs text-muted-foreground">in the selected window</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardDescription className="flex items-center gap-2"><ShieldAlert className="size-4" /> Failed mutations</CardDescription><CardTitle className="text-2xl">{dataset.summary.failureCount}</CardTitle></CardHeader>
          <CardContent className="text-xs text-muted-foreground">denied or errored grants/revokes</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardDescription className="flex items-center gap-2"><Users className="size-4" /> Distinct actors</CardDescription><CardTitle className="text-2xl">{dataset.topActors.length}</CardTitle></CardHeader>
          <CardContent className="text-xs text-muted-foreground">most active in the returned page</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardDescription>Open permission alerts</CardDescription><CardTitle className="text-2xl">{alerts.filter((alert) => alert.status === 'Open').length}</CardTitle></CardHeader>
          <CardContent className="text-xs text-muted-foreground">from the security alert queue</CardContent>
        </Card>
      </div>

      {dataset.summary.scanTruncated && (
        <Alert>
          <AlertTriangle className="size-4" />
          <AlertTitle>Scan truncated</AlertTitle>
          <AlertDescription>
            The query hit the audit store&apos;s row cap; totals describe the scanned subset only.
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Permission change volume</CardTitle>
          <CardDescription>Daily count of permission mutations (grants, revokes, changes).</CardDescription>
        </CardHeader>
        <CardContent>
          {dataset.dailySeries.length === 0 ? (
            <p className="text-sm text-muted-foreground">No permission changes recorded in this window.</p>
          ) : (
            <div className="h-64">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={[...dataset.dailySeries]}>
                  <CartesianGrid strokeDasharray="3 3" vertical={false} />
                  <XAxis dataKey="dateUtc" tick={{ fontSize: 11 }} tickFormatter={(value: string) => value.slice(5)} />
                  <YAxis allowDecimals={false} width={32} />
                  <Tooltip labelFormatter={(value: unknown) => (typeof value === 'string' ? value : '')} />
                  <Bar dataKey="count" name="changes" fill="#2563eb" radius={[3, 3, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}
        </CardContent>
      </Card>

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Action mix</CardTitle>
            <CardDescription>Mutations broken down by audit action type.</CardDescription>
          </CardHeader>
          <CardContent>
            {dataset.byAction.length === 0 ? (
              <p className="text-sm text-muted-foreground">No actions matched.</p>
            ) : (
              <div className="h-56">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={dataset.byAction.map((entry) => ({ name: shortAction(entry.actor), count: entry.count }))} layout="vertical">
                    <CartesianGrid strokeDasharray="3 3" horizontal={false} />
                    <XAxis type="number" allowDecimals={false} />
                    <YAxis type="category" dataKey="name" tick={{ fontSize: 11 }} width={92} />
                    <Tooltip />
                    <Bar dataKey="count" name="changes" radius={[0, 3, 3, 0]}>
                      {dataset.byAction.map((entry, index) => (
                        <Cell key={entry.actor} fill={ACTION_COLORS[index % ACTION_COLORS.length]} />
                      ))}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Most active actors</CardTitle>
            <CardDescription>Actors with the most permission mutations in the returned page (identities masked by the audit store).</CardDescription>
          </CardHeader>
          <CardContent>
            {dataset.topActors.length === 0 ? (
              <p className="text-sm text-muted-foreground">No actor activity in this window.</p>
            ) : (
              <ul className="space-y-2">
                {dataset.topActors.map((actor) => (
                  <li key={actor.actor} className="flex items-center justify-between gap-2 text-sm">
                    <span className="truncate font-mono text-xs">{actor.actor}</span>
                    <Badge variant="outline">{actor.count}</Badge>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Security alert queue</CardTitle>
          <CardDescription>Permission rules (bulk change, change burst) first, then the remaining tenant alerts.</CardDescription>
        </CardHeader>
        <CardContent>
          {alerts.length === 0 ? (
            <p className="text-sm text-muted-foreground">No security alerts recorded.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Rule</TableHead>
                  <TableHead>Title</TableHead>
                  <TableHead>Severity</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="text-right">Occurrences</TableHead>
                  <TableHead>Last seen</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {alerts.map((alert) => (
                  <TableRow key={alert.id ?? `${alert.ruleId}-${alert.lastSeenAtUtc}`}>
                    <TableCell className="font-mono text-xs">{alert.ruleId ?? '—'}</TableCell>
                    <TableCell className="max-w-72 truncate" title={alert.description ?? undefined}>{alert.title ?? '—'}</TableCell>
                    <TableCell><Badge variant={severityVariant(alert.severity)}>{alert.severity ?? '—'}</Badge></TableCell>
                    <TableCell>{alert.status ?? '—'}</TableCell>
                    <TableCell className="text-right">{alert.occurrenceCount ?? 0}</TableCell>
                    <TableCell className="text-xs">{alert.lastSeenAtUtc ? new Date(alert.lastSeenAtUtc).toLocaleString() : '—'}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
