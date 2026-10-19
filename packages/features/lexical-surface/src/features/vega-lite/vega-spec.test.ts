import { describe, expect, it } from "vitest";
import { isVegaSpecRecord } from "./vega-spec";

describe("isVegaSpecRecord", () => {
  it.each([
    { name: "object", value: { mark: "bar" }, expected: true },
    { name: "empty object", value: {}, expected: true },
    { name: "array", value: [], expected: false },
    { name: "null", value: null, expected: false },
    { name: "undefined", value: undefined, expected: false },
    { name: "number", value: 7, expected: false },
    { name: "string", value: "bar", expected: false },
    { name: "boolean", value: false, expected: false },
  ])("checks only the JSON object boundary for $name", ({ value, expected }) => {
    expect(isVegaSpecRecord(value)).toBe(expected);
  });
});
