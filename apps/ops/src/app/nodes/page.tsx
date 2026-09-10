"use client";

import { Loader2 } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@game-guild/ui/components/alert";
import { Badge } from "@game-guild/ui/components/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@game-guild/ui/components/card";
import { Progress } from "@game-guild/ui/components/progress";

import { useNodes, usePrometheus } from "@/lib/polling";

type NodeRow = {
  name: string;
  zone: string;
  role: "control-plane" | "agent";
  ready: boolean;
  flannelHealthy: boolean;
  podCount?: number;
};

// ponytail: the canonical zones for this cluster. New zones fall back to
// "unknown" so they still render (just not in a dedicated column).
const ZONE_ORDER = ["home", "champlain", "cloud", "leahy"] as const;

type VectorResult = {
  data?: {
    result?: Array<{
      metric?: { instance?: string; node?: string; mountpoint?: string };
      value?: [number, string];
    }>;
  };
};

// Flatten a Prometheus vector into { nodeName -> numericValue }. Prefers
// metric.node (k8s node name), falls back to the hostname of metric.instance.
function extractMap(data: unknown): Record<string, number> {
  if (!data || typeof data !== "object") return {};
  const result = (data as VectorResult).data?.result ?? [];
  const out: Record<string, number> = {};
  for (const point of result) {
    const key =
      point.metric?.node ?? point.metric?.instance?.split(":")[0];
    const raw = point.value?.[1];
    if (!key || raw === undefined) continue;
    const num = Number.parseFloat(raw);
    if (Number.isFinite(num)) out[key] = num;
  }
  return out;
}

function extractFsMap(
  data: unknown,
): Record<string, Record<string, number>> {
  if (!data || typeof data !== "object") return {};
  const result = (data as VectorResult).data?.result ?? [];
  const out: Record<string, Record<string, number>> = {};
  for (const point of result) {
    const node =
      point.metric?.node ?? point.metric?.instance?.split(":")[0];
    const mount = point.metric?.mountpoint;
    const raw = point.value?.[1];
    if (!node || !mount || raw === undefined) continue;
    const num = Number.parseFloat(raw);
    if (!Number.isFinite(num)) continue;
    (out[node] ??= {})[mount] = num;
  }
  return out;
}

function formatBytes(n: number): string {
  if (n >= 1e12) return `${(n / 1e12).toFixed(1)} TB`;
  return `${(n / 1e9).toFixed(1)} GB`;
}

// 0..1 ratio → thresholded colour. Recording rules already normalise.
function tintClass(ratio: number): string {
  if (ratio >= 0.85) return "[&[data-slot=progress-indicator]]:bg-red-500";
  if (ratio >= 0.6) return "[&[data-slot=progress-indicator]]:bg-yellow-500";
  return "[&[data-slot=progress-indicator]]:bg-green-500";
}

function MetricBar({
  label,
  ratio,
  detail,
}: {
  label: string;
  ratio: number | undefined;
  detail?: string;
}) {
  if (ratio === undefined || !Number.isFinite(ratio)) {
    return (
      <div className="text-muted-foreground">
        {label}: N/A
      </div>
    );
  }
  const pct = Math.max(0, Math.min(100, ratio * 100));
  return (
    <div className="space-y-1">
      <div className="flex justify-between">
        <span>{label}</span>
        <span>{detail ?? `${pct.toFixed(0)}%`}</span>
      </div>
      <Progress value={pct} className={tintClass(ratio)} />
    </div>
  );
}

// All real disks per node. vfat/boot, tmpfs and overlay are excluded as noise;
// /var/snap/* binds duplicate the root disk.
const DISK_FS_SELECTOR =
  'fstype=~"ext[234]|xfs|zfs|btrfs|f2fs",mountpoint!~"/var/snap/.*"';

