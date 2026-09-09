import {
  ASSESSMENT_EXECUTION_DELIVERY_SCHEMA_VERSION,
  ASSESSMENT_EXECUTION_MANIFEST_SCHEMA_VERSION,
  ASSESSMENT_EXECUTION_POLICY_SCHEMA_VERSION,
  GradingContractValidationError,
  type AssessmentExecutionDeliveryV1,
  type AssessmentExecutionManifestV1,
  type AssessmentExecutionPolicyV1,
  type AssessmentExecutionSnapshotV1,
  type AssessmentItemProjectionV1,
} from "./types";
import {
  parseReviewMethods,
  reviewMethodsToSequence,
  reviewSequenceToMethods,
  type AssessmentReviewMethod,
  type ReviewMethods,
} from "./review-methods";
import { parseScoreValue } from "./values";
import { validateContentGradingDefinition } from "./config";

const UTC_INSTANT_PATTERN = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/;
const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const SHA256_PATTERN = /^[0-9a-f]{64}$/;
const REVIEW_METHODS = new Set<AssessmentReviewMethod>([
  "PeerReview",
  "AIReview",
  "AutomatedReview",
  "InstructorReview",
  "SelfReview",
]);

export function validateAssessmentExecutionPolicy(value: unknown): AssessmentExecutionPolicyV1 {
  const root = exactRecord(value, [
    "schemaVersion", "passingScore", "maxAttempts", "attemptContribution",
    "timeLimitMinutes", "availability", "completion", "resultRelease",
    "presentation", "review",
  ], "AssessmentExecutionPolicyV1");
  requireLiteral(root.schemaVersion, ASSESSMENT_EXECUTION_POLICY_SCHEMA_VERSION, "policy.schemaVersion");
  if (root.passingScore !== undefined) parseScoreValue(root.passingScore);
  const maxAttempts = optionalPositiveInteger(root.maxAttempts, "policy.maxAttempts");
  optionalPositiveInteger(root.timeLimitMinutes, "policy.timeLimitMinutes");

  const contribution = root.attemptContribution === undefined
    ? undefined
    : exactRecord(root.attemptContribution, ["mode"], "policy.attemptContribution");
  if (contribution && !["first-finalized", "last-finalized", "highest-finalized"].includes(String(contribution.mode))) {
    fail("policy.attemptContribution.mode is unsupported.");
  }
  if ((maxAttempts ?? 1) > 1 && !contribution) {
    fail("policy.attemptContribution is required when maxAttempts is greater than 1.");
  }

  validateAvailability(root.availability);
  const completion = exactRecord(root.completion, ["mode"], "policy.completion");
  if (!["on-submit", "on-finalize", "on-release", "on-release-and-pass"].includes(String(completion.mode))) {
    fail("policy.completion.mode is unsupported.");
  }
  validateRelease(root.resultRelease);
  const presentation = exactRecord(root.presentation, ["mode"], "policy.presentation");
  if (presentation.mode !== "continuous" && presentation.mode !== "single-step") {
    fail("policy.presentation.mode is unsupported.");
  }
  validateReviewPolicy(root.review);
  return structuredClone(root) as unknown as AssessmentExecutionPolicyV1;
}

