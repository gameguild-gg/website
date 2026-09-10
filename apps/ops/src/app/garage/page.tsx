"use client";

import { useGarage } from "@/lib/polling";
import {
  Alert,
  AlertDescription,
  AlertTitle,
} from "@game-guild/ui/components/alert";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@game-guild/ui/components/card";
import { Progress } from "@game-guild/ui/components/progress";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@game-guild/ui/components/table";

interface GarageNode {
  nodeId?: string;
  hostname?: string;
  address?: string;
  port?: number;
  zone?: string;
  capacityBytes?: number;
  availableBytes?: number;
  usedBytes?: number;
}

function formatBytes(n: number): string {
  if (n >= 1e12) return `${(n / 1e12).toFixed(1)} TB`;
  return `${(n / 1e9).toFixed(1)} GB`;
}

function tintClass(ratio: number): string {
  if (ratio >= 0.85) return "[&[data-slot=progress-indicator]]:bg-red-500";
  if (ratio >= 0.6) return "[&[data-slot=progress-indicator]]:bg-yellow-500";
  return "[&[data-slot=progress-indicator]]:bg-green-500";
}

function StorageBar({
  usedBytes,
  capacityBytes,
}: {
  usedBytes?: number;
  capacityBytes?: number;
}) {
  if (
    usedBytes === undefined ||
    capacityBytes === undefined ||
    capacityBytes <= 0
  ) {
    return <span className="text-muted-foreground">—</span>;
  }
  const pct = Math.min(100, (usedBytes / capacityBytes) * 100);
  return (
    <div className="flex min-w-40 items-center gap-2">
      <Progress value={pct} className={tintClass(usedBytes / capacityBytes)} />
      <span className="whitespace-nowrap tabular-nums text-muted-foreground">
        {formatBytes(usedBytes)} / {formatBytes(capacityBytes)}
      </span>
    </div>
  );
}

export default function GaragePage() {
  const { data, isLoading, error } = useGarage();

  if (isLoading) return <p>Loading…</p>;
  if (error)
    return (
      <Alert variant="destructive">
        <AlertDescription>
          Failed to load Garage data. Will retry shortly.
        </AlertDescription>
      </Alert>
    );

  const nodes: GarageNode[] = Array.isArray(data) ? data : [];
  const withUsage = nodes.filter(
    (n) => n.usedBytes !== undefined && n.capacityBytes !== undefined,
  );
  const totalUsed = withUsage.reduce((sum, n) => sum + (n.usedBytes ?? 0), 0);
  const totalCapacity = withUsage.reduce(
    (sum, n) => sum + (n.capacityBytes ?? 0),
    0,
  );

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Garage</h1>

      <h2 className="text-lg font-medium">Registered Nodes: {nodes.length}</h2>

      {withUsage.length > 0 && (
        <Card className="cluster-usage-card">
          <CardHeader>
            <CardTitle>Cluster Storage</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            <StorageBar usedBytes={totalUsed} capacityBytes={totalCapacity} />
            <p className="text-xs text-muted-foreground">
              Aggregated across {withUsage.length} node
              {withUsage.length === 1 ? "" : "s"} reporting usage metrics
              (garage_local_disk_total/avail, data volume).
            </p>
          </CardContent>
        </Card>
      )}

      {nodes.length === 0 ? (
        <Alert>
          <AlertDescription>No registered nodes</AlertDescription>
        </Alert>
      ) : (
        <Table className="garage-node-table">
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Zone</TableHead>
              <TableHead>Address</TableHead>
              <TableHead>Port</TableHead>
              <TableHead>Storage</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {nodes.map((n) => (
              <TableRow key={n.nodeId ?? n.hostname}>
                <TableCell className="font-medium" title={n.nodeId}>
                  {n.hostname ?? n.nodeId}
                </TableCell>
                <TableCell>{n.zone}</TableCell>
                <TableCell>{n.address}</TableCell>
                <TableCell>{n.port}</TableCell>
                <TableCell>
                  <StorageBar
                    usedBytes={n.usedBytes}
                    capacityBytes={n.capacityBytes}
                  />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      {nodes.length > 0 && withUsage.length === 0 && (
        <Alert>
          <AlertTitle>Usage unavailable</AlertTitle>
          <AlertDescription>
            No garage disk metrics found in Prometheus. Storage usage requires
            garage_local_disk_total/avail to be scraped from the garage pods.
          </AlertDescription>
        </Alert>
      )}
    </div>
  );
}
