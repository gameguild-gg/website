import { describe, expect, it } from "vitest";
import {
  QuizEntryType,
  createCategorizationEntry,
  createEssayEntry,
  createFillInTheBlankEntry,
  createFormulaEntry,
  createHighlightEntry,
  createHotspotEntry,
  createMatchingEntry,
  createMultipleChoiceEntry,
  createNumericEntry,
  createOrderingEntry,
  createRatingEntry,
  createShortAnswerEntry,
  createSingleChoiceEntry,
  createTrueFalseEntry,
  isQuizEntry,
  safeParseQuizEntry,
  type QuizEntry,
} from "./index";

const entries: QuizEntry[] = [
  createSingleChoiceEntry("Single"),
  createMultipleChoiceEntry("Multiple"),
  createTrueFalseEntry("Boolean"),
  createFillInTheBlankEntry("Fill ___"),
  createShortAnswerEntry("Short"),
  createEssayEntry("Essay"),
  createMatchingEntry("Matching"),
  createOrderingEntry("Ordering"),
  createCategorizationEntry("Categories"),
  createRatingEntry("Rating"),
  createNumericEntry("Numeric"),
  createFormulaEntry("Formula"),
  createHotspotEntry("Hotspot"),
  createHighlightEntry("Highlight"),
];

describe("quiz entry runtime schema", () => {
  it("parses every supported authoring entry type", () => {
    expect(entries).toHaveLength(14);
    for (const entry of entries) {
      expect(safeParseQuizEntry(entry).success, entry.type).toBe(true);
    }
  });

  it("rejects unknown discriminants and unknown fields", () => {
    expect(isQuizEntry({ type: "UNKNOWN", stem: "", settings: { allowRetry: true } })).toBe(false);
    expect(isQuizEntry({
      ...createTrueFalseEntry("Question"),
      leakedAnswerKey: true,
    })).toBe(false);
  });

  it("keeps structurally valid incomplete drafts parseable", () => {
    const draft = createSingleChoiceEntry("");
    expect(draft.type).toBe(QuizEntryType.SingleChoice);
    expect(safeParseQuizEntry(draft).success).toBe(true);
  });

  it("rejects malformed nested values", () => {
    expect(isQuizEntry({
      ...createMultipleChoiceEntry("Question"),
      options: [{ id: "one", text: 42 }],
    })).toBe(false);
  });

  it("rejects direct URLs in asset fields", () => {
    expect(isQuizEntry({
      ...createHotspotEntry("Hotspot"),
      imageAssetUri: "https://example.com/image.png",
    })).toBe(false);
  });

  it("accepts only non-negative integer academic point units", () => {
    expect(isQuizEntry({
      ...createTrueFalseEntry("Question"),
      points: 250,
    })).toBe(true);
    expect(isQuizEntry({
      ...createTrueFalseEntry("Question"),
      points: 2.5,
    })).toBe(false);
    expect(isQuizEntry({
      ...createTrueFalseEntry("Question"),
      points: "250",
    })).toBe(false);
  });
});