export function validateAssessmentExecutionManifest(value: unknown): AssessmentExecutionManifestV1 {
  const root = exactRecord(value, ["schemaVersion", "items", "stages", "policies"], "AssessmentExecutionManifestV1");
  requireLiteral(root.schemaVersion, ASSESSMENT_EXECUTION_MANIFEST_SCHEMA_VERSION, "manifest.schemaVersion");
  const items = requireArray(root.items, "manifest.items");
  const itemIds = new Set<string>();
  for (const [index, rawItem] of items.entries()) {
    const item = exactRecord(rawItem, [
      "itemId", "itemType", "adapterKey", "adapterVersion",
    ], `manifest.items[${index}]`);
    const itemId = nonEmptyString(item.itemId, `manifest.items[${index}].itemId`);
    if (itemIds.has(itemId)) fail(`manifest contains duplicate itemId ${itemId}.`);
    itemIds.add(itemId);
    for (const key of [
      "itemType", "adapterKey", "adapterVersion",
    ] as const) nonEmptyString(item[key], `manifest.items[${index}].${key}`);
  }

  const stages = requireArray(root.stages, "manifest.stages");
  const sequence = stages.map((rawStage, index) => {
    const stage = exactRecord(rawStage, [
      "method", "handlerKey", "handlerVersion", "providerKey", "providerPolicyVersion",
    ], `manifest.stages[${index}]`);
    const method = stage.method;
    if (typeof method !== "string" || !REVIEW_METHODS.has(method as AssessmentReviewMethod)) {
      fail(`manifest.stages[${index}].method is unsupported.`);
    }
    nonEmptyString(stage.handlerKey, `manifest.stages[${index}].handlerKey`);
    nonEmptyString(stage.handlerVersion, `manifest.stages[${index}].handlerVersion`);
    pairedFields(stage, "providerKey", "providerPolicyVersion", `manifest.stages[${index}]`);
    return method as AssessmentReviewMethod;
  });
  reviewSequenceToMethods(sequence);

  const policies = requireArray(root.policies, "manifest.policies");
  const policyKeys = new Set<string>();
  for (const [index, rawPolicy] of policies.entries()) {
    const policy = exactRecord(rawPolicy, ["policyKey", "policyVersion"], `manifest.policies[${index}]`);
    const policyKey = nonEmptyString(policy.policyKey, `manifest.policies[${index}].policyKey`);
    const policyVersion = nonEmptyString(policy.policyVersion, `manifest.policies[${index}].policyVersion`);
    const identity = `${policyKey}@${policyVersion}`;
    if (policyKeys.has(identity)) fail(`manifest contains duplicate policy ${identity}.`);
    policyKeys.add(identity);
  }
  return structuredClone(root) as unknown as AssessmentExecutionManifestV1;
}

export function validateAssessmentExecutionSnapshot<
  TContent = unknown,
  TProjection extends AssessmentItemProjectionV1 = AssessmentItemProjectionV1,
>(
  value: unknown,
): AssessmentExecutionSnapshotV1<TContent, TProjection> {
  const root = exactRecord(value, ["schemaVersion", "authoringSource", "manifest", "itemProjections"], "AssessmentExecutionSnapshotV1");
  requireLiteral(root.schemaVersion, 1, "snapshot.schemaVersion");
  const authoring = exactRecord(root.authoringSource, ["schemaVersion", "contentType", "content", "grading", "policy"], "snapshot.authoringSource");
  requireLiteral(authoring.schemaVersion, 1, "snapshot.authoringSource.schemaVersion");
  const contentType = nonEmptyString(authoring.contentType, "snapshot.authoringSource.contentType");
  const grading = validateContentGradingDefinition(authoring.grading);
  const policy = validateAssessmentExecutionPolicy(authoring.policy);
  const manifest = validateAssessmentExecutionManifest(root.manifest);
  const projections = exactRecord(root.itemProjections, undefined, "snapshot.itemProjections");
  requireSameKeys(
    manifest.items.map(({ itemId }) => itemId),
    Object.keys(grading.items),
    "snapshot manifest items and authoring grading items",
  );
  requireSameKeys(
    manifest.items.map(({ itemId }) => itemId),
    Object.keys(projections),
    "snapshot.itemProjections and manifest items",
  );

  const configuredStages = reviewMethodsToSequence(policy.review.methods);
  const manifestStages = manifest.stages.map(({ method }) => method);
  if (configuredStages.length !== manifestStages.length ||
      configuredStages.some((method, index) => method !== manifestStages[index])) {
    fail("snapshot manifest stages must exactly match policy.review.methods in canonical order.");
  }

  for (const item of manifest.items) {
    validateProjectionBinding(projections[item.itemId], item.itemId, item.itemType, contentType);
  }
  validateStageBindings(manifest, policy);
  return structuredClone(root) as unknown as AssessmentExecutionSnapshotV1<TContent, TProjection>;
}

