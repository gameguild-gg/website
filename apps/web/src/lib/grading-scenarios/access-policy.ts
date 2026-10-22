export function isGradingScenarioLabEnabled(
  environment: NodeJS.ProcessEnv = process.env,
): boolean {
  if (environment.NODE_ENV === "production") return false;
  if (environment.GRADING_SCENARIO_LAB_ENABLED === "0") return false;
  if (environment.NODE_ENV === "development") return true;
  return environment.GRADING_SCENARIO_LAB_ENABLED === "1";
}

export function isLocalGradingScenarioHost(hostHeader: string | null): boolean {
  if (!hostHeader) return false;
  const firstHost = hostHeader.split(",")[0]?.trim();
  if (!firstHost) return false;

  try {
    const hostname = new URL(`http://${firstHost}`).hostname.toLowerCase();
    return (
      hostname === "localhost" ||
      hostname === "127.0.0.1" ||
      hostname === "[::1]" ||
      hostname === "::1" ||
      hostname.endsWith(".localhost")
    );
  } catch {
    return false;
  }
}
