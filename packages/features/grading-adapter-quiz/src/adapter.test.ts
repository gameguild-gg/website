import {
  ReviewCapabilityRegistry,
  canonicalizeJson,
  hashAssessmentExecutionDelivery,
} from "@game-guild/grading";
import { parseQuizPoints, QuizEntryType } from "@game-guild/quiz";
import { describe, expect, it } from "vitest";
import sharedAnswerEnvelope from "../fixtures/quiz-answer-envelope-v1.json";
import {
  classifyQuizReviewCapability,
  createQuizAnswerEnvelope,
  createQuizExecutionDelivery,
  createQuizGradingDefinition,
  createQuizItemManifest,
  decodeQuizAnswerEnvelope,
  evaluateDeterministicQuiz,
  parseQuizAnswerEnvelope,
  projectQuizGradingItems,
  quizAssessmentTypeAdapter,
  registerQuizGradingCapabilities,
} from "./index";
import {
  allQuizEntryTypesV1,
  deterministicQuizItemsV1,
  quizAnswerEnvelopeV1Fixture,
  quizAnswerVariantsV1,
} from "./testing/fixtures";

describe("quiz grading adapter contracts", () => {
  it("round-trips all 14 answer variants without textual encodings", () => {
    const parsed = parseQuizAnswerEnvelope(JSON.parse(JSON.stringify(sharedAnswerEnvelope)));
    expect(parsed).toEqual(quizAnswerEnvelopeV1Fixture);
    expect(Object.keys(parsed.payload.answers)).toHaveLength(14);
    expect(parsed.payload.answers.matching).toEqual({
      type: QuizEntryType.Matching,
      matches: { left: "right" },
    });
    expect(parsed.payload.answers.hotspot).toEqual({
      type: QuizEntryType.Hotspot,
      point: { x: 25, y: 75 },
    });
  });

  it("rejects unknown versions, fields, item IDs and mismatched answer types", () => {
    expect(() => parseQuizAnswerEnvelope({ ...quizAnswerEnvelopeV1Fixture, schemaVersion: 2 })).toThrow();
    expect(() => parseQuizAnswerEnvelope({ ...quizAnswerEnvelopeV1Fixture, answerKey: {} })).toThrow();
    expect(() => parseQuizAnswerEnvelope({
      ...quizAnswerEnvelopeV1Fixture,
      payload: { answers: { q1: { type: QuizEntryType.Matching, matches: {}, encoded: "a:b" } } },
    })).toThrow();
    expect(() => decodeQuizAnswerEnvelope(
      createQuizAnswerEnvelope({ q1: quizAnswerVariantsV1.boolean }),
      projectQuizGradingItems([{ itemId: "q1", entry: allQuizEntryTypesV1[0]! }]),
    )).toThrow(/does not match/);
  });

  it("projects only stable IDs, types and canonical scores", () => {
    expect(createQuizGradingDefinition(deterministicQuizItemsV1)).toEqual({
      schemaVersion: 2,
      items: { "true-false": {}, matching: {} },
    });
    expect(projectQuizGradingItems(deterministicQuizItemsV1)).toMatchObject([
      { itemId: "true-false", itemType: QuizEntryType.TrueFalse, maxScore: 200 },
      { itemId: "matching", itemType: QuizEntryType.Matching, maxScore: 300 },
    ]);
  });

  it("returns a partial generic result and applies exact matching partial credit", () => {
    const items = [
      ...deterministicQuizItemsV1,
      { itemId: "essay", entry: { type: QuizEntryType.Essay, stem: "Explain", points: parseQuizPoints(400), settings: { allowRetry: false } } as const },
    ];
    const result = evaluateDeterministicQuiz(projectQuizGradingItems(items), { answers: {
      "true-false": { type: QuizEntryType.TrueFalse, value: true },
      matching: { type: QuizEntryType.Matching, matches: { a: "1", b: "wrong", c: "3" } },
      essay: { type: QuizEntryType.Essay, richText: null, plainText: "Response" },
    },
    });
    expect(result.state).toBe("partial");
    expect(result.score).toBeNull();
    expect(result.items[0]?.score).toBe(200);
    expect(result.items[1]?.score).toBe(200);
    expect(result.maxScore).toBe(900);
    expect(result.items[1]?.score).toBe(200);
    expect(result.items[2]?.state).toBe("pending");
  });

  it("keeps delivery concrete, learner-safe and hash-sensitive to order", async () => {
    const projected = projectQuizGradingItems(deterministicQuizItemsV1);
    const first = createQuizExecutionDelivery("revision-1", "snapshot", projected);
    const second = createQuizExecutionDelivery(
      "revision-1",
      "snapshot",
      projected,
      ["matching", "true-false"],
    );
    expect(canonicalizeJson(first)).not.toBe(canonicalizeJson(second));
    expect(await hashAssessmentExecutionDelivery(first)).not.toBe(await hashAssessmentExecutionDelivery(second));
    expect(JSON.stringify(first)).not.toContain("correctAnswer");
    expect(JSON.stringify(first)).not.toContain('"right"');
  });

  it("registers author-test capabilities without implying official readiness", () => {
    const registry = new ReviewCapabilityRegistry();
    registerQuizGradingCapabilities(registry);
    const manifest = {
      schemaVersion: 1 as const,
      items: createQuizItemManifest(projectQuizGradingItems(deterministicQuizItemsV1)),
      stages: [{
        method: "AutomatedReview" as const,
        handlerKey: "quiz-automated-review",
        handlerVersion: "1",
      }],
      policies: [],
    };
    expect(registry.validateManifest(manifest, "author-test")).toEqual([]);
    expect(registry.validateManifest(manifest, "official-submission").length).toBeGreaterThan(0);
  });

  it("exposes projection, delivery, decoding and evaluation as one versioned adapter", () => {
    const projected = quizAssessmentTypeAdapter.projectAuthoring(deterministicQuizItemsV1);
    const envelope = createQuizAnswerEnvelope({
      "true-false": { type: QuizEntryType.TrueFalse, value: true },
      matching: { type: QuizEntryType.Matching, matches: { a: "1", b: "2", c: "3" } },
    });
    const decoded = quizAssessmentTypeAdapter.decodeResponse(envelope, projected);

    expect(quizAssessmentTypeAdapter.capability).toMatchObject({
      kind: "assessment-type-adapter",
      key: "quiz-assessment-type",
      version: "1",
    });
    expect(quizAssessmentTypeAdapter.isCurrentForAuthoring).toBe(true);
    expect(quizAssessmentTypeAdapter.evaluateDeterministic(projected, decoded).score).toBe(500);
  });

  it("classifies authoring capability independently from grading metadata", () => {
    expect(classifyQuizReviewCapability(allQuizEntryTypesV1[0]!)).toBe("automated-review");
    expect(classifyQuizReviewCapability(allQuizEntryTypesV1[5]!)).toBe("instructor-review");
    expect(classifyQuizReviewCapability(allQuizEntryTypesV1[10]!)).toBe("unsupported");
  });
});
