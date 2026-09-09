import type {
  AssessmentExecutionDeliveryV1,
  AssessmentItemManifestV1,
} from "@game-guild/grading";
import { toQuizLearnerEntry } from "@game-guild/quiz";
import {
  QUIZ_ASSESSMENT_TYPE_ADAPTER,
  type QuizItemProjectionV1,
  type QuizLearnerDeliveryItemV1,
} from "./contracts";

export function createQuizItemManifest(
  items: readonly QuizItemProjectionV1[],
): AssessmentItemManifestV1[] {
  assertProjectedItems(items);
  return items.map(({ itemId, itemType }) => ({
    itemId,
    itemType,
    adapterKey: QUIZ_ASSESSMENT_TYPE_ADAPTER.key,
    adapterVersion: QUIZ_ASSESSMENT_TYPE_ADAPTER.version,
  }));
}

export function createQuizExecutionDelivery(
  definitionRevisionId: string,
  executionSnapshotHash: string,
  items: readonly QuizItemProjectionV1[],
  itemOrder: readonly string[] = items.map(({ itemId }) => itemId),
): AssessmentExecutionDeliveryV1<QuizLearnerDeliveryItemV1> {
  assertProjectedItems(items);
  assertItemOrder(items, itemOrder);
  const byId = new Map(items.map((item) => [item.itemId, item]));
  return {
    schemaVersion: 1,
    definitionRevisionId,
    executionSnapshotHash,
    itemOrder: [...itemOrder],
    items: Object.fromEntries(itemOrder.map((itemId) => {
      const item = byId.get(itemId)!;
      return [itemId, {
        adapterKey: QUIZ_ASSESSMENT_TYPE_ADAPTER.key,
        adapterVersion: QUIZ_ASSESSMENT_TYPE_ADAPTER.version,
        learnerPayload: {
          itemId,
          entry: toQuizLearnerEntry(item.authoringEntry),
        },
      }];
    })),
  };
}

function assertItemOrder(
  items: readonly QuizItemProjectionV1[],
  itemOrder: readonly string[],
): void {
  const expected = new Set(items.map(({ itemId }) => itemId));
  const actual = new Set(itemOrder);
  if (actual.size !== itemOrder.length || actual.size !== expected.size) {
    throw new TypeError("Quiz delivery itemOrder must contain every item exactly once.");
  }
  for (const itemId of actual) {
    if (!expected.has(itemId)) throw new TypeError(`Quiz delivery itemOrder contains unknown item ${itemId}.`);
  }
}

function assertProjectedItems(items: readonly QuizItemProjectionV1[]): void {
  const ids = new Set<string>();
  for (const item of items) {
    if (!item.itemId.trim()) throw new TypeError("Quiz projected item IDs must be non-empty.");
    if (ids.has(item.itemId)) throw new TypeError(`Duplicate quiz projected item ID: ${item.itemId}.`);
    ids.add(item.itemId);
  }
}