export default function NodesPage() {
  const nodes = useNodes();
  const cpu = usePrometheus("instance:node_cpu_utilisation:rate5m");
  const mem = usePrometheus("instance:node_memory_utilisation:ratio");
  const diskSize = usePrometheus(
    `node_filesystem_size_bytes{${DISK_FS_SELECTOR}}`,
  );
  const diskAvail = usePrometheus(
    `node_filesystem_avail_bytes{${DISK_FS_SELECTOR}}`,
  );

  if (nodes.isLoading) {
    return (
      <div className="flex items-center gap-2 text-muted-foreground">
        <Loader2 className="size-4 animate-spin" />
        Loading nodes…
      </div>
    );
  }

  if (nodes.error) {
    return (
      <Alert variant="destructive">
        <AlertTitle>Failed to load nodes</AlertTitle>
        <AlertDescription>
          Retrying automatically.{" "}
          {nodes.error instanceof Error ? nodes.error.message : String(nodes.error)}
        </AlertDescription>
      </Alert>
    );
  }

  const nodeList: NodeRow[] = Array.isArray(nodes.data) ? nodes.data : [];
  const cpuMap = extractMap(cpu.data);
  const memMap = extractMap(mem.data);
  const diskSizeMap = extractFsMap(diskSize.data);
  const diskAvailMap = extractFsMap(diskAvail.data);

  function disksFor(name: string): Array<{
    mount: string;
    used: number;
    size: number;
  }> {
    const sizes = diskSizeMap[name] ?? {};
    const avails = diskAvailMap[name] ?? {};
    return Object.keys(sizes)
      .filter((mount) => avails[mount] !== undefined && sizes[mount] > 0)
      .map((mount) => ({
        mount,
        used: sizes[mount] - avails[mount],
        size: sizes[mount],
      }))
      .sort((a, b) => b.size - a.size);
  }

  const byZone: Record<string, NodeRow[]> = {};
  for (const n of nodeList) {
    (byZone[n.zone] ??= []).push(n);
  }

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Node Topology</h1>
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2 xl:grid-cols-4">
        {ZONE_ORDER.map((zone) => (
          <section key={zone} className="space-y-3">
            <h2 className="text-sm font-semibold uppercase tracking-wider text-muted-foreground">
              {zone}{" "}
              <span className="text-xs">
                ({byZone[zone]?.length ?? 0})
              </span>
            </h2>
            <div className="space-y-3">
              {(byZone[zone] ?? []).map((n) => {
                const disks = disksFor(n.name);
                return (
                  <Card key={n.name} className="node-card gap-3 py-4">
                    <CardHeader>
                      <CardTitle className="flex items-center justify-between text-sm">
                        <span className="truncate">{n.name}</span>
                        <Badge
                          variant={n.role === "control-plane" ? "default" : "secondary"}
                        >
                          {n.role}
                        </Badge>
                      </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-2 text-xs">
                      <div className="flex flex-wrap gap-2">
                        <Badge
                          variant="outline"
                          className={
                            n.ready
                              ? "border-green-500 text-green-500"
                              : "border-red-500 text-red-500"
                          }
                        >
                          {n.ready ? "Ready" : "NotReady"}
                        </Badge>
                        <Badge
                          variant="outline"
                          className={
                            n.flannelHealthy
                              ? "border-green-500 text-green-500"
                              : "border-red-500 text-red-500"
                          }
                        >
                          flannel.1
                        </Badge>
                      </div>
                      <MetricBar label="CPU" ratio={cpuMap[n.name]} />
                      <MetricBar label="Memory" ratio={memMap[n.name]} />
                      {disks.length === 0 ? (
                        <MetricBar label="Disk" ratio={undefined} />
                      ) : (
                        disks.map((d) => (
                          <MetricBar
                            key={d.mount}
                            label={`Disk ${d.mount}`}
                            ratio={d.used / d.size}
                            detail={`${formatBytes(d.used)} / ${formatBytes(d.size)}`}
                          />
                        ))
                      )}
                    </CardContent>
                  </Card>
                );
              })}
              {(byZone[zone]?.length ?? 0) === 0 && (
                <p className="text-xs text-muted-foreground">
                  No nodes in this zone.
                </p>
              )}
            </div>
          </section>
        ))}
      </div>
    </div>
  );
}
