import {
  createReviewMethods,
  parseReviewMethods,
  REVIEW_METHOD_FLAGS,
  reviewMethodsToSequence,
  type AssessmentReviewMethod,
  type ReviewMethods,
} from "@game-guild/grading";

export const ASSESSMENT_PRIMARY_REVIEW_METHODS = [
  "AutomatedReview",
  "InstructorReview",
  "PeerReview",
  "AIReview",
  "SelfReview",
] as const satisfies readonly AssessmentReviewMethod[];

export const AVAILABLE_PRIMARY_REVIEW_METHODS = [
  "AutomatedReview",
  "InstructorReview",
] as const satisfies readonly AssessmentReviewMethod[];

export const REVIEW_METHOD_LABELS: Record<AssessmentReviewMethod, string> = {
  AutomatedReview: "Automated review",
  InstructorReview: "Instructor review",
  PeerReview: "Peer review",
  AIReview: "AI review",
  SelfReview: "Self review",
};

export function readReviewWorkflow(value: unknown): {
  methods: ReviewMethods;
  primary: AssessmentReviewMethod | null;
  requiresInstructorReview: boolean;
} {
  const methods = parseReviewMethods(value, { allowDraft: true });
  const sequence = reviewMethodsToSequence(methods);
  return {
    methods,
    primary: sequence[0] ?? null,
    requiresInstructorReview:
      sequence.length === 2 && sequence[1] === "InstructorReview",
  };
}

export function buildReviewWorkflow(
  primary: AssessmentReviewMethod,
  requiresInstructorReview: boolean,
): ReviewMethods {
  return createReviewMethods(
    primary,
    primary !== "InstructorReview" && requiresInstructorReview,
  );
}

export function hasReviewMethod(
  methods: ReviewMethods,
  method: AssessmentReviewMethod,
): boolean {
  return (methods & REVIEW_METHOD_FLAGS[method]) !== 0;
}

export {
  REVIEW_METHOD_FLAGS,
  type AssessmentReviewMethod,
  type ReviewMethods,
};
