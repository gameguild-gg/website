'use client';

import { buildPricingInsightsDataset, type PricingEffectivenessPayload, type PricingInsightsDataset, type PricingLedgerPagePayload } from '@/lib/billing/pricing-insights';
import { Alert, AlertDescription, AlertTitle } from '@game-guild/ui/components/alert';
import { Badge } from '@game-guild/ui/components/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@game-guild/ui/components/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@game-guild/ui/components/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@game-guild/ui/components/table';
import { AlertTriangle, BadgePercent, Calculator, Gauge, ShieldCheck, Tag, TrendingDown } from 'lucide-react';
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

/** Pricing and discount-effectiveness dashboard (issue #400): decision volume over
 * time, discount effectiveness (top promo codes by redemption and discount impact),
 * pricing-rule revenue impact and the drift/validation status of the ledger. The
 * server page loads the initial window through the session token; this console
 * refetches through the scoped proxy when the window changes. Authorization stays
 * server-side on the billing API. */

const WINDOW_OPTIONS = [
  { value: '7', label: 'Last 7 days' },
  { value: '30', label: 'Last 30 days' },
  { value: '90', label: 'Last 90 days' },
] as const;

const INSIGHT_COLORS = ['#2563eb', '#dc2626', '#16a34a', '#9333ea', '#ea580c', '#0891b2', '#4f46e5', '#65a30d'];

interface PricingInsightsConsoleProps {
  readonly initialDays: number;
  readonly initialDataset: PricingInsightsDataset;
}

interface ConsolePayload {
  days: number;
  fromUtc: string;
  toUtc: string;
  effectiveness: PricingEffectivenessPayload;
  ledger: PricingLedgerPagePayload | null;
}

function compact(value: number): string {
  return new Intl.NumberFormat('en', { notation: 'compact', maximumFractionDigits: 1 }).format(value);
}

function money(value: number): string {
  return new Intl.NumberFormat('en', { notation: 'compact', maximumFractionDigits: 2 }).format(value);
}

function percent(value: number): string {
  return `${(value * 100).toFixed(1)}%`;
}

function shortRule(ruleId: string): string {
  return ruleId.length > 12 ? `${ruleId.slice(0, 10)}…` : ruleId;
}

