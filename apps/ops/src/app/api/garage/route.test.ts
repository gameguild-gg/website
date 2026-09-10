import { describe, it, expect, vi, beforeEach } from "vitest";

const mocks = vi.hoisted(() => {
  return {
    listCustom: vi.fn(),
    listPods: vi.fn(),
    prom: vi.fn(),
  };
});

vi.mock("../../../lib/k8s", () => ({
  k8sCustom: {
    listNamespacedCustomObject: mocks.listCustom,
  },
  k8sCore: {
    listNamespacedPod: mocks.listPods,
  },
}));

vi.mock("../../../lib/prometheus", () => ({
  PROMETHEUS_URL: "http://stub:9090",
  prometheusQuery: mocks.prom,
}));

import { GET } from "./route";

function promResult(result: unknown) {
  return { status: "success", data: { resultType: "vector", result } };
}

function sample(metric: Record<string, string>, value: string) {
  return { metric, value: [Date.now() / 1000, value] };
}

function emptyProm() {
  mocks.prom.mockResolvedValue(promResult([]));
}

// Route queries total first, then avail (Promise.all argument order).
function usageProm(
  totals: Array<{ metric: Record<string, string>; value: string }>,
  avails: Array<{ metric: Record<string, string>; value: string }>,
) {
  mocks.prom
    .mockResolvedValueOnce(promResult(totals.map((t) => sample(t.metric, t.value))))
    .mockResolvedValueOnce(promResult(avails.map((a) => sample(a.metric, a.value))));
}

describe("garage route", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    emptyProm();
  });

  it("derives zone from garage pod's node hostname", async () => {
    mocks.listCustom.mockResolvedValue({
      items: [
        {
          metadata: { name: "node-1" },
          spec: {
            hostname: "garage-qg5sc",
            address: "10.0.0.1",
            port: 3901,
          },
        },
        {
          metadata: { name: "node-2" },
          spec: {
            hostname: "garage-abcde",
            address: "10.0.0.2",
            port: 3901,
          },
        },
      ],
    });
    mocks.listPods.mockResolvedValue({
      items: [
        { metadata: { name: "garage-qg5sc" }, spec: { nodeName: "mario" } },
        { metadata: { name: "garage-abcde" }, spec: { nodeName: "oracle" } },
      ],
    });

    const res = await GET();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveLength(2);
    expect(body[0]).toMatchObject({
      nodeId: "node-1",
      hostname: "garage-qg5sc",
      address: "10.0.0.1",
      port: 3901,
      zone: "champlain",
    });
    expect(body[1]).toMatchObject({
      nodeId: "node-2",
      hostname: "garage-abcde",
      address: "10.0.0.2",
      port: 3901,
      zone: "cloud",
    });
  });

  it("returns zone 'unknown' when hostname has no matching pod", async () => {
    mocks.listCustom.mockResolvedValue({
      items: [
        {
          metadata: { name: "node-1" },
          spec: {
            hostname: "garage-missing",
            address: "10.0.0.1",
            port: 3901,
          },
        },
      ],
    });
    mocks.listPods.mockResolvedValue({ items: [] });

    const res = await GET();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body[0].zone).toBe("unknown");
  });

  it("merges disk usage by pod label and instance host fallback", async () => {
    mocks.listCustom.mockResolvedValue({
      items: [
        {
          metadata: { name: "node-1" },
          spec: {
            hostname: "garage-qg5sc",
            address: "10.0.0.1",
            port: 3901,
          },
        },
        {
          metadata: { name: "node-2" },
          spec: {
            hostname: "garage-abcde",
            address: "10.0.0.2",
            port: 3901,
          },
        },
      ],
    });
    mocks.listPods.mockResolvedValue({ items: [] });
    usageProm(
      [
        { metric: { volume: "data", pod: "garage-qg5sc" }, value: "1000000000000" },
        { metric: { volume: "data", instance: "10.0.0.2:3903" }, value: "2000000000000" },
      ],
      [
        { metric: { volume: "data", pod: "garage-qg5sc" }, value: "250000000000" },
        { metric: { volume: "data", instance: "10.0.0.2:3903" }, value: "1500000000000" },
      ],
    );

    const res = await GET();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body[0]).toMatchObject({
      capacityBytes: 1_000_000_000_000,
      availableBytes: 250_000_000_000,
      usedBytes: 750_000_000_000,
    });
    expect(body[1]).toMatchObject({
      capacityBytes: 2_000_000_000_000,
      availableBytes: 1_500_000_000_000,
      usedBytes: 500_000_000_000,
    });
  });

  it("ignores avail samples with no matching total", async () => {
    mocks.listCustom.mockResolvedValue({ items: [] });
    mocks.listPods.mockResolvedValue({ items: [] });
    usageProm(
      [],
      [{ metric: { volume: "data", pod: "garage-orphan" }, value: "1" }],
    );

    const res = await GET();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toEqual([]);
    expect(mocks.prom).toHaveBeenCalledTimes(2);
  });

  it("omits usage fields when Prometheus errors", async () => {
    mocks.listCustom.mockResolvedValue({
      items: [
        {
          metadata: { name: "node-1" },
          spec: { hostname: "garage-qg5sc", address: "10.0.0.1", port: 3901 },
        },
      ],
    });
    mocks.listPods.mockResolvedValue({ items: [] });
    mocks.prom.mockReset();
    mocks.prom.mockRejectedValue(new Error("prom down"));

    const res = await GET();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body[0]).toMatchObject({ nodeId: "node-1" });
    expect(body[0].capacityBytes).toBeUndefined();
    expect(body[0].usedBytes).toBeUndefined();
  });

  it("returns 500 with error message on failure", async () => {
    mocks.listCustom.mockRejectedValue(new Error("forbidden"));

    const res = await GET();
    expect(res.status).toBe(500);
    const body = await res.json();
    expect(body.error).toContain("forbidden");
  });
});
