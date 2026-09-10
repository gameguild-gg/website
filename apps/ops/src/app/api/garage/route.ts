import { NextResponse } from "next/server";
import { k8sCore, k8sCustom } from "../../../lib/k8s";
import { prometheusQuery } from "../../../lib/prometheus";
import { hostnameToZone } from "../../../lib/zones";

export const dynamic = "force-dynamic";

interface GarageNode {
  metadata?: { name?: string };
  spec?: {
    hostname?: string;
    address?: string;
    port?: number;
  };
}
interface CustomResourceList<T> {
  items?: T[];
}
interface Pod {
  metadata?: { name?: string };
  spec?: { nodeName?: string };
}
interface PodList {
  items?: Pod[];
}

const USAGE_TIMEOUT_MS = 3_000;

interface DiskUsage {
  capacityBytes: number;
  availableBytes: number;
}

// Garage disk metrics carry only volume=, so per-node identity comes from the
// scrape target: prefer the pod label, fall back to the instance host.
function sampleKey(metric: Record<string, string>): string | undefined {
  return metric.pod ?? metric.instance?.split(":")[0];
}

async function fetchDiskUsage(): Promise<Map<string, DiskUsage>> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), USAGE_TIMEOUT_MS);
  try {
    const [totalRes, availRes] = await Promise.all([
      prometheusQuery('garage_local_disk_total{volume="data"}', controller.signal),
      prometheusQuery('garage_local_disk_avail{volume="data"}', controller.signal),
    ]);

    const totals = new Map<string, number>();
    for (const point of totalRes?.data?.result ?? []) {
      const key = point.metric ? sampleKey(point.metric) : undefined;
      const value = Number(point.value?.[1]);
      if (key && Number.isFinite(value)) totals.set(key, value);
    }

    const usage = new Map<string, DiskUsage>();
    for (const point of availRes?.data?.result ?? []) {
      const key = point.metric ? sampleKey(point.metric) : undefined;
      const capacity = key ? totals.get(key) : undefined;
      const available = Number(point.value?.[1]);
      if (key && capacity !== undefined && Number.isFinite(available)) {
        usage.set(key, { capacityBytes: capacity, availableBytes: available });
      }
    }
    return usage;
  } finally {
    clearTimeout(timer);
  }
}

export async function GET() {
  try {
    const [resp, podList, usageByPod] = await Promise.all([
      k8sCustom.listNamespacedCustomObject({
        group: "deuxfleurs.fr",
        version: "v1",
        namespace: "garage",
        plural: "garagenodes",
      }) as Promise<CustomResourceList<GarageNode>>,
      k8sCore.listNamespacedPod({
        namespace: "garage",
        labelSelector: "app.kubernetes.io/name=garage",
      }) as Promise<PodList>,
      fetchDiskUsage().catch(() => new Map<string, DiskUsage>()),
    ]);

    // CRD has no zone field; derive it from pod → node → node hostname.
    const podZone = new Map<string, string>();
    for (const pod of podList.items ?? []) {
      const nodeName = pod.spec?.nodeName;
      const podName = pod.metadata?.name;
      if (nodeName && podName) {
        podZone.set(podName, hostnameToZone(nodeName));
      }
    }

    const nodes = (resp.items ?? []).map((n) => {
      const hostname = n.spec?.hostname;
      const address = n.spec?.address;
      const usage =
        (hostname !== undefined ? usageByPod.get(hostname) : undefined) ??
        (address !== undefined ? usageByPod.get(address) : undefined);

      return {
        nodeId: n.metadata?.name,
        hostname,
        address,
        port: n.spec?.port,
        zone: hostname ? (podZone.get(hostname) ?? "unknown") : "unknown",
        capacityBytes: usage?.capacityBytes,
        availableBytes: usage?.availableBytes,
        usedBytes:
          usage === undefined
            ? undefined
            : Math.max(0, usage.capacityBytes - usage.availableBytes),
      };
    });

    return NextResponse.json(nodes);
  } catch (e) {
    return NextResponse.json({ error: String(e) }, { status: 500 });
  }
}
