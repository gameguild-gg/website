import {
  ASSESSMENT_RESPONSE_SCHEMA_VERSION,
  type AssessmentResponseEnvelopeV1,
} from "@game-guild/grading";
import { parseQuizAnswer, type QuizAnswer } from "@game-guild/quiz";
import {
  QUIZ_ANSWER_PAYLOAD_SCHEMA,
  QUIZ_CONTENT_TYPE,
  type QuizAnswerEnvelopeV1,
  type QuizAnswerPayloadV1,
  type QuizItemProjectionV1,
} from "./contracts";

const ENVELOPE_KEYS = new Set(["schemaVersion", "contentType", "payloadSchema", "payload"]);
const PAYLOAD_KEYS = new Set(["answers"]);

export function createQuizAnswerEnvelope(
  answers: Readonly<Record<string, QuizAnswer>>,
): QuizAnswerEnvelopeV1 {
  return parseQuizAnswerEnvelope({
    schemaVersion: ASSESSMENT_RESPONSE_SCHEMA_VERSION,
    contentType: QUIZ_CONTENT_TYPE,
    payloadSchema: QUIZ_ANSWER_PAYLOAD_SCHEMA,
    payload: { answers },
  });
}

export function parseQuizAnswerEnvelope(value: unknown): QuizAnswerEnvelopeV1 {
  const envelope = asRecord(value);
  if (!envelope) throw new TypeError("Quiz answer envelope must be an object.");
  assertOnlyKeys(envelope, ENVELOPE_KEYS, "Quiz answer envelope");
  if (envelope.schemaVersion !== ASSESSMENT_RESPONSE_SCHEMA_VERSION) {
    throw new TypeError(`Quiz answer envelope schemaVersion must be ${ASSESSMENT_RESPONSE_SCHEMA_VERSION}.`);
  }
  if (envelope.contentType !== QUIZ_CONTENT_TYPE) {
    throw new TypeError(`Quiz answer envelope contentType must be ${QUIZ_CONTENT_TYPE}.`);
  }
  if (envelope.payloadSchema !== QUIZ_ANSWER_PAYLOAD_SCHEMA) {
    throw new TypeError(`Unsupported quiz answer payload schema: ${String(envelope.payloadSchema)}.`);
  }

  const payload = asRecord(envelope.payload);
  if (!payload) throw new TypeError("Quiz answer payload must be an object.");
  assertOnlyKeys(payload, PAYLOAD_KEYS, "Quiz answer payload");
  const sourceAnswers = asRecord(payload.answers);
  if (!sourceAnswers) throw new TypeError("Quiz answer payload answers must be an object.");

  const answers: Record<string, QuizAnswer> = {};
  for (const [itemId, answer] of Object.entries(sourceAnswers)) {
    if (!itemId.trim()) throw new TypeError("Quiz answer item IDs must be non-empty.");
    answers[itemId] = parseQuizAnswer(answer);
  }

  return {
    schemaVersion: ASSESSMENT_RESPONSE_SCHEMA_VERSION,
    contentType: QUIZ_CONTENT_TYPE,
    payloadSchema: QUIZ_ANSWER_PAYLOAD_SCHEMA,
    payload: { answers },
  };
}

export function decodeQuizAnswerEnvelope(
  envelope: AssessmentResponseEnvelopeV1,
  items: readonly QuizItemProjectionV1[],
): QuizAnswerPayloadV1 {
  const parsed = parseQuizAnswerEnvelope(envelope);
  const expected = new Map(items.map(({ itemId, itemType }) => [itemId, itemType]));
  for (const [itemId, answer] of Object.entries(parsed.payload.answers)) {
    const expectedType = expected.get(itemId);
    if (!expectedType) throw new TypeError(`Quiz answer references unknown item ${itemId}.`);
    if (answer.type !== expectedType) {
      throw new TypeError(`Quiz answer type for ${itemId} does not match its question type.`);
    }
  }
  return parsed.payload;
}

function assertOnlyKeys(value: Record<string, unknown>, allowed: Set<string>, label: string): void {
  const unknown = Object.keys(value).filter((key) => !allowed.has(key));
  if (unknown.length > 0) throw new TypeError(`${label} contains unknown fields: ${unknown.join(", ")}.`);
}

function asRecord(value: unknown): Record<string, unknown> | null {
  return value && typeof value === "object" && !Array.isArray(value)
    ? value as Record<string, unknown>
    : null;
}
