import { existsSync } from "node:fs";
import { resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { REPO_ROOT } from "./manifest.mjs";

let clientPackagePromise;

const silentDevToolsLogger = Object.freeze({
  group() {},
  groupEnd() {},
  log() {},
  warn() {},
  error() {},
  info() {},
  debug() {},
});

const quietDevTools = Object.freeze({
  enabled: true,
  logger: silentDevToolsLogger,
});

export async function assertStackAvailable(apiBaseUrl, webBaseUrl) {
  const checks = [
    ["API", `${apiBaseUrl}/health`, (status) => status >= 200 && status < 300],
    ["web", webBaseUrl, (status) => status >= 200 && status < 500],
  ];
  for (const [label, url, accepts] of checks) {
    await waitForEndpoint(label, url, accepts);
  }
}

async function waitForEndpoint(label, url, accepts) {
  const deadline = Date.now() + 30_000;
  let diagnostic = "no response";
  do {
    try {
      const response = await fetch(url, {
        redirect: "manual",
        signal: AbortSignal.timeout(5_000),
      });
      if (accepts(response.status)) return;
      diagnostic = `HTTP ${response.status}`;
    } catch (error) {
      diagnostic = error.message;
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  } while (Date.now() < deadline);

  throw new Error(`${label} is not ready at ${url}: ${diagnostic}`);
}

export async function authenticatePersona(
  manifest,
  personaKey,
  { create = true } = {},
) {
  const persona = manifest.personas[personaKey];
  if (!persona)
    throw new Error(`Persona "${personaKey}" is not present in this scenario.`);
  const { createClient, GeneratedApi } = await loadClientPackage();
  const anonymousClient = createClient({
    baseUrl: manifest.environment.apiBaseUrl,
    devtools: quietDevTools,
  });
  const tenantId = manifest.environment.tenantId;

  const signInRequest = () =>
    retryNetworkResult(() =>
      anonymousClient.request({
        method: "POST",
        path: "/v1/auth/sign-in",
        body: {
          email: persona.email,
          password: persona.password,
          ...(tenantId ? { tenantId } : {}),
        },
        requiresAuth: false,
      }),
    );
  let signIn = await signInRequest();

  if (!signIn.ok && create) {
    const signUp = await retryNetworkResult(() =>
      anonymousClient.request({
        method: "POST",
        path: "/v1/auth/sign-up",
        body: {
          email: persona.email,
          username: persona.username,
          password: persona.password,
          ...(tenantId ? { tenantId } : {}),
        },
        requiresAuth: false,
      }),
    );
    // A previous process may have received an invalid generated-client
    // response after the account was committed. Always retry canonical sign-in
    // before classifying signup as a failure.
    signIn = await signInRequest();
    if (!signIn.ok && !signUp.ok && !isConflict(signUp.error)) {
      throw new Error(
        `${apiFailure(`Sign up ${personaKey}`, signUp.error).message}; ${apiFailure(`Sign in ${personaKey}`, signIn.error).message}`,
      );
    }
  }
  const identity = unwrap(signIn, `Sign in ${personaKey}`);
  if (!identity.accessToken || !identity.userId || !identity.tenantId) {
    throw new Error(
      `Sign in ${personaKey} returned no accessToken, userId, or tenantId.`,
    );
  }
  if (tenantId && identity.tenantId !== tenantId) {
    throw new Error(
      `Persona ${personaKey} signed into tenant ${identity.tenantId}, expected ${tenantId}.`,
    );
  }

  persona.userId = identity.userId;
  manifest.environment.tenantId ??= identity.tenantId;
  const client = createClient({
    baseUrl: manifest.environment.apiBaseUrl,
    auth: { getAccessToken: async () => identity.accessToken },
    tenant: { getTenantId: async () => identity.tenantId },
    autoRefresh: false,
    devtools: quietDevTools,
  });

  return {
    client,
    identity,
    modules: {
      assessments: new GeneratedApi.LearningAssessmentsModule(client),
      courses: new GeneratedApi.LearningCoursesProgramModule(client),
      content: new GeneratedApi.LearningCoursesProgramContentModule(client),
      lifecycle: new GeneratedApi.LearningCoursesProgramLifecycleModule(client),
      groupSets: new GeneratedApi.LearningAssessmentsGroupSetsModule(client),
      enrollments: new GeneratedApi.LearningEnrollmentsModule(client),
    },
  };
}

export async function createAuthenticatedClient({
  apiBaseUrl,
  token,
  tenantId,
}) {
  const { createClient } = await loadClientPackage();
  return createClient({
    baseUrl: apiBaseUrl,
    auth: { getAccessToken: async () => token },
    tenant: { getTenantId: async () => tenantId },
    autoRefresh: false,
    devtools: quietDevTools,
  });
}

export function unwrap(result, label) {
  if (result?.ok) return result.data;
  throw apiFailure(label, result?.error);
}

export function apiFailure(label, error) {
  const status = error?.status ? `HTTP ${error.status}` : "request failure";
  const code = error?.code ? ` ${error.code}` : "";
  const details = error?.details ? ` ${safeStringify(error.details)}` : "";
  return new Error(
    `${label}: ${status}${code}: ${error?.message ?? "unknown error"}${details}`,
  );
}

export function isMissing(error) {
  return error?.status === 404;
}

export function isConflict(error) {
  return error?.status === 409 || error?.code === "CONFLICT";
}

export function isForbidden(error) {
  return (
    error?.status === 401 || error?.status === 403 || error?.status === 404
  );
}

export async function request(client, label, config) {
  return unwrap(await client.request({ ...config, requiresAuth: true }), label);
}

export async function tryRequest(client, config) {
  return client.request({ ...config, requiresAuth: true });
}

async function retryNetworkResult(operation) {
  let result;
  for (let attempt = 1; attempt <= 5; attempt += 1) {
    result = await operation();
    if (result.ok || result.error?.code !== "NETWORK_ERROR") return result;
    if (attempt < 5) {
      await new Promise((resolve) => setTimeout(resolve, attempt * 250));
    }
  }
  return result;
}

async function loadClientPackage() {
  clientPackagePromise ??= importClientPackage();
  return clientPackagePromise;
}

async function importClientPackage() {
  const target = resolveClientBuildTarget();
  if (!target) {
    throw new Error(
      '@game-guild/client has no build output. Keep "pnpm dev:fast" running or run "pnpm --filter @game-guild/client build", then retry.',
    );
  }
  try {
    return await import(target.specifier);
  } catch (error) {
    throw new Error(
      `Unable to load @game-guild/client from ${target.label}: ${error.message}`,
    );
  }
}

export function resolveClientBuildTarget({
  repoRoot = REPO_ROOT,
  fileExists = existsSync,
} = {}) {
  const standardEntry = resolve(
    repoRoot,
    "packages/infrastructure/client/dist/index.js",
  );
  if (fileExists(standardEntry)) {
    return { label: "dist", specifier: "@game-guild/client" };
  }

  const fastEntry = resolve(
    repoRoot,
    "packages/infrastructure/client/dist-fast/index.js",
  );
  if (fileExists(fastEntry)) {
    return { label: "dist-fast", specifier: pathToFileURL(fastEntry).href };
  }
  return null;
}

function safeStringify(value) {
  try {
    return JSON.stringify(value);
  } catch {
    return String(value);
  }
}
