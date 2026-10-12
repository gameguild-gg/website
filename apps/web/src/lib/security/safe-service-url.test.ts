import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  assertSafeRemoteUrl,
  assertSafeServiceUrl,
  UnsafeRemoteUrlError,
} from "./safe-remote-url";

describe("configured service URL policy", () => {
  beforeEach(() => {
    vi.stubEnv("NODE_ENV", "production");
    vi.stubEnv("ALLOW_UNSAFE_REMOTE_URL", "true");
  });
  afterEach(() => vi.unstubAllEnvs());

  it.each([
    "http://backend:8080",
    "http://api:8080",
    "http://127.0.0.1:8080",
    "https://api.example.test:9443",
  ])("retains configured HTTP(S) service %s without a bypass", (base) => {
    expect(assertSafeServiceUrl(`${base}/v1/ready`, base)).toBe(
      `${base}/v1/ready`,
    );
    expect(assertSafeServiceUrl("/v1/ready", base)).toBe(`${base}/v1/ready`);
  });

  it("resolves relative targets against the configured service path", () => {
    expect(assertSafeServiceUrl("ready", "http://backend:8080/v1/")).toBe(
      "http://backend:8080/v1/ready",
    );
  });

  it.each([
    "http://evil.example/x",
    "https://backend:8080/x",
    "http://backend:8081/x",
    "http://user:pass@backend:8080/x",
    "//backend:8080/x",
    "  //evil.example/x",
    "\\\\evil.example/x",
    "/\\evil.example/x",
    "  http://backend:8080/x",
    "javascript:alert(1)",
    "blob:http://backend:8080/id",
  ])(
    "rejects origin escapes, credentials and disguised target %s",
    (target) => {
      expect(() => assertSafeServiceUrl(target, "http://backend:8080")).toThrow(
        UnsafeRemoteUrlError,
      );
    },
  );

  it.each([
    "//backend:8080",
    "/\\backend:8080",
    "  http://backend:8080",
    "http://user:pass@backend:8080",
    "file:///tmp",
    "javascript:alert(1)",
    "http://[invalid",
  ])("rejects unsafe configured base %s", (base) => {
    expect(() => assertSafeServiceUrl("/v1/ready", base)).toThrow(
      UnsafeRemoteUrlError,
    );
  });

  it.each(["", "/", "/api"])(
    "preserves relative browser target for base %s",
    (base) => {
      expect(
        assertSafeServiceUrl("/api/ready?target=%2F%2Fevil.example", base),
      ).toBe("/api/ready?target=%2F%2Fevil.example");
      expect(() =>
        assertSafeServiceUrl("https://relative.invalid/ready", base),
      ).toThrow(UnsafeRemoteUrlError);
      expect(() =>
        assertSafeServiceUrl("https://evil.example/ready", base),
      ).toThrow(UnsafeRemoteUrlError);
    },
  );

  it("retains URL objects only after checking their origin", () => {
    const target = new URL("http://backend:8080/ready");
    expect(assertSafeServiceUrl(target, "http://backend:8080")).toBe(target);
  });

  it("keeps external private HTTP targets blocked in production", () => {
    expect(() => assertSafeRemoteUrl("http://backend:8080/ready")).toThrow(
      UnsafeRemoteUrlError,
    );
  });

  it.each(["test", "development", "production"])(
    "enforces service pinning in %s",
    (environment) => {
      vi.stubEnv("NODE_ENV", environment);
      expect(() =>
        assertSafeServiceUrl(
          "https://evil.example/ready",
          "http://backend:8080",
        ),
      ).toThrow(UnsafeRemoteUrlError);
    },
  );

  it("canonicalizes scheme URLs before fetching", () => {
    expect(
      assertSafeServiceUrl("http:backend:8080/ready", "http://backend:8080"),
    ).toBe("http://backend:8080/ready");
  });
});
