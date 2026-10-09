import { describe, expect, it } from "vitest";
import { QuizEntryType, createTrueFalseEntry } from "@game-guild/quiz";
import { QUIZ_BLOCK_TYPE, QUIZ_CONTENT_SCHEMA_VERSION } from "./constants";
import {
  isQuizRuntimeContentDocument,
  parseQuizLearnerContentDocument,
  prepareQuizContentForRuntime,
  toQuizLearnerContentDocument,
} from "./learner";
import { createAllQuestionTypesDocument } from "./testing/fixtures";

describe("quiz learner content", () => {
  it("redacts answer keys without mutating the authoring document", () => {
    const entry = createTrueFalseEntry("Question");
    entry.correctAnswer = true;
    const authoring = {
      schemaVersion: QUIZ_CONTENT_SCHEMA_VERSION,
      order: [["1", QUIZ_BLOCK_TYPE]] as const,
      blocks: { "1": entry },
    };

    const learner = toQuizLearnerContentDocument({
      ...authoring,
      order: [...authoring.order],
    });

    expect(learner.blocks["1"]?.type).toBe(QuizEntryType.TrueFalse);
    expect(learner.blocks["1"]).not.toHaveProperty("correctAnswer");
    expect(authoring.blocks["1"]).toHaveProperty("correctAnswer", true);
  });

  it("projects every supported question type through the document boundary", () => {
    const authoring = {
      ...createAllQuestionTypesDocument(),
      grading: { schemaVersion: 2 as const, items: {} },
    };
    const learner = toQuizLearnerContentDocument(authoring);

    expect(learner.order).toEqual(authoring.order);
    expect(Object.keys(learner.blocks)).toHaveLength(14);
    expect(learner).not.toHaveProperty("grading");

    for (const [id] of learner.order) {
      const entry = learner.blocks[id]!;
      const feedback = entry.feedback as Record<string, unknown> | undefined;
      expect(feedback?.correct).toBeUndefined();
      expect(feedback?.incorrect).toBeUndefined();
      expect(
        (entry.attachments as Record<string, unknown> | undefined)?.authorOnly,
      ).toBeUndefined();

      switch (entry.type) {
        case QuizEntryType.SingleChoice:
          expect(entry).not.toHaveProperty("correctOptionId");
          break;
        case QuizEntryType.MultipleChoice:
          expect(entry).not.toHaveProperty("correctOptionIds");
          break;
        case QuizEntryType.TrueFalse:
          expect(entry).not.toHaveProperty("correctAnswer");
          break;
        case QuizEntryType.FillInTheBlank:
          expect(entry.blanks[0]?.input).not.toHaveProperty("acceptedAnswers");
          expect(entry.blanks[0]?.input).not.toHaveProperty("correctValue");
          break;
        case QuizEntryType.ShortAnswer:
          expect(entry).not.toHaveProperty("acceptedAnswers");
          break;
        case QuizEntryType.Essay:
          expect(entry).not.toHaveProperty("correctAnswer");
          expect(entry).not.toHaveProperty("correctAnswerPlain");
          break;
        case QuizEntryType.Matching:
          expect(entry.pairs[0]).not.toHaveProperty("right");
          break;
        case QuizEntryType.Ordering:
          expect(entry.items[0]).not.toHaveProperty("correctPosition");
          break;
        case QuizEntryType.Categorization:
          expect(entry.items[0]).not.toHaveProperty("correctCategoryIds");
          break;
        case QuizEntryType.Rating:
          expect(entry).not.toHaveProperty("correctRating");
          break;
        case QuizEntryType.Numeric:
          expect(entry).not.toHaveProperty("tolerance");
          expect(entry).not.toHaveProperty("toleranceType");
          break;
        case QuizEntryType.Formula:
          expect(entry).not.toHaveProperty("formula");
          expect(entry).not.toHaveProperty("tolerance");
          break;
        case QuizEntryType.Hotspot:
          expect(entry).not.toHaveProperty("hotspots");
          break;
        case QuizEntryType.Highlight:
          expect(entry).not.toHaveProperty("sourceText");
          expect(entry).not.toHaveProperty("highlights");
          break;
      }
    }
  });

  it("recognizes only explicit runtime envelopes", () => {
    const document = createAllQuestionTypesDocument();
    const runtime = prepareQuizContentForRuntime(document, "server-graded");

    expect(isQuizRuntimeContentDocument(runtime)).toBe(true);
    expect(isQuizRuntimeContentDocument(runtime.document)).toBe(false);
    expect(
      isQuizRuntimeContentDocument({
        mode: "server-graded",
        document: { order: [] },
      }),
    ).toBe(false);
  });

  it("parses the redacted learner document for every supported question type", () => {
    const learner = toQuizLearnerContentDocument(
      createAllQuestionTypesDocument(),
    );
    const parsed = parseQuizLearnerContentDocument(learner);

    expect(parsed.issues).toEqual([]);
    expect(parsed.document).toEqual(learner);
  });

  it("rejects answer keys at the learner document boundary", () => {
    const learner = toQuizLearnerContentDocument(
      createAllQuestionTypesDocument(),
    );
    const [firstId] = learner.order.find(
      ([id]) => learner.blocks[id]?.type === QuizEntryType.TrueFalse,
    )!;
    const leaked = structuredClone(learner) as unknown as {
      blocks: Record<string, Record<string, unknown>>;
    };
    leaked.blocks[firstId]!.correctAnswer = true;

    expect(parseQuizLearnerContentDocument(leaked).issues).toEqual([
      expect.objectContaining({
        code: "invalid-quiz-entry",
        path: `blocks.${firstId}`,
      }),
    ]);
  });
});