export function validateAssessmentExecutionDelivery<TPayload = unknown>(
  value: unknown,
): AssessmentExecutionDeliveryV1<TPayload> {
  const root = exactRecord(value, [
    "schemaVersion", "definitionRevisionId", "executionSnapshotHash", "itemOrder", "items",
  ], "AssessmentExecutionDeliveryV1");
  requireLiteral(root.schemaVersion, ASSESSMENT_EXECUTION_DELIVERY_SCHEMA_VERSION, "delivery.schemaVersion");
  uuid(root.definitionRevisionId, "delivery.definitionRevisionId");
  sha256(root.executionSnapshotHash, "delivery.executionSnapshotHash");
  const order = requireArray(root.itemOrder, "delivery.itemOrder")
    .map((itemId, index) => nonEmptyString(itemId, `delivery.itemOrder[${index}]`));
  if (new Set(order).size !== order.length) fail("delivery.itemOrder cannot contain duplicates.");
  const items = exactRecord(root.items, undefined, "delivery.items");
  if (order.length !== Object.keys(items).length || order.some((itemId) => !(itemId in items))) {
    fail("delivery.itemOrder must contain every delivery item exactly once.");
  }
  for (const [itemId, rawItem] of Object.entries(items)) {
    const item = exactRecord(rawItem, ["adapterKey", "adapterVersion", "learnerPayload"], `delivery.items.${itemId}`);
    nonEmptyString(item.adapterKey, `delivery.items.${itemId}.adapterKey`);
    nonEmptyString(item.adapterVersion, `delivery.items.${itemId}.adapterVersion`);
    if (!("learnerPayload" in item)) fail(`delivery.items.${itemId}.learnerPayload is required.`);
  }
  return structuredClone(root) as unknown as AssessmentExecutionDeliveryV1<TPayload>;
}

function validateAvailability(value: unknown): void {
  const availability = exactRecord(value, [
    "availableFrom", "availableUntil", "dueAt", "allowLateSubmissions", "lateSubmissionDeadline",
  ], "policy.availability");
  if (typeof availability.allowLateSubmissions !== "boolean") fail("policy.availability.allowLateSubmissions must be boolean.");
  for (const key of ["availableFrom", "availableUntil", "dueAt", "lateSubmissionDeadline"] as const) {
    if (availability[key] !== undefined && availability[key] !== null) utcInstant(availability[key], `policy.availability.${key}`);
  }
  if (!availability.allowLateSubmissions && availability.lateSubmissionDeadline != null) {
    fail("lateSubmissionDeadline requires allowLateSubmissions.");
  }
}

function validateRelease(value: unknown): void {
  const release = exactRecord(value, undefined, "policy.resultRelease");
  if (release.mode === "scheduled") {
    exactKeys(release, ["mode", "scheduledFor"], "policy.resultRelease");
    utcInstant(release.scheduledFor, "policy.resultRelease.scheduledFor");
    return;
  }
  if (release.mode !== "immediate" && release.mode !== "manual") fail("policy.resultRelease.mode is unsupported.");
  exactKeys(release, ["mode"], "policy.resultRelease");
}

