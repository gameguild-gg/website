import { describe, expect, it } from "vitest";
import { QuizEntryType, parseQuizAnswer, safeParseQuizAnswer } from "./index";

describe("quiz answer runtime schema", () => {
  it("parses a discriminated answer without coercion", () => {
    expect(parseQuizAnswer({
      type: QuizEntryType.Hotspot,
      point: { x: 12.5, y: 20 },
    })).toEqual({
      type: QuizEntryType.Hotspot,
      point: { x: 12.5, y: 20 },
    });
  });

  it("rejects unknown fields and textual structural encodings", () => {
    expect(safeParseQuizAnswer({
      type: QuizEntryType.Matching,
      matches: "left:right",
    }).success).toBe(false);
    expect(safeParseQuizAnswer({
      type: QuizEntryType.Hotspot,
      point: { x: "12", y: "20" },
    }).success).toBe(false);
    expect(safeParseQuizAnswer({
      type: QuizEntryType.Ordering,
      itemIds: ["a"],
      score: 100,
    }).success).toBe(false);
  });
});
