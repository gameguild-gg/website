import { describe, expect, it } from "vitest";
import {
  addScoreValues,
  compareScoreValues,
  percentValueFromPercentage,
  parsePercentValue,
  parseScoreValue,
  scoreValueFromPoints,
  scoreValueByRatio,
} from "./index";

describe("academic value objects", () => {
  it("parses human values into integer storage units", () => {
    expect(scoreValueFromPoints("12.5")).toBe(1_250);
    expect(percentValueFromPercentage("5")).toBe(500);
    expect(() => parseScoreValue("12.5")).toThrow(TypeError);
    expect(() => parsePercentValue(10_001)).toThrow(TypeError);
    expect(() => scoreValueFromPoints("1.001")).toThrow(TypeError);
  });

  it("uses exact integer arithmetic and half-up quantization", () => {
    const one = scoreValueFromPoints("1");
    expect(addScoreValues([one, one])).toBe(200);
    expect(scoreValueByRatio(one, 1n, 3n)).toBe(33);
    expect(scoreValueByRatio(one, 1n, 32n)).toBe(3);
    expect(compareScoreValues(one, scoreValueFromPoints("2"))).toBe(-100);
  });
});