function validateReviewPolicy(value: unknown): ReviewMethods {
  const review = exactRecord(value, ["schemaVersion", "methods", "peer", "ai", "self", "instructor"], "policy.review");
  requireLiteral(review.schemaVersion, 1, "policy.review.schemaVersion");
  const methods = parseReviewMethods(review.methods);
  const allowedConfig = new Map<string, number>([
    ["peer", 1], ["ai", 2], ["self", 16], ["instructor", 8],
  ]);
  for (const [key, flag] of allowedConfig) {
    if (review[key] !== undefined && (methods & flag) === 0) fail(`policy.review.${key} requires its review method.`);
    if ((methods & flag) !== 0 && review[key] === undefined) fail(`policy.review.${key} is required by its review method.`);
  }

  if (review.peer !== undefined) {
    const peer = exactRecord(review.peer, [
      "reviewsPerReviewer", "reviewsRequiredPerSubmission", "minimumReviewsToFinalize",
      "aggregation", "claimLeaseMinutes", "evidenceWindowMinutes", "onInsufficientEvidence",
    ], "policy.review.peer");
    const reviewsPerReviewer = positiveInteger(peer.reviewsPerReviewer, "policy.review.peer.reviewsPerReviewer");
    const reviewsRequired = positiveInteger(peer.reviewsRequiredPerSubmission, "policy.review.peer.reviewsRequiredPerSubmission");
    const minimumReviews = positiveInteger(peer.minimumReviewsToFinalize, "policy.review.peer.minimumReviewsToFinalize");
    if (minimumReviews > reviewsRequired) {
      fail("policy.review.peer.minimumReviewsToFinalize cannot exceed reviewsRequiredPerSubmission.");
    }
    if (reviewsPerReviewer > reviewsRequired) {
      fail("policy.review.peer.reviewsPerReviewer cannot exceed reviewsRequiredPerSubmission.");
    }
    if (peer.aggregation !== "mean" && peer.aggregation !== "median") {
      fail("policy.review.peer.aggregation is unsupported.");
    }
    positiveInteger(peer.claimLeaseMinutes, "policy.review.peer.claimLeaseMinutes");
    positiveInteger(peer.evidenceWindowMinutes, "policy.review.peer.evidenceWindowMinutes");
    if (peer.onInsufficientEvidence !== "await-instructor-resolution") {
      fail("policy.review.peer.onInsufficientEvidence is unsupported.");
    }
  }

  if (review.ai !== undefined) {
    const ai = exactRecord(review.ai, ["providerKey", "policyVersion"], "policy.review.ai");
    nonEmptyString(ai.providerKey, "policy.review.ai.providerKey");
    nonEmptyString(ai.policyVersion, "policy.review.ai.policyVersion");
  }

  if (review.self !== undefined) {
    const self = exactRecord(review.self, ["instructions", "requireFeedback"], "policy.review.self");
    if (self.instructions !== undefined) nonEmptyString(self.instructions, "policy.review.self.instructions");
    requireBoolean(self.requireFeedback, "policy.review.self.requireFeedback");
  }

  if (review.instructor !== undefined) {
    const instructor = exactRecord(review.instructor, ["requireOverrideReason"], "policy.review.instructor");
    requireBoolean(instructor.requireOverrideReason, "policy.review.instructor.requireOverrideReason");
  }
  return methods;
}

function validateProjectionBinding(
  value: unknown,
  itemId: string,
  itemType: string,
  contentType: string,
): void {
  const label = `snapshot.itemProjections.${itemId}`;
  const projection = exactRecord(value, undefined, label);
  positiveInteger(projection.schemaVersion, `${label}.schemaVersion`);
  if (nonEmptyString(projection.itemId, `${label}.itemId`) !== itemId) {
    fail(`${label}.itemId must match its manifest item ID.`);
  }
  if (nonEmptyString(projection.itemType, `${label}.itemType`) !== itemType) {
    fail(`${label}.itemType must match its manifest item type.`);
  }
  parseScoreValue(projection.maxScore);

  const source = exactRecord(projection.source, ["contentType", "itemId"], `${label}.source`);
  if (nonEmptyString(source.contentType, `${label}.source.contentType`) !== contentType) {
    fail(`${label}.source.contentType must match the authoring content type.`);
  }
  if (nonEmptyString(source.itemId, `${label}.source.itemId`) !== itemId) {
    fail(`${label}.source.itemId must match its manifest item ID.`);
  }
}

