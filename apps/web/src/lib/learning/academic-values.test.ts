import { describe, expect, it } from "vitest";
import {
  percentageToPercentUnits,
  percentUnitsToPercentage,
  pointsToScoreUnits,
  rubricScoresToUnits,
  scoreUnitsToPoints,
} from "./academic-values";

describe("academic value UI boundary", () => {
  it("converts human point and percentage decimals to integer units", () => {
    expect(pointsToScoreUnits("0.5")).toBe(50);
    expect(pointsToScoreUnits("1.5")).toBe(150);
    expect(percentageToPercentUnits("33.25")).toBe(3_325);
  });

  it("formats integer wire units for human presentation", () => {
    expect(scoreUnitsToPoints(33)).toBe(0.33);
    expect(percentUnitsToPercentage(7_550)).toBe(75.5);
  });

  it("converts every rubric criterion without changing non-score fields", () => {
    expect(JSON.parse(rubricScoresToUnits(JSON.stringify({
      clarity: { points: 0.5, comment: "Clear" },
      accuracy: { points: 1.25 },
    })))).toEqual({
      clarity: { points: 50, comment: "Clear" },
      accuracy: { points: 125 },
    });
  });

  it("rejects decimal wire values and human precision beyond two places", () => {
    expect(() => scoreUnitsToPoints(0.5)).toThrow(TypeError);
    expect(() => pointsToScoreUnits("0.005")).toThrow(TypeError);
  });
});
