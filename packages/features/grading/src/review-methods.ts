export const REVIEW_METHOD_FLAGS = {
  PeerReview: 1,
  AIReview: 2,
  AutomatedReview: 4,
  InstructorReview: 8,
  SelfReview: 16,
} as const;

export type AssessmentReviewMethod = keyof typeof REVIEW_METHOD_FLAGS;

export const VALID_REVIEW_METHOD_VALUES = [
  0, 1, 2, 4, 8, 9, 10, 12, 16, 24,
] as const;
export type ReviewMethodsValue = (typeof VALID_REVIEW_METHOD_VALUES)[number];

declare const reviewMethodsBrand: unique symbol;
export type ReviewMethods = ReviewMethodsValue & {
  readonly [reviewMethodsBrand]: "ReviewMethods";
};

const VALID_REVIEW_METHODS = new Set<number>(VALID_REVIEW_METHOD_VALUES);
const PRIMARY_METHODS: readonly AssessmentReviewMethod[] = [
  "PeerReview",
  "AIReview",
  "AutomatedReview",
  "SelfReview",
];

export function parseReviewMethods(
  value: unknown,
  options: { allowDraft?: boolean } = {},
): ReviewMethods {
  if (
    typeof value !== "number" ||
    !Number.isInteger(value) ||
    !VALID_REVIEW_METHODS.has(value)
  ) {
    throw new TypeError("ReviewMethods contains an unsupported workflow.");
  }
  if (value === 0 && !options.allowDraft) {
    throw new TypeError(
      "A published review workflow must select one review method.",
    );
  }
  return value as ReviewMethods;
}

export function createReviewMethods(
  primary: AssessmentReviewMethod,
  requireInstructorReview = false,
): ReviewMethods {
  const primaryValue = REVIEW_METHOD_FLAGS[primary];
  if (primary === "InstructorReview") {
    return parseReviewMethods(primaryValue);
  }
  return parseReviewMethods(
    primaryValue |
      (requireInstructorReview ? REVIEW_METHOD_FLAGS.InstructorReview : 0),
  );
}

export function reviewMethodsToSequence(
  value: ReviewMethods,
): AssessmentReviewMethod[] {
  const methods = parseReviewMethods(value, { allowDraft: true });
  if (methods === 0) return [];
  if (methods === REVIEW_METHOD_FLAGS.InstructorReview)
    return ["InstructorReview"];

  const primary = PRIMARY_METHODS.find(
    (method) => (methods & REVIEW_METHOD_FLAGS[method]) !== 0,
  );
  if (!primary)
    throw new TypeError("ReviewMethods has no primary review method.");
  return (methods & REVIEW_METHOD_FLAGS.InstructorReview) !== 0
    ? [primary, "InstructorReview"]
    : [primary];
}

export function reviewSequenceToMethods(
  sequence: readonly AssessmentReviewMethod[],
): ReviewMethods {
  if (sequence.length === 0) return parseReviewMethods(0, { allowDraft: true });
  if (sequence.length > 2)
    throw new TypeError("A review workflow supports at most two stages.");
  const [primary, final] = sequence;
  if (!primary)
    throw new TypeError("A review workflow requires a primary method.");
  if (final !== undefined && final !== "InstructorReview") {
    throw new TypeError("Only InstructorReview may be the final review stage.");
  }
  if (primary === "InstructorReview" && final !== undefined) {
    throw new TypeError(
      "InstructorReview cannot be followed by another stage.",
    );
  }
  return createReviewMethods(primary, final === "InstructorReview");
}

export function describeReviewWorkflow(value: ReviewMethods): string {
  const sequence = reviewMethodsToSequence(value);
  return sequence.length === 0
    ? "Draft without review workflow"
    : sequence.join(" -> ");
}
