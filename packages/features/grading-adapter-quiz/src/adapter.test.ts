import {
  ReviewCapabilityRegistry,
  canonicalizeJson,
  hashAssessmentExecutionDelivery,
} from "@game-guild/grading";
import {
  parseQuizAnswer,
  parseQuizEntry,
  parseQuizPoints,
  QuizEntryType,
} from "@game-guild/quiz";
import { describe, expect, it } from "vitest";
import sharedAnswerEnvelope from "../fixtures/quiz-answer-envelope-v1.json";
import partialCreditFixture from "../fixtures/quiz-partial-credit-v1.json";
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
  quizValidationCasesV1,
} from "./testing/fixtures";

describe("quiz grading adapter contracts", () => {
  it("round-trips all 14 answer variants without textual encodings", () => {
    const parsed = parseQuizAnswerEnvelope(
      JSON.parse(JSON.stringify(sharedAnswerEnvelope)),
    );
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
    expect(() =>
      parseQuizAnswerEnvelope({
        ...quizAnswerEnvelopeV1Fixture,
        schemaVersion: 2,
      }),
    ).toThrow();
    expect(() =>
      parseQuizAnswerEnvelope({
        ...quizAnswerEnvelopeV1Fixture,
        answerKey: {},
      }),
    ).toThrow();
    expect(() =>
      parseQuizAnswerEnvelope({
        ...quizAnswerEnvelopeV1Fixture,
        payload: {
          answers: {
            q1: { type: QuizEntryType.Matching, matches: {}, encoded: "a:b" },
          },
        },
      }),
    ).toThrow();
    expect(() =>
      decodeQuizAnswerEnvelope(
        createQuizAnswerEnvelope({ q1: quizAnswerVariantsV1.boolean }),
        projectQuizGradingItems([
          { itemId: "q1", entry: allQuizEntryTypesV1[0]! },
        ]),
      ),
    ).toThrow(/does not match/);
  });

  it("projects only stable IDs, types and canonical scores", () => {
    expect(createQuizGradingDefinition(deterministicQuizItemsV1)).toEqual({
      schemaVersion: 2,
      items: { "true-false": {}, matching: {} },
    });
    expect(projectQuizGradingItems(deterministicQuizItemsV1)).toMatchObject([
      {
        itemId: "true-false",
        itemType: QuizEntryType.TrueFalse,
        maxScore: 200,
      },
      { itemId: "matching", itemType: QuizEntryType.Matching, maxScore: 300 },
    ]);
  });

  it("returns a partial generic result and applies exact matching partial credit", () => {
    const items = [
      ...deterministicQuizItemsV1,
      {
        itemId: "essay",
        entry: {
          type: QuizEntryType.Essay,
          stem: "Explain",
          points: parseQuizPoints(400),
          settings: { allowRetry: false },
        } as const,
      },
    ];
    const result = evaluateDeterministicQuiz(projectQuizGradingItems(items), {
      answers: {
        "true-false": { type: QuizEntryType.TrueFalse, value: true },
        matching: {
          type: QuizEntryType.Matching,
          matches: { a: "1", b: "wrong", c: "3" },
        },
        essay: {
          type: QuizEntryType.Essay,
          richText: null,
          plainText: "Response",
        },
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

  it("matches the shared versioned partial-credit and invalid-answer vectors", () => {
    expect(partialCreditFixture.schemaVersion).toBe(1);
    const inputs = partialCreditFixture.items.map(({ itemId, entry }) => ({
      itemId,
      entry: parseQuizEntry(entry),
    }));
    const projections = projectQuizGradingItems(inputs);
    const projectionById = new Map(
      projections.map((projection) => [projection.itemId, projection]),
    );

    expect(projectionById.get("matching-partial")?.partialCreditAlgorithm).toBe(
      "matching-position-v1",
    );
    expect(projectionById.get("ordering-partial")?.partialCreditAlgorithm).toBe(
      "ordering-position-v1",
    );
    expect(
      projectionById.get("matching-all-or-nothing")?.partialCreditAlgorithm,
    ).toBeUndefined();

    for (const testCase of partialCreditFixture.scoreCases) {
      const projection = projectionById.get(testCase.itemId)!;
      const envelope = createQuizAnswerEnvelope({
        [testCase.itemId]: parseQuizAnswer(testCase.answer),
      });
      const decoded = decodeQuizAnswerEnvelope(envelope, [projection]);
      const result = evaluateDeterministicQuiz([projection], decoded);
      expect(result.score, testCase.name).toBe(testCase.expectedScore);
    }

    for (const testCase of partialCreditFixture.invalidCases) {
      const projection = projectionById.get(testCase.itemId)!;
      const envelope = createQuizAnswerEnvelope({
        [testCase.itemId]: parseQuizAnswer(testCase.answer),
      });
      expect(
        () => decodeQuizAnswerEnvelope(envelope, [projection]),
        testCase.name,
      ).toThrow();
    }
  });

  it("keeps delivery concrete, learner-safe and hash-sensitive to order", async () => {
    const projected = projectQuizGradingItems(deterministicQuizItemsV1);
    const first = createQuizExecutionDelivery(
      "revision-1",
      "snapshot",
      projected,
    );
    const second = createQuizExecutionDelivery(
      "revision-1",
      "snapshot",
      projected,
      ["matching", "true-false"],
    );
    expect(canonicalizeJson(first)).not.toBe(canonicalizeJson(second));
    expect(await hashAssessmentExecutionDelivery(first)).not.toBe(
      await hashAssessmentExecutionDelivery(second),
    );
    expect(JSON.stringify(first)).not.toContain("correctAnswer");
    expect(JSON.stringify(first)).not.toContain('"right"');
  });

  it("registers the exact runtime capabilities in both execution contexts", () => {
    const registry = new ReviewCapabilityRegistry();
    registerQuizGradingCapabilities(registry);
    const manifest = {
      schemaVersion: 1 as const,
      items: createQuizItemManifest(
        projectQuizGradingItems(deterministicQuizItemsV1),
      ),
      stages: [
        {
          method: "AutomatedReview" as const,
          handlerKey: "quiz-automated-review",
          handlerVersion: "1",
        },
      ],
      policies: [],
    };
    expect(registry.validateManifest(manifest, "author-test")).toEqual([]);
    expect(registry.validateManifest(manifest, "official-submission")).toEqual(
      [],
    );
  });

  it("exposes projection, delivery, decoding and evaluation as one versioned adapter", () => {
    const projected = quizAssessmentTypeAdapter.projectAuthoring(
      deterministicQuizItemsV1,
    );
    const envelope = createQuizAnswerEnvelope({
      "true-false": { type: QuizEntryType.TrueFalse, value: true },
      matching: {
        type: QuizEntryType.Matching,
        matches: { a: "1", b: "2", c: "3" },
      },
    });
    const decoded = quizAssessmentTypeAdapter.decodeResponse(
      envelope,
      projected,
    );

    expect(quizAssessmentTypeAdapter.capability).toMatchObject({
      kind: "assessment-type-adapter",
      key: "quiz-assessment-type",
      version: "1",
    });
    expect(quizAssessmentTypeAdapter.isCurrentForAuthoring).toBe(true);
    expect(
      quizAssessmentTypeAdapter.evaluateDeterministic(projected, decoded).score,
    ).toBe(500);
  });

  it("classifies authoring capability independently from grading metadata", () => {
    expect(classifyQuizReviewCapability(allQuizEntryTypesV1[0]!)).toBe(
      "automated-review",
    );
    expect(classifyQuizReviewCapability(allQuizEntryTypesV1[5]!)).toBe(
      "instructor-review",
    );
    expect(classifyQuizReviewCapability(allQuizEntryTypesV1[10]!)).toBe(
      "unsupported",
    );
  });

  it("keeps the validation matrix exhaustive, integer-based and learner-safe", () => {
    expect(quizValidationCasesV1).toHaveLength(18);
    expect(
      quizValidationCasesV1.filter((testCase) => testCase.baseType),
    ).toHaveLength(14);
    expect(
      new Set(quizValidationCasesV1.map((testCase) => testCase.id)).size,
    ).toBe(quizValidationCasesV1.length);

    for (const testCase of quizValidationCasesV1) {
      expect(Number.isInteger(testCase.entry.points), testCase.id).toBe(true);
      expect(classifyQuizReviewCapability(testCase.entry), testCase.id).toBe(
        testCase.officialCapability,
      );

      const projection = projectQuizGradingItems([
        {
          itemId: "q1",
          entry: testCase.entry,
        },
      ]);
      const delivery = createQuizExecutionDelivery(
        "revision-1",
        "snapshot",
        projection,
      );
      const serialized = JSON.stringify(delivery);
      for (const privateKey of privateAuthoringFields(testCase.entry.type)) {
        expect(serialized, `${testCase.id} leaked ${privateKey}`).not.toContain(
          `"${privateKey}"`,
        );
      }
      if (testCase.entry.type === QuizEntryType.Numeric) {
        expect(serialized, `${testCase.id} lost its learner prompt`).toContain(
          '"formula"',
        );
      }
    }
  });

  it("matches every deterministic validation vector without silently scoring unresolved items", () => {
    for (const testCase of quizValidationCasesV1) {
      const projection = projectQuizGradingItems([
        {
          itemId: "q1",
          entry: testCase.entry,
        },
      ]);
      const evaluate = (answer: typeof testCase.answers.correct | null) => {
        const envelope = createQuizAnswerEnvelope(answer ? { q1: answer } : {});
        return evaluateDeterministicQuiz(
          projection,
          decodeQuizAnswerEnvelope(envelope, projection),
        );
      };

      const correct = evaluate(testCase.answers.correct);
      if (testCase.officialCapability === "automated-review") {
        expect(correct.state, testCase.id).toBe(
          testCase.expected.correct.resultState,
        );
        expect(correct.score, testCase.id).toBe(
          testCase.expected.correct.score,
        );
        expect(correct.items[0]?.state, testCase.id).toBe("graded");

        const omitted = evaluate(null);
        expect(omitted.score, `${testCase.id} omitted`).toBe(0);
        if (testCase.answers.incorrect && testCase.expected.incorrect) {
          expect(
            evaluate(testCase.answers.incorrect).score,
            `${testCase.id} incorrect`,
          ).toBe(testCase.expected.incorrect.score);
        }
        if (testCase.answers.partial && testCase.expected.partial) {
          expect(
            evaluate(testCase.answers.partial).score,
            `${testCase.id} partial`,
          ).toBe(testCase.expected.partial.score);
        }
      } else {
        expect(correct.state, testCase.id).toBe("partial");
        expect(correct.score, testCase.id).toBeNull();
        expect(correct.items[0]?.score, testCase.id).toBeNull();
        expect(correct.items[0]?.state, testCase.id).not.toBe("graded");
      }
    }
  });
});

function privateAuthoringFields(type: string): readonly string[] {
  switch (type) {
    case QuizEntryType.SingleChoice:
      return ["correctOptionId"];
    case QuizEntryType.MultipleChoice:
      return ["correctOptionIds"];
    case QuizEntryType.TrueFalse:
      return ["correctAnswer"];
    case QuizEntryType.FillInTheBlank:
      return ["acceptedAnswers", "correctValue"];
    case QuizEntryType.ShortAnswer:
      return ["acceptedAnswers"];
    case QuizEntryType.Essay:
      return ["correctAnswer", "correctAnswerPlain"];
    case QuizEntryType.Matching:
      return ["right"];
    case QuizEntryType.Ordering:
      return ["correctPosition"];
    case QuizEntryType.Categorization:
      return ["correctCategoryIds"];
    case QuizEntryType.Rating:
      return ["correctRating"];
    case QuizEntryType.Numeric:
      return ["toleranceType", "tolerance"];
    case QuizEntryType.Formula:
      return ["formula", "toleranceType", "tolerance"];
    case QuizEntryType.Hotspot:
      return ["hotspots"];
    case QuizEntryType.Highlight:
      return ["sourceText", "highlights"];
    default:
      return [];
  }
}
