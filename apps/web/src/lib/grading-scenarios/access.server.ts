import "server-only";

import { getSession } from "@/auth";
import { hasRole } from "@game-guild/client";
import { headers } from "next/headers";
import {
  isGradingScenarioLabEnabled,
  isLocalGradingScenarioHost,
} from "./access-policy";

export interface GradingScenarioLabAccess {
  allowed: boolean;
  webBaseUrl: string | null;
  reason:
    "allowed" | "disabled" | "non-local" | "unauthenticated" | "forbidden";
}

export async function getGradingScenarioLabAccess(): Promise<GradingScenarioLabAccess> {
  if (!isGradingScenarioLabEnabled()) {
    return { allowed: false, webBaseUrl: null, reason: "disabled" };
  }

  const requestHeaders = await headers();
  const host =
    requestHeaders.get("x-forwarded-host") ?? requestHeaders.get("host");
  if (!isLocalGradingScenarioHost(host)) {
    return { allowed: false, webBaseUrl: null, reason: "non-local" };
  }

  const session = await getSession();
  if (!session) {
    return { allowed: false, webBaseUrl: null, reason: "unauthenticated" };
  }
  if (!hasRole(session, "SystemAdmin")) {
    return { allowed: false, webBaseUrl: null, reason: "forbidden" };
  }

  const protocol =
    requestHeaders.get("x-forwarded-proto")?.split(",")[0]?.trim() || "http";
  return {
    allowed: true,
    webBaseUrl: `${protocol}://${host?.split(",")[0]?.trim()}`,
    reason: "allowed",
  };
}
