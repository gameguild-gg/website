import { describe, expect, it } from "vitest";
import {
  isGradingScenarioLabEnabled,
  isLocalGradingScenarioHost,
} from "./access-policy";

describe("grading scenario lab access policy", () => {
  it("is enabled by default only in development", () => {
    expect(isGradingScenarioLabEnabled({ NODE_ENV: "development" })).toBe(true);
    expect(isGradingScenarioLabEnabled({ NODE_ENV: "test" })).toBe(false);
    expect(isGradingScenarioLabEnabled({ NODE_ENV: "production" })).toBe(false);
  });

  it("allows an explicit test opt-in but never enables production", () => {
    expect(
      isGradingScenarioLabEnabled({
        NODE_ENV: "test",
        GRADING_SCENARIO_LAB_ENABLED: "1",
      }),
    ).toBe(true);
    expect(
      isGradingScenarioLabEnabled({
        NODE_ENV: "development",
        GRADING_SCENARIO_LAB_ENABLED: "0",
      }),
    ).toBe(false);
    expect(
      isGradingScenarioLabEnabled({
        NODE_ENV: "production",
        GRADING_SCENARIO_LAB_ENABLED: "1",
      }),
    ).toBe(false);
  });

  it("accepts local hosts and rejects remote or malformed hosts", () => {
    expect(isLocalGradingScenarioHost("localhost:3000")).toBe(true);
    expect(isLocalGradingScenarioHost("127.0.0.1:3000")).toBe(true);
    expect(isLocalGradingScenarioHost("[::1]:3000")).toBe(true);
    expect(isLocalGradingScenarioHost("grading.localhost:3000")).toBe(true);
    expect(isLocalGradingScenarioHost("localhost:3000, proxy.internal")).toBe(
      true,
    );
    expect(isLocalGradingScenarioHost("grading.example.com")).toBe(false);
    expect(isLocalGradingScenarioHost("localhost.example.com")).toBe(false);
    expect(isLocalGradingScenarioHost(null)).toBe(false);
  });
});
