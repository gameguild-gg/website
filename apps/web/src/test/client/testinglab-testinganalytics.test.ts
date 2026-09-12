import { describe, expect, it, vi } from "vitest";
// Product-generated API coverage belongs to the GameGuild host, not the shared client runtime.
import type { ApiClient } from "../../../../../packages/infrastructure/client/src/runtime/client.js";
import { TestingLabTestingAnalyticsModule } from "../../../../../packages/infrastructure/client/src/generated/modules/testing-lab-testing-analytics.gen.js";

describe("TestingLabTestingAnalyticsModule", () => {
  it("requests the tenant analytics report with period and comparison parameters", async () => {
    const request = vi
      .fn()
      .mockResolvedValue({ ok: true, data: { events: [] } });
    const analyticsModule = new TestingLabTestingAnalyticsModule({
      request,
      getBaseUrl: () => "https://api.example.com",
    } as ApiClient);

    await analyticsModule.getTestingAnalytics({
      fromDate: "2026-07-01T00:00:00.000Z",
      toDate: "2026-07-08T00:00:00.000Z",
      includeComparison: true,
    });

    expect(request).toHaveBeenCalledWith({
      method: "GET",
      path: "/v1/testing/analytics",
      params: {
        fromDate: "2026-07-01T00:00:00.000Z",
        toDate: "2026-07-08T00:00:00.000Z",
        includeComparison: true,
      },
      requiresAuth: true,
    });
  });

  it("requests an authenticated CSV export", async () => {
    const request = vi
      .fn()
      .mockResolvedValue({ ok: true, data: "event,applications" });
    const analyticsModule = new TestingLabTestingAnalyticsModule({
      request,
      getBaseUrl: () => "https://api.example.com",
    } as ApiClient);

    await analyticsModule.getTestingAnalyticsExport({
      fromDate: "2026-07-01T00:00:00.000Z",
      toDate: "2026-07-08T00:00:00.000Z",
    });

    expect(request).toHaveBeenCalledWith({
      method: "GET",
      path: "/v1/testing/analytics/export",
      params: {
        fromDate: "2026-07-01T00:00:00.000Z",
        toDate: "2026-07-08T00:00:00.000Z",
      },
      requiresAuth: true,
    });
  });
});