export function PricingInsightsConsole({ initialDays, initialDataset }: PricingInsightsConsoleProps) {
  const [days, setDays] = useState(String(initialDays));
  const [dataset, setDataset] = useState<PricingInsightsDataset>(initialDataset);
  const [windowLabel, setWindowLabel] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const changeWindow = useCallback(async (windowDays: string) => {
    setDays(windowDays);
    setPending(true);
    setError(null);
    try {
      const response = await fetch(`/api/billing/pricing-insights?days=${encodeURIComponent(windowDays)}`, {
        cache: 'no-store',
      });
      if (!response.ok) {
        setError(`The pricing analytics API rejected the dashboard query (HTTP ${response.status}).`);
        return;
      }
      const payload = (await response.json()) as ConsolePayload;
      setWindowLabel(`${payload.fromUtc.slice(0, 10)} → ${payload.toUtc.slice(0, 10)}`);
      setDataset(buildPricingInsightsDataset(payload.effectiveness, payload.ledger));
    } catch {
      setError('The pricing-insights dashboard could not be loaded.');
    } finally {
      setPending(false);
    }
  }, []);

  const driftVariant = dataset.driftHealth.drifted > 0 ? 'destructive' : 'default';

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
        {windowLabel && <p className="font-mono text-xs text-muted-foreground">window: {windowLabel} (UTC)</p>}
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertTriangle className="size-4" />
          <AlertTitle>Query failed</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <div className="grid gap-4 md:grid-cols-3 xl:grid-cols-5">
        <Card>
          <CardHeader className="pb-2"><CardDescription className="flex items-center gap-2"><Calculator className="size-4" /> Pricing decisions</CardDescription><CardTitle className="text-2xl">{dataset.summary.entryCount}</CardTitle></CardHeader>
          <CardContent className="text-xs text-muted-foreground">ledger entries in the selected window</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardDescription className="flex items-center gap-2"><Gauge className="size-4" /> Final revenue</CardDescription><CardTitle className="text-2xl">{money(dataset.summary.finalRevenue)}</CardTitle></CardHeader>
          <CardContent className="text-xs text-muted-foreground">baseline {money(dataset.summary.baselineRevenue)} before discounts</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardDescription className="flex items-center gap-2"><BadgePercent className="size-4" /> Discount given</CardDescription><CardTitle className="text-2xl">{money(dataset.summary.discountGiven)}</CardTitle></CardHeader>
          <CardContent className="text-xs text-muted-foreground">{percent(dataset.summary.discountRate)} of baseline revenue</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardDescription className="flex items-center gap-2"><Tag className="size-4" /> Promo redemptions</CardDescription><CardTitle className="text-2xl">{dataset.summary.promoRedemptions}</CardTitle></CardHeader>
          <CardContent className="text-xs text-muted-foreground">across {dataset.summary.entriesWithPromo} decisions with codes</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardDescription className="flex items-center gap-2"><ShieldCheck className="size-4" /> Drift health</CardDescription><CardTitle className="text-2xl">{dataset.driftHealth.drifted} drifted</CardTitle></CardHeader>
          <CardContent className="text-xs text-muted-foreground">{percent(dataset.driftHealth.driftRate)} of {dataset.driftHealth.validated} validated entries</CardContent>
        </Card>
      </div>

      {dataset.summary.feedTruncated && (
        <Alert>
          <AlertTriangle className="size-4" />
          <AlertTitle>Volume series truncated</AlertTitle>
          <AlertDescription>
            The daily volume chart covers the newest 100 of {dataset.summary.feedTotalCount} ledger entries; the
            totals above aggregate the full window.
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Pricing decision volume</CardTitle>
          <CardDescription>Daily count of pricing decisions (checkout line items, renewal quotes, order promo applications).</CardDescription>
        </CardHeader>
        <CardContent>
          {dataset.dailySeries.length === 0 ? (
            <p className="text-sm text-muted-foreground">No pricing decisions recorded in this window.</p>
          ) : (
            <div className="h-64">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={[...dataset.dailySeries]}>
                  <CartesianGrid strokeDasharray="3 3" vertical={false} />
                  <XAxis dataKey="dateUtc" tick={{ fontSize: 11 }} tickFormatter={(value: string) => value.slice(5)} />
                  <YAxis allowDecimals={false} width={32} />
                  <Tooltip labelFormatter={(value: unknown) => (typeof value === 'string' ? value : '')} />
                  <Bar dataKey="decisions" name="decisions" fill="#2563eb" radius={[3, 3, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}
        </CardContent>
      </Card>

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Discount effectiveness</CardTitle>
            <CardDescription>Top promo codes by discount given in the window.</CardDescription>
          </CardHeader>
          <CardContent>
            {dataset.topPromoCodes.length === 0 ? (
              <p className="text-sm text-muted-foreground">No promo-code discounts recorded in this window.</p>
            ) : (
              <div className="h-56">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={dataset.topPromoCodes.map((row) => ({ name: row.code, discountGiven: row.discountGiven }))} layout="vertical">
                    <CartesianGrid strokeDasharray="3 3" horizontal={false} />
                    <XAxis type="number" tickFormatter={(value: number) => compact(value)} />
                    <YAxis type="category" dataKey="name" tick={{ fontSize: 11 }} width={110} />
                    <Tooltip />
                    <Bar dataKey="discountGiven" name="discount given" radius={[0, 3, 3, 0]}>
                      {dataset.topPromoCodes.map((row, index) => (
                        <Cell key={row.code} fill={INSIGHT_COLORS[index % INSIGHT_COLORS.length]} />
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
            <CardTitle>Pricing-rule impact</CardTitle>
            <CardDescription>Top pricing rules by adjustment total (reduction from baseline).</CardDescription>
          </CardHeader>
          <CardContent>
            {dataset.topRules.length === 0 ? (
              <p className="text-sm text-muted-foreground">No pricing-rule adjustments recorded in this window.</p>
            ) : (
              <div className="h-56">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={dataset.topRules.map((row) => ({ name: shortRule(row.ruleId), adjustmentTotal: row.adjustmentTotal }))} layout="vertical">
                    <CartesianGrid strokeDasharray="3 3" horizontal={false} />
                    <XAxis type="number" tickFormatter={(value: number) => compact(value)} />
                    <YAxis type="category" dataKey="name" tick={{ fontSize: 11 }} width={110} />
                    <Tooltip />
                    <Bar dataKey="adjustmentTotal" name="adjustment total" radius={[0, 3, 3, 0]}>
                      {dataset.topRules.map((row, index) => (
                        <Cell key={row.ruleId} fill={INSIGHT_COLORS[index % INSIGHT_COLORS.length]} />
                      ))}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Top discounts — redemption and revenue impact</CardTitle>
          <CardDescription>
            Per promo code: redemptions, discount given, effective discount rate and the revenue collected on the influenced decisions.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {dataset.topPromoCodes.length === 0 ? (
            <p className="text-sm text-muted-foreground">No promo-code discounts recorded in this window.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Code</TableHead>
                  <TableHead className="text-right">Redemptions</TableHead>
                  <TableHead className="text-right">Discount given</TableHead>
                  <TableHead className="text-right">Discount rate</TableHead>
                  <TableHead className="text-right">Baseline revenue</TableHead>
                  <TableHead className="text-right">Influenced revenue</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {dataset.topPromoCodes.map((row) => (
                  <TableRow key={row.code}>
                    <TableCell className="font-mono text-xs">{row.code}</TableCell>
                    <TableCell className="text-right">{row.redemptions}</TableCell>
                    <TableCell className="text-right">{money(row.discountGiven)}</TableCell>
                    <TableCell className="text-right"><Badge variant="outline">{percent(row.discountRate)}</Badge></TableCell>
                    <TableCell className="text-right">{money(row.baselineRevenue)}</TableCell>
                    <TableCell className="text-right">{money(row.revenueInfluenced)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Drift &amp; validation status</CardTitle>
            <CardDescription>
              Ledger entries per drift status. Drift validation recomputes stored finals from each entry&apos;s own
              breakdown; drifted entries no longer follow from their recorded arithmetic.
            </CardDescription>
          </CardHeader>
          <CardContent>
            {dataset.driftHealth.total === 0 ? (
              <p className="text-sm text-muted-foreground">No drift statuses recorded yet.</p>
            ) : (
              <ul className="space-y-2">
                <li className="flex items-center justify-between gap-2 text-sm">
                  <span className="flex items-center gap-2"><Badge variant={driftVariant}>Drifted</Badge> entries failing recomputation</span>
                  <span className="font-mono text-xs">{dataset.driftHealth.drifted}</span>
                </li>
                <li className="flex items-center justify-between gap-2 text-sm">
                  <span className="flex items-center gap-2"><Badge variant="secondary">Verified</Badge> recomputation matches the stored final</span>
                  <span className="font-mono text-xs">{dataset.driftHealth.verified}</span>
                </li>
                <li className="flex items-center justify-between gap-2 text-sm">
                  <span className="flex items-center gap-2"><Badge variant="outline">Not validated</Badge> awaiting a validation pass</span>
                  <span className="font-mono text-xs">{dataset.driftHealth.notValidated}</span>
                </li>
                {dataset.driftHealth.other > 0 && (
                  <li className="flex items-center justify-between gap-2 text-sm">
                    <span className="flex items-center gap-2"><Badge variant="outline">Other</Badge> statuses outside the core set</span>
                    <span className="font-mono text-xs">{dataset.driftHealth.other}</span>
                  </li>
                )}
                <li className="flex items-center justify-between gap-2 border-t pt-2 text-sm">
                  <span className="text-muted-foreground">Drift rate over {dataset.driftHealth.validated} validated entries</span>
                  <span className="font-mono text-xs">{percent(dataset.driftHealth.driftRate)}</span>
                </li>
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2"><TrendingDown className="size-4" /> Stacking outcomes</CardTitle>
            <CardDescription>How discount conflicts were resolved when multiple codes/rules applied.</CardDescription>
          </CardHeader>
          <CardContent>
            {dataset.stackingOutcomes.length === 0 ? (
              <p className="text-sm text-muted-foreground">No stacking outcomes recorded in this window.</p>
            ) : (
              <ul className="space-y-2">
                {dataset.stackingOutcomes.map((outcome) => (
                  <li key={outcome.name} className="flex items-center justify-between gap-2 text-sm">
                    <span className="truncate font-mono text-xs">{outcome.name}</span>
                    <Badge variant="outline">{outcome.decisions} decisions</Badge>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
