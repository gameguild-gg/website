"use client";

import {
  inspectGradingScenarioAction,
  prepareGradingScenarioAction,
  resetGradingScenarioAction,
  verifyGradingScenarioAction,
} from "@/lib/grading-scenarios/actions";
import type {
  GradingScenarioActionResult,
  GradingScenarioAdapterSummary,
  GradingScenarioDefinitionSummary,
  GradingScenarioLabModel,
  GradingScenarioState,
} from "@/lib/grading-scenarios/contracts";
import {
  Alert,
  AlertDescription,
  AlertTitle,
} from "@game-guild/ui/components/alert";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@game-guild/ui/components/alert-dialog";
import { Badge } from "@game-guild/ui/components/badge";
import { Button } from "@game-guild/ui/components/button";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@game-guild/ui/components/card";
import { Checkbox } from "@game-guild/ui/components/checkbox";
import { Label } from "@game-guild/ui/components/label";
import {
  NativeSelect,
  NativeSelectOption,
} from "@game-guild/ui/components/native-select";
import { Switch } from "@game-guild/ui/components/switch";
import {
  AlertCircle,
  CheckCircle2,
  Clipboard,
  ExternalLink,
  ListChecks,
  Loader2,
  Play,
  RefreshCw,
  RotateCcw,
  ShieldCheck,
} from "lucide-react";
import { useEffect, useMemo, useRef, useState, useTransition } from "react";
import { toast } from "sonner";

function scenarioFor(
  adapter: GradingScenarioAdapterSummary,
  scenarioKey: string,
): GradingScenarioDefinitionSummary {
  const fallback = adapter.scenarios[0];
  if (!fallback)
    throw new Error(`Adapter "${adapter.key}" does not expose any scenarios.`);
  return (
    adapter.scenarios.find((scenario) => scenario.key === scenarioKey) ??
    fallback
  );
}

function stateKey(adapterKey: string, scenarioKey: string) {
  return `${adapterKey}:${scenarioKey}`;
}

function initialCheckpoint(adapter: GradingScenarioAdapterSummary): string {
  const checkpoint = adapter.checkpoints.includes("learner-ready")
    ? "learner-ready"
    : adapter.checkpoints[0];
  if (!checkpoint) {
    throw new Error(
      `Adapter "${adapter.key}" does not expose any checkpoints.`,
    );
  }
  return checkpoint;
}

function healthBadge(state: GradingScenarioState | undefined) {
  if (!state || state.health === "not-prepared")
    return <Badge variant="outline">Not prepared</Badge>;
  if (state.health === "unhealthy")
    return <Badge variant="destructive">Needs attention</Badge>;
  return <Badge variant="default">Ready</Badge>;
}

