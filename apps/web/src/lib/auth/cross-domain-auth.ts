interface SharedAuthCookieEnvironment {
  authCookieDomain?: string;
  authCookieSecure?: boolean | string;
  nodeEnv?: string;
}

interface SharedAuthCookieConfig {
  domain?: string;
  httpOnly: true;
  name: "gameguild";
  path: "/";
  sameSite: "lax";
  secure: boolean;
}

interface AllowedAuthRedirectOptions {
  fallback?: string;
}

export function createSharedAuthCookieConfig({
  authCookieDomain,
  authCookieSecure,
  nodeEnv,
}: SharedAuthCookieEnvironment): SharedAuthCookieConfig {
  const explicitSecure =
    typeof authCookieSecure === "boolean"
      ? authCookieSecure
      : authCookieSecure?.trim().toLowerCase();

  return {
    name: "gameguild",
    secure:
      explicitSecure === true || explicitSecure === "true"
        ? true
        : explicitSecure === false || explicitSecure === "false"
          ? false
          : nodeEnv === "production",
    sameSite: "lax",
    path: "/",
    domain: authCookieDomain?.trim() || undefined,
    httpOnly: true,
  };
}

export function resolveAllowedAuthRedirect(
  value: unknown,
  { fallback = "/social" }: AllowedAuthRedirectOptions = {},
): string {
  const redirectTo = typeof value === "string" ? value.trim() : "";

  return redirectTo.startsWith("/") && !redirectTo.startsWith("//")
    ? redirectTo
    : fallback;
}
