import type { AssessmentSubmissionRuntimeViewV1 } from '@game-guild/grading';
import type { CodingAssignmentContent } from './types';

/** Selects the immutable Code item; current mutable authoring is never a fallback. */
export function readCodeRuntimeDefinition(
  submission: AssessmentSubmissionRuntimeViewV1,
  instructor = false,
): CodingAssignmentContent {
  const delivery = submission.execution.delivery;
  if (delivery.itemOrder.length !== 1 || delivery.itemOrder[0] !== 'code') {
    throw new Error('The Code attempt has an invalid delivery.');
  }
  const item = delivery.items.code;
  if (!item || item.adapterKey !== 'code-assessment-type' || item.adapterVersion !== '1') {
    throw new Error('The Code attempt requires an unavailable adapter version.');
  }
  const payload = item.learnerPayload as { definition?: CodingAssignmentContent };
  const definition = instructor
    ? submission.execution.instructorVisibleContent as CodingAssignmentContent | null | undefined
    : payload.definition;
  if (!definition || definition.Type !== 'coding-assignment' || definition.Version !== 1 ||
      !definition.Environment || !definition.Data?.Files || !definition.Tests || !definition.Grading) {
    throw new Error('The frozen Code definition is unavailable.');
  }
  return definition;
}