export function GradingScenarioLab({
  initialModel,
}: {
  initialModel: GradingScenarioLabModel;
}) {
  const initialAdapter = initialModel.adapters[0];
  if (!initialAdapter)
    throw new Error("The grading scenario registry is empty.");
  const [adapterKey, setAdapterKey] = useState(initialAdapter.key);
  const [scenarioKey, setScenarioKey] = useState(
    initialAdapter.defaultScenarioKey,
  );
  const initialScenario = scenarioFor(
    initialAdapter,
    initialAdapter.defaultScenarioKey,
  );
  const [targetKeys, setTargetKeys] = useState<string[]>(
    initialScenario.targets.map((target) => target.key),
  );
  const [checkpoint, setCheckpoint] = useState(() =>
    initialCheckpoint(initialAdapter),
  );
  const [fresh, setFresh] = useState(false);
  const [capabilitySuite, setCapabilitySuite] = useState("all");
  const [states, setStates] = useState(
    () =>
      new Map(
        initialModel.states.map((state) => [
          stateKey(state.adapterKey, state.scenarioKey),
          state,
        ]),
      ),
  );
  const [result, setResult] = useState<GradingScenarioActionResult | null>(
    null,
  );
  const [pending, startTransition] = useTransition();
  const labRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    labRef.current?.setAttribute("data-hydrated", "true");
  }, []);

  const adapter =
    initialModel.adapters.find((candidate) => candidate.key === adapterKey) ??
    initialAdapter;
  const scenario = scenarioFor(adapter, scenarioKey);
  const state = states.get(stateKey(adapter.key, scenario.key));
  const commonLinks =
    state?.links.filter((link) => link.targetKey === null) ?? [];

  const selectedTargetSet = useMemo(() => new Set(targetKeys), [targetKeys]);
  const capabilitySuites = [
    "all",
    ...new Set(adapter.capabilityCatalog.map((entry) => entry.suite)),
  ];
  const visibleCapabilities =
    capabilitySuite === "all"
      ? adapter.capabilityCatalog
      : adapter.capabilityCatalog.filter(
          (entry) => entry.suite === capabilitySuite,
        );

  function replaceSelection(
    nextAdapter: GradingScenarioAdapterSummary,
    nextScenarioKey: string,
  ) {
    const nextScenario = scenarioFor(nextAdapter, nextScenarioKey);
    setScenarioKey(nextScenario.key);
    setTargetKeys(nextScenario.targets.map((target) => target.key));
    setResult(null);
  }

  function record(next: GradingScenarioActionResult) {
    setResult(next);
    if (next.success) {
      setStates((current) =>
        new Map(current).set(
          stateKey(next.state.adapterKey, next.state.scenarioKey),
          next.state,
        ),
      );
      toast.success(next.message);
    } else {
      toast.error(next.error);
    }
  }

  function prepare() {
    startTransition(async () => {
      record(
        await prepareGradingScenarioAction({
          adapterKey: adapter.key,
          scenarioKey: scenario.key,
          targetKeys,
          checkpoint,
          fresh,
        }),
      );
    });
  }

  function verify() {
    startTransition(async () => {
      record(
        await verifyGradingScenarioAction({
          adapterKey: adapter.key,
          scenarioKey: scenario.key,
        }),
      );
    });
  }

  function refresh() {
    startTransition(async () => {
      record(
        await inspectGradingScenarioAction({
          adapterKey: adapter.key,
          scenarioKey: scenario.key,
        }),
      );
    });
  }

  function reset() {
    startTransition(async () => {
      record(
        await resetGradingScenarioAction({
          adapterKey: adapter.key,
          scenarioKey: scenario.key,
        }),
      );
    });
  }

  function toggleTarget(key: string, checked: boolean) {
    setTargetKeys((current) =>
      checked
        ? [...current, key]
        : current.filter((candidate) => candidate !== key),
    );
  }

  async function copy(value: string, label: string) {
    try {
      await navigator.clipboard.writeText(value);
      toast.success(`${label} copied.`);
    } catch {
      toast.error(`${label} could not be copied.`);
    }
  }

  return (
    <div
      ref={labRef}
      data-testid="grading-scenario-lab"
      data-hydrated="false"
      className="space-y-6"
    >
      <Card>
        <CardHeader className="border-b">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <CardTitle className="text-lg">Scenario configuration</CardTitle>
            {healthBadge(state)}
          </div>
        </CardHeader>
        <CardContent className="space-y-6 p-5">
          <div className="grid gap-4 lg:grid-cols-3">
            <div className="space-y-2">
              <Label htmlFor="grading-scenario-adapter">Grading adapter</Label>
              <NativeSelect
                id="grading-scenario-adapter"
                className="w-full"
                value={adapter.key}
                disabled={pending}
                onChange={(event) => {
                  const next = initialModel.adapters.find(
                    (candidate) => candidate.key === event.target.value,
                  );
                  if (!next) return;
                  setAdapterKey(next.key);
                  setCapabilitySuite("all");
                  setCheckpoint(initialCheckpoint(next));
                  replaceSelection(next, next.defaultScenarioKey);
                }}
              >
                {initialModel.adapters.map((candidate) => (
                  <NativeSelectOption key={candidate.key} value={candidate.key}>
                    {candidate.title}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
            </div>
            <div className="space-y-2">
              <Label htmlFor="grading-scenario-definition">Scenario</Label>
              <NativeSelect
                id="grading-scenario-definition"
                className="w-full"
                value={scenario.key}
                disabled={pending}
                onChange={(event) =>
                  replaceSelection(adapter, event.target.value)
                }
              >
                {adapter.scenarios.map((candidate) => (
                  <NativeSelectOption key={candidate.key} value={candidate.key}>
                    {candidate.title}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
            </div>
            <div className="space-y-2">
              <Label htmlFor="grading-scenario-checkpoint">Checkpoint</Label>
              <NativeSelect
                id="grading-scenario-checkpoint"
                className="w-full"
                value={checkpoint}
                disabled={pending}
                onChange={(event) => setCheckpoint(event.target.value)}
              >
                {adapter.checkpoints.map((candidate) => (
                  <NativeSelectOption key={candidate} value={candidate}>
                    {candidate}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
            </div>
          </div>

          <section
            aria-labelledby="scenario-targets-heading"
            className="space-y-3"
          >
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h2
                id="scenario-targets-heading"
                className="text-sm font-semibold capitalize"
              >
                {adapter.targetLabelPlural}
              </h2>
              <span className="text-xs text-muted-foreground">
                {targetKeys.length}/{scenario.targets.length} selected
              </span>
            </div>
            <div className="grid gap-2 md:grid-cols-2 xl:grid-cols-3">
              {scenario.targets.map((target) => (
                <label
                  key={target.key}
                  className="flex min-h-20 cursor-pointer items-start gap-3 rounded-md border p-3 hover:bg-muted/40"
                >
                  <Checkbox
                    data-testid={`grading-target-${target.key}`}
                    aria-label={`${target.title} (${target.key})`}
                    checked={selectedTargetSet.has(target.key)}
                    disabled={pending}
                    onCheckedChange={(checked) =>
                      toggleTarget(target.key, checked === true)
                    }
                  />
                  <span className="min-w-0">
                    <span className="block truncate text-sm font-medium">
                      {target.title}
                    </span>
                    <span className="mt-1 block text-xs text-muted-foreground">
                      {target.workflow.join(" → ") ||
                        target.capability ||
                        target.kind}
                    </span>
                  </span>
                </label>
              ))}
            </div>
          </section>

          <div className="flex flex-col gap-4 border-t pt-5 sm:flex-row sm:items-center sm:justify-between">
            <label className="flex items-center gap-3 text-sm">
              <Switch
                checked={fresh}
                disabled={pending}
                onCheckedChange={(checked) => setFresh(checked === true)}
              />
              Recreate owned scenario
            </label>
            <div className="flex flex-wrap justify-end gap-2">
              <Button
                type="button"
                variant="outline"
                disabled={pending}
                onClick={refresh}
              >
                <RefreshCw
                  className={pending ? "size-4 animate-spin" : "size-4"}
                />
                Refresh
              </Button>
              <Button
                type="button"
                variant="outline"
                disabled={pending || !state?.prepared}
                onClick={verify}
              >
                {pending ? (
                  <Loader2 className="size-4 animate-spin" />
                ) : (
                  <CheckCircle2 className="size-4" />
                )}
                Verify
              </Button>
              <AlertDialog>
                <AlertDialogTrigger
                  data-testid="grading-scenario-reset-trigger"
                  render={
                    <Button
                      type="button"
                      variant="outline"
                      disabled={pending || !state?.prepared}
                    />
                  }
                >
                  <RotateCcw className="size-4" />
                  Reset
                </AlertDialogTrigger>
                <AlertDialogContent>
                  <AlertDialogHeader>
                    <AlertDialogTitle>Reset {scenario.title}?</AlertDialogTitle>
                    <AlertDialogDescription>
                      Only resources recorded by this local scenario manifest
                      will be removed.
                    </AlertDialogDescription>
                  </AlertDialogHeader>
                  <AlertDialogFooter>
                    <AlertDialogCancel>Cancel</AlertDialogCancel>
                    <AlertDialogAction
                      data-testid="grading-scenario-reset-confirm"
                      onClick={reset}
                    >
                      Reset scenario
                    </AlertDialogAction>
                  </AlertDialogFooter>
                </AlertDialogContent>
              </AlertDialog>
              <Button
                type="button"
                disabled={pending || targetKeys.length === 0}
                onClick={prepare}
              >
                {pending ? (
                  <Loader2 className="size-4 animate-spin" />
                ) : (
                  <Play className="size-4" />
                )}
                {pending ? "Preparing…" : "Prepare"}
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <section
        aria-labelledby="capability-catalog-heading"
        className="space-y-3"
      >
        <div className="flex flex-wrap items-end justify-between gap-3 border-b pb-3">
          <div>
            <div className="flex items-center gap-2">
              <ListChecks className="size-5" />
              <h2
                id="capability-catalog-heading"
                className="text-lg font-semibold"
              >
                Capability catalog
              </h2>
            </div>
            <p className="mt-1 text-sm text-muted-foreground">
              Stable validation cases exposed by the selected adapter.
            </p>
          </div>
          <div className="min-w-48 space-y-1">
            <Label htmlFor="grading-capability-suite">Suite</Label>
            <NativeSelect
              id="grading-capability-suite"
              value={capabilitySuite}
              onChange={(event) => setCapabilitySuite(event.target.value)}
            >
              {capabilitySuites.map((suite) => (
                <NativeSelectOption key={suite} value={suite}>
                  {suite === "all" ? "All suites" : suite}
                </NativeSelectOption>
              ))}
            </NativeSelect>
          </div>
        </div>
        <div className="divide-y rounded-md border">
          {visibleCapabilities.map((entry) => (
            <div
              key={entry.id}
              className="grid gap-2 p-3 md:grid-cols-[minmax(13rem,0.7fr)_minmax(0,1.3fr)_auto] md:items-center"
            >
              <div className="min-w-0">
                <p className="truncate font-mono text-xs font-medium">
                  {entry.id}
                </p>
                <p className="text-xs text-muted-foreground">{entry.suite}</p>
              </div>
              <div className="min-w-0">
                <p className="text-sm">{entry.capability}</p>
                <p className="text-xs text-muted-foreground">
                  {entry.automation} · {entry.owner}
                </p>
              </div>
              <Badge variant="outline">{entry.status}</Badge>
            </div>
          ))}
        </div>
      </section>

      {result ? (
        <Alert variant={result.success ? "default" : "destructive"}>
          {result.success ? (
            <CheckCircle2 className="size-4" />
          ) : (
            <AlertCircle className="size-4" />
          )}
          <AlertTitle>
            {result.success ? result.message : "Operation failed"}
          </AlertTitle>
          <AlertDescription>
            {result.success
              ? (result.progress.at(-1) ?? "The scenario state was updated.")
              : result.error}
          </AlertDescription>
        </Alert>
      ) : null}

      {state?.prepared ? (
        <div className="grid gap-6 xl:grid-cols-[minmax(0,1.25fr)_minmax(22rem,0.75fr)]">
          <section
            className="space-y-5"
            aria-labelledby="scenario-state-heading"
          >
            <div className="flex flex-wrap items-center justify-between gap-3 border-b pb-3">
              <div>
                <h2
                  id="scenario-state-heading"
                  className="text-lg font-semibold"
                >
                  Scenario state
                </h2>
                <p className="text-sm text-muted-foreground">
                  Run {state.runId}
                </p>
              </div>
              <div className="flex items-center gap-2">
                <Badge variant="outline">
                  Declared: {state.declaredCheckpoint}
                </Badge>
                <Badge variant="outline">
                  Confirmed: {state.confirmedCheckpoint ?? "not verified"}
                </Badge>
              </div>
            </div>

            {state.diagnostic ? (
              <Alert variant="destructive">
                <AlertCircle className="size-4" />
                <AlertDescription>{state.diagnostic}</AlertDescription>
              </Alert>
            ) : null}

            <div className="divide-y rounded-md border">
              {state.targets.map((target) => (
                <div
                  key={target.key}
                  className="flex flex-wrap items-center justify-between gap-3 p-3"
                >
                  <div>
                    <p className="text-sm font-medium">{target.title}</p>
                    <p className="text-xs text-muted-foreground">
                      {target.key}
                    </p>
                  </div>
                  <div className="flex gap-2 text-xs">
                    <Badge variant="outline">{target.declaredCheckpoint}</Badge>
                    <Badge
                      variant={
                        target.confirmedCheckpoint === target.declaredCheckpoint
                          ? "default"
                          : "secondary"
                      }
                    >
                      {target.confirmedCheckpoint ?? "pending verification"}
                    </Badge>
                  </div>
                </div>
              ))}
            </div>

            <section
              aria-labelledby="scenario-links-heading"
              className="space-y-3"
            >
              <h3 id="scenario-links-heading" className="text-sm font-semibold">
                Open surfaces
              </h3>
              <div className="flex flex-wrap gap-2">
                {commonLinks.map((link) => (
                  <Button
                    key={link.key}
                    variant="outline"
                    size="sm"
                    nativeButton={false}
                    render={
                      <a href={link.href} target="_blank" rel="noreferrer" />
                    }
                  >
                    <ExternalLink className="size-4" />
                    {link.label}
                  </Button>
                ))}
              </div>
              {state.targets.map((target) => {
                const links = state.links.filter(
                  (link) => link.targetKey === target.key,
                );
                if (links.length === 0) return null;
                return (
                  <div
                    key={target.key}
                    className="grid gap-2 border-t pt-3 sm:grid-cols-[12rem_minmax(0,1fr)]"
                  >
                    <p className="truncate text-sm font-medium">
                      {target.title}
                    </p>
                    <div className="flex flex-wrap gap-2">
                      {links.map((link) => (
                        <Button
                          key={link.key}
                          variant="ghost"
                          size="sm"
                          nativeButton={false}
                          render={
                            <a
                              href={link.href}
                              target="_blank"
                              rel="noreferrer"
                            />
                          }
                        >
                          <ExternalLink className="size-4" />
                          {link.label}
                        </Button>
                      ))}
                    </div>
                  </div>
                );
              })}
            </section>

            {state.checks.length > 0 ? (
              <section
                aria-labelledby="scenario-checks-heading"
                className="space-y-3"
              >
                <h3
                  id="scenario-checks-heading"
                  className="text-sm font-semibold"
                >
                  Verification checks
                </h3>
                <div className="divide-y rounded-md border">
                  {state.checks.map((check) => (
                    <div
                      key={check.name}
                      className="flex items-start gap-3 p-3 text-sm"
                    >
                      {check.ok ? (
                        <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-emerald-500" />
                      ) : (
                        <AlertCircle className="mt-0.5 size-4 shrink-0 text-destructive" />
                      )}
                      <div>
                        <p className="font-medium">{check.name}</p>
                        {check.detail ? (
                          <p className="text-xs text-muted-foreground">
                            {check.detail}
                          </p>
                        ) : null}
                      </div>
                    </div>
                  ))}
                </div>
              </section>
            ) : null}
          </section>

          <aside
            className="space-y-3"
            aria-labelledby="scenario-personas-heading"
          >
            <div className="flex items-center gap-2 border-b pb-3">
              <ShieldCheck className="size-5" />
              <h2
                id="scenario-personas-heading"
                className="text-lg font-semibold"
              >
                Personas
              </h2>
            </div>
            <div className="divide-y rounded-md border">
              {state.personas.map((persona) => (
                <div key={persona.key} className="space-y-3 p-4">
                  <div className="flex items-center justify-between gap-2">
                    <p className="font-medium">{persona.label}</p>
                    <Badge variant="outline">{persona.username}</Badge>
                  </div>
                  <div className="grid gap-2 text-sm">
                    <button
                      type="button"
                      className="flex min-w-0 items-center justify-between gap-2 text-left"
                      onClick={() =>
                        void copy(persona.email, `${persona.label} email`)
                      }
                    >
                      <span className="truncate text-muted-foreground">
                        {persona.email}
                      </span>
                      <Clipboard className="size-4 shrink-0" />
                    </button>
                    <button
                      type="button"
                      className="flex items-center justify-between gap-2 text-left"
                      onClick={() =>
                        void copy(persona.password, `${persona.label} password`)
                      }
                    >
                      <span className="font-mono text-muted-foreground">
                        {persona.password}
                      </span>
                      <Clipboard className="size-4 shrink-0" />
                    </button>
                    {persona.userId ? (
                      <button
                        type="button"
                        className="flex min-w-0 items-center justify-between gap-2 text-left"
                        onClick={() => {
                          if (persona.userId) {
                            void copy(
                              persona.userId,
                              `${persona.label} user ID`,
                            );
                          }
                        }}
                      >
                        <span className="truncate font-mono text-xs text-muted-foreground">
                          {persona.userId}
                        </span>
                        <Clipboard className="size-4 shrink-0" />
                      </button>
                    ) : null}
                  </div>
                </div>
              ))}
            </div>
          </aside>
        </div>
      ) : (
        <div className="flex min-h-48 flex-col items-center justify-center gap-3 rounded-md border border-dashed text-center">
          <ShieldCheck className="size-7 text-muted-foreground" />
          <p className="text-sm font-medium">
            No prepared {adapter.targetLabel} scenario
          </p>
        </div>
      )}
    </div>
  );
}
