import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  redirect: vi.fn(),
}));

vi.mock("@/i18n/navigation", () => ({
  redirect: mocks.redirect,
}));

import LegacyTestingAccessPage from "./access/page";
import LegacyTestingAnalyticsPage from "./analytics/page";
import LegacyTestingApplicationsPage from "./applications/page";
import LegacyTestingFeedbackPage from "./feedback/page";
import LegacyTestingLocationsPage from "./locations/page";
import LegacyTestingParticipantsPage from "./participants/page";
import LegacyTestingPeoplePage from "./people/page";
import LegacyTestingProjectsPage from "./projects/page";
import LegacyTestingProjectPage from "./projects/[projectId]/page";
import LegacyTestingReportsPage from "./reports/page";
import LegacyTestingRequestPage from "./requests/[requestId]/page";
import LegacyTestingRequestsPage from "./requests/page";
import LegacyTestingSessionPage from "./sessions/[sessionId]/page";
import LegacyTestingSessionsPage from "./sessions/page";
import TestingLabSettingsPage from "./settings/page";

describe("Testing Lab legacy routes", () => {
  beforeEach(() => {
    mocks.redirect.mockReset();
  });

  it.each([
    [LegacyTestingRequestsPage, "/workspace/testing-lab/events"],
    [LegacyTestingPeoplePage, "/workspace/testing-lab/events"],
    [LegacyTestingReportsPage, "/workspace/testing-lab/settings/analytics"],
    [LegacyTestingLocationsPage, "/workspace/testing-lab/settings/locations"],
    [LegacyTestingAccessPage, "/workspace/testing-lab/settings/access"],
    [LegacyTestingAnalyticsPage, "/workspace/testing-lab/settings/analytics"],
    [LegacyTestingApplicationsPage, "/workspace/testing-lab/events"],
    [LegacyTestingProjectsPage, "/workspace/testing-lab/events"],
    [LegacyTestingParticipantsPage, "/workspace/testing-lab/events"],
    [LegacyTestingFeedbackPage, "/workspace/testing-lab/events"],
    [LegacyTestingSessionsPage, "/workspace/testing-lab/events"],
    [TestingLabSettingsPage, "/workspace/testing-lab/settings/general"],
  ])(
    "preserves locale while redirecting a legacy workspace route",
    async (page, href) => {
      await page({ params: Promise.resolve({ locale: "pt-BR" }) });

      expect(mocks.redirect).toHaveBeenCalledWith({ href, locale: "pt-BR" });
    },
  );

  it("moves legacy request detail into the event-scoped workflow", async () => {
    await LegacyTestingRequestPage({
      params: Promise.resolve({ locale: "pt-BR", requestId: "request-42" }),
    });

    expect(mocks.redirect).toHaveBeenCalledWith({
      href: "/workspace/testing-lab/events",
      locale: "pt-BR",
    });
  });

  it("moves legacy project detail into the event-scoped workflow", async () => {
    await LegacyTestingProjectPage({
      params: Promise.resolve({ locale: "pt-BR", projectId: "project-42" }),
    });

    expect(mocks.redirect).toHaveBeenCalledWith({
      href: "/workspace/testing-lab/events",
      locale: "pt-BR",
    });
  });

  it("moves legacy session detail into the event-scoped workflow", async () => {
    await LegacyTestingSessionPage({
      params: Promise.resolve({ locale: "pt-BR", sessionId: "session-42" }),
    });

    expect(mocks.redirect).toHaveBeenCalledWith({
      href: "/workspace/testing-lab/events",
      locale: "pt-BR",
    });
  });
});