function validateStageBindings(
  manifest: AssessmentExecutionManifestV1,
  policy: AssessmentExecutionPolicyV1,
): void {
  for (const [index, stage] of manifest.stages.entries()) {
    const label = `manifest.stages[${index}]`;
    if (stage.method === "AIReview") {
      const ai = policy.review.ai;
      if (!ai || stage.providerKey !== ai.providerKey ||
          stage.providerPolicyVersion !== ai.policyVersion) {
        fail(`${label} provider must exactly match policy.review.ai.`);
      }
    } else if (stage.providerKey || stage.providerPolicyVersion) {
      fail(`${label} may only fix a provider for AIReview.`);
    }
  }
}

function requireSameKeys(expected: readonly string[], actual: readonly string[], label: string): void {
  const expectedSet = new Set(expected);
  const actualSet = new Set(actual);
  if (expectedSet.size !== actualSet.size || [...expectedSet].some((key) => !actualSet.has(key))) {
    fail(`${label} must contain the same item IDs exactly once.`);
  }
}

function exactRecord(value: unknown, keys: readonly string[] | undefined, label: string): Record<string, unknown> {
  if (!value || typeof value !== "object" || Array.isArray(value)) fail(`${label} must be an object.`);
  const record = value as Record<string, unknown>;
  if (keys) exactKeys(record, keys, label);
  return record;
}

function exactKeys(record: Record<string, unknown>, keys: readonly string[], label: string): void {
  const allowed = new Set(keys);
  const unknown = Object.keys(record).find((key) => !allowed.has(key));
  if (unknown) fail(`${label}.${unknown} is not allowed.`);
}

function requireArray(value: unknown, label: string): unknown[] {
  if (!Array.isArray(value)) fail(`${label} must be an array.`);
  return value;
}

function nonEmptyString(value: unknown, label: string): string {
  if (typeof value !== "string" || !value.trim()) fail(`${label} must be a non-empty string.`);
  return value;
}

function uuid(value: unknown, label: string): string {
  const parsed = nonEmptyString(value, label);
  if (!UUID_PATTERN.test(parsed) || parsed === "00000000-0000-0000-0000-000000000000") {
    fail(`${label} must be a canonical non-empty UUID.`);
  }
  return parsed;
}

function sha256(value: unknown, label: string): string {
  const parsed = nonEmptyString(value, label);
  if (!SHA256_PATTERN.test(parsed)) fail(`${label} must be a lowercase SHA-256 hash.`);
  return parsed;
}

function optionalPositiveInteger(value: unknown, label: string): number | undefined {
  if (value === undefined) return undefined;
  return positiveInteger(value, label);
}

function positiveInteger(value: unknown, label: string): number {
  if (typeof value !== "number" || !Number.isInteger(value) || value <= 0) fail(`${label} must be a positive integer.`);
  return value;
}

function requireBoolean(value: unknown, label: string): boolean {
  if (typeof value !== "boolean") fail(`${label} must be boolean.`);
  return value;
}

function utcInstant(value: unknown, label: string): string {
  if (typeof value !== "string" || !UTC_INSTANT_PATTERN.test(value) || Number.isNaN(Date.parse(value))) {
    fail(`${label} must be a canonical UTC instant with millisecond precision.`);
  }
  return value;
}

function requireLiteral(value: unknown, expected: number, label: string): void {
  if (value !== expected) fail(`${label} must be ${expected}.`);
}

function pairedFields(
  record: Record<string, unknown>,
  first: string,
  second: string,
  label: string,
): void {
  if ((record[first] === undefined) !== (record[second] === undefined)) fail(`${label}.${first} and ${second} must be provided together.`);
  if (record[first] !== undefined) {
    nonEmptyString(record[first], `${label}.${first}`);
    nonEmptyString(record[second], `${label}.${second}`);
  }
}

function fail(message: string): never {
  throw new GradingContractValidationError([message]);
}
