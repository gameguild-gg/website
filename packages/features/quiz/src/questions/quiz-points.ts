export const QUIZ_POINTS_SCALE = 100 as const;
export const MAX_QUIZ_POINTS_UNITS = 2_147_483_647 as const;
export const DEFAULT_QUIZ_POINTS = 100 as QuizPoints;

declare const quizPointsBrand: unique symbol;
export type QuizPoints = number & { readonly [quizPointsBrand]: "QuizPoints" };

export function parseQuizPoints(value: unknown): QuizPoints {
  if (
    typeof value !== "number" ||
    !Number.isSafeInteger(value) ||
    value < 0 ||
    value > MAX_QUIZ_POINTS_UNITS
  ) {
    throw new TypeError(`Quiz points must be an integer between 0 and ${MAX_QUIZ_POINTS_UNITS}.`);
  }
  return value as QuizPoints;
}

export function quizPointsFromPoints(value: string): QuizPoints {
  const match = /^(0|[1-9]\d*)(?:\.(\d{1,2}))?$/.exec(value.trim());
  if (!match) throw new TypeError("Quiz points must have at most two fractional digits.");
  const units = BigInt(match[1]!) * 100n + BigInt((match[2] ?? "").padEnd(2, "0") || "0");
  if (units > BigInt(MAX_QUIZ_POINTS_UNITS)) throw new RangeError("Quiz points are outside the supported range.");
  return Number(units) as QuizPoints;
}

export function formatQuizPoints(value: QuizPoints): string {
  const units = parseQuizPoints(value);
  const whole = Math.floor(units / QUIZ_POINTS_SCALE);
  const fraction = units % QUIZ_POINTS_SCALE;
  if (fraction === 0) return String(whole);
  if (fraction % 10 === 0) return `${whole}.${fraction / 10}`;
  return `${whole}.${String(fraction).padStart(2, "0")}`;
}
