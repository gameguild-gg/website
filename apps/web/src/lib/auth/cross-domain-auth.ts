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
  { fallback = "/feed" }: AllowedAuthRedirectOptions = {},
): string {
  const redirectTo = typeof value === "string" ? value.trim() : "";

  if (!redirectTo.startsWith("/") || redirectTo.startsWith("//")) {
    return fallback;
  }

  try {
    const base = new URL("https://auth-redirect.invalid");
    const resolved = new URL(redirectTo, base);
    return resolved.origin === base.origin
      ? `${resolved.pathname}${resolved.search}${resolved.hash}`
      : fallback;
  } catch {
    return fallback;
  }
}
