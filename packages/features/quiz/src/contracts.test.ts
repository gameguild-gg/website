import { describe, expect, it } from "vitest"
import {
  isCompleteQuizAuthoringEntry,
  toQuizLearnerEntry,
  validateQuizAuthoringEntry,
} from "./contracts/contracts"
import {
  FillBlankInputType,
  QuizEntryType,
  createEssayEntry,
  createHotspotEntry,
  type FillInTheBlankEntry,
  type FormulaEntry,
  type HighlightEntry,
  type MatchingEntry,
  type MultipleChoiceEntry,
  type SingleChoiceEntry,
} from "./questions/question-types"
import { createAssetUri } from "@game-guild/assets"

describe("quiz contracts", () => {
  it("rejects direct URLs in asset fields at runtime", () => {
    const entry = createHotspotEntry("Click the target")
    entry.imageAssetUri = "https://example.com/image.png" as never
    entry.attachments = {
      learnerVisible: [{
        assetUri: "data:image/png;base64,AAAA" as never,
        role: "question",
      }],
    }

    expect(validateQuizAuthoringEntry(entry)).toEqual(expect.arrayContaining([
      expect.objectContaining({ path: "imageAssetUri" }),
      expect.objectContaining({ path: "attachments.learnerVisible.0.assetUri" }),
    ]))
  })

  it("keeps learner-visible attachments and redacts author-only attachments", () => {
    const source: SingleChoiceEntry = {
      type: QuizEntryType.SingleChoice,
      stem: "Inspect the diagram.",
      options: [{ id: "a", text: "A" }],
      correctOptionId: "a",
      attachments: {
        learnerVisible: [{
          assetUri: createAssetUri("7776453f-1123-4f56-8abc-1234567890ab"),
          role: "question",
          altText: "Architecture diagram",
        }],
        authorOnly: [{
          assetUri: createAssetUri("8776453f-1123-4f56-8abc-1234567890ab"),
          role: "answer",
          label: "Answer key",
        }],
      },
      settings: { allowRetry: false },
    }

    const learnerEntry = toQuizLearnerEntry(source)
    expect(learnerEntry.attachments).toEqual({
      learnerVisible: [source.attachments!.learnerVisible![0]],
    })
    expect("authorOnly" in learnerEntry.attachments!).toBe(false)
  })

  it("redacts answer-key fields from authoring entries", () => {
    const source: MatchingEntry = {
      type: QuizEntryType.Matching,
      stem: "Match each country to its capital.",
      pairs: [
        { id: "fr", left: "France", right: "Paris" },
        { id: "it", left: "Italy", right: "Rome" },
      ],
      distractors: ["Madrid"],
      feedback: {
        correct: "Correct",
        incorrect: "Try again",
        general: "Review capitals.",
      },
      settings: {
        allowRetry: false,
        showFeedback: false,
        showCorrectAnswer: false,
      },
    }

    const learnerEntry = toQuizLearnerEntry(source)

    expect(learnerEntry).toEqual({
      type: QuizEntryType.Matching,
      stem: "Match each country to its capital.",
      pairs: [
        { id: "fr", left: "France" },
        { id: "it", left: "Italy" },
      ],
      rightOptions: ["Rome", "Madrid", "Paris"],
      feedback: {
        general: "Review capitals.",
      },
      settings: {
        allowRetry: false,
        showFeedback: false,
        showCorrectAnswer: false,
      },
    })
    expect("correct" in learnerEntry.feedback!).toBe(false)
    expect(learnerEntry.type).toBe(QuizEntryType.Matching)
    if (learnerEntry.type === QuizEntryType.Matching) {
      expect("right" in learnerEntry.pairs[0]!).toBe(false)
    }
  })

  it("redacts per-type answer keys that commonly leak through generic omission", () => {
    const fill: FillInTheBlankEntry = {
      type: QuizEntryType.FillInTheBlank,
      stem: "Capital: ___",
      blanks: [
        {
          id: "blank-1",
          position: 0,
          input: {
            type: FillBlankInputType.Text,
            acceptedAnswers: ["Paris"],
          },
        },
      ],
      settings: {
        allowRetry: false,
        showFeedback: false,
        showCorrectAnswer: false,
      },
    }
    const highlight: HighlightEntry = {
      type: QuizEntryType.Highlight,
      stem: "Highlight the target.",
      sourceText: "The __target__",
      plainText: "The target",
      highlights: [{ start: 4, end: 10 }],
      settings: {
        allowRetry: false,
        showFeedback: false,
        showCorrectAnswer: false,
      },
    }

    expect(toQuizLearnerEntry(fill)).toMatchObject({
      blanks: [{ input: { type: "TEXT" } }],
    })
    expect(toQuizLearnerEntry(highlight)).toEqual({
      type: QuizEntryType.Highlight,
      stem: "Highlight the target.",
      plainText: "The target",
      settings: {
        allowRetry: false,
        showFeedback: false,
        showCorrectAnswer: false,
      },
    })
  })

  it("preserves the multiple-choice selection count without exposing the answer key", () => {
    const source: MultipleChoiceEntry = {
      type: QuizEntryType.MultipleChoice,
      stem: "Choose the prime numbers.",
      options: [
        { id: "two", text: "2" },
        { id: "three", text: "3" },
        { id: "four", text: "4" },
        { id: "seven", text: "7" },
      ],
      correctOptionIds: ["two", "three", "seven"],
      settings: {
        allowRetry: false,
        showFeedback: false,
        showCorrectAnswer: false,
      },
    }

    const learnerEntry = toQuizLearnerEntry(source)

    expect(learnerEntry).toMatchObject({
      type: QuizEntryType.MultipleChoice,
      selectionLimit: 3,
    })
    expect("correctOptionIds" in learnerEntry).toBe(false)
  })

  it("redacts formula answer material without inventing server-generated prompts", () => {
    const source: FormulaEntry = {
      type: QuizEntryType.Formula,
      stem: "Find the formula.",
      variables: [{ id: "x", name: "x", min: 1, max: 10, decimals: 0 }],
      formula: "x * 2",
      toleranceType: "absolute",
      tolerance: 0,
      decimalPlaces: 0,
      settings: {
        allowRetry: false,
        showFeedback: false,
        showCorrectAnswer: false,
      },
    }

    const learnerEntry = toQuizLearnerEntry(source)

    expect(learnerEntry).toEqual({
      type: QuizEntryType.Formula,
      stem: "Find the formula.",
      variables: [{ id: "x", name: "x", min: 1, max: 10, decimals: 0 }],
      decimalPlaces: 0,
      settings: {
        allowRetry: false,
        showFeedback: false,
        showCorrectAnswer: false,
      },
    })
    expect("formula" in learnerEntry).toBe(false)
    expect("tolerance" in learnerEntry).toBe(false)
    expect("prompt" in learnerEntry).toBe(false)
  })

  it("validates source answer keys before grading contracts consume them", () => {
    const incompleteSingle: SingleChoiceEntry = {
      type: QuizEntryType.SingleChoice,
      stem: "Pick one.",
      options: [{ id: "a", text: "A" }],
      correctOptionId: "missing",
      settings: {
        allowRetry: true,
        showFeedback: true,
        showCorrectAnswer: true,
      },
    }
    const incompleteMultiple: MultipleChoiceEntry = {
      type: QuizEntryType.MultipleChoice,
      stem: "Pick many.",
      options: [{ id: "a", text: "A" }],
      correctOptionIds: [],
      settings: {
        allowRetry: true,
        showFeedback: true,
        showCorrectAnswer: true,
      },
    }

    expect(validateQuizAuthoringEntry(incompleteSingle)).toEqual([
      expect.objectContaining({
        code: "invalid-answer-key",
        path: "correctOptionId",
      }),
    ])
    expect(validateQuizAuthoringEntry(incompleteMultiple)).toEqual([
      expect.objectContaining({
        code: "missing-answer-key",
        path: "correctOptionIds",
      }),
    ])
    expect(isCompleteQuizAuthoringEntry(incompleteSingle)).toBe(false)
  })

  it("validates optional essay word limits before content serialization", () => {
    const invalidNumbers = createEssayEntry("Explain your reasoning.")
    invalidNumbers.minWordCount = Number.NaN
    invalidNumbers.maxWordCount = -1

    expect(validateQuizAuthoringEntry(invalidNumbers)).toEqual([
      expect.objectContaining({ path: "minWordCount" }),
      expect.objectContaining({ path: "maxWordCount" }),
    ])

    const invertedRange = createEssayEntry("Explain your reasoning.")
    invertedRange.minWordCount = 200
    invertedRange.maxWordCount = 100

    expect(validateQuizAuthoringEntry(invertedRange)).toEqual([
      expect.objectContaining({ path: "maxWordCount" }),
    ])
  })
})
