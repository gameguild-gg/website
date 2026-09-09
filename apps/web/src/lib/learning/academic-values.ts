import {
  formatPercentValue,
  formatScoreValue,
  parsePercentValue,
  parseScoreValue,
  percentValueFromPercentage,
  scoreValueFromPoints,
  type PercentValue,
  type ScoreValue,
} from "@game-guild/grading";

export function scoreUnitsToPoints(value: unknown): number {
  return Number(formatScoreValue(parseScoreValue(value)));
}

export function optionalScoreUnitsToPoints(
  value: unknown,
): number | null {
  return value == null ? null : scoreUnitsToPoints(value);
}

export function percentUnitsToPercentage(value: unknown): number {
  return Number(formatPercentValue(parsePercentValue(value)));
}

export function optionalPercentUnitsToPercentage(
  value: unknown,
): number | null {
  return value == null ? null : percentUnitsToPercentage(value);
}

export function pointsToScoreUnits(value: number | string): ScoreValue {
  return scoreValueFromPoints(String(value));
}

export function percentageToPercentUnits(
  value: number | string,
): PercentValue {
  return percentValueFromPercentage(String(value));
}

export function rubricScoresToUnits(rubricScores: string): string {
  const parsed: unknown = JSON.parse(rubricScores);
  if (!isRecord(parsed)) {
    throw new TypeError("Rubric scores must be an object keyed by criterion ID.");
  }

  return JSON.stringify(
    Object.fromEntries(
      Object.entries(parsed).map(([criterionId, rawEntry]) => {
        if (!isRecord(rawEntry) || typeof rawEntry.points !== "number") {
          throw new TypeError(
            `Rubric score ${criterionId} must contain numeric points.`,
          );
        }

        return [
          criterionId,
          {
            ...rawEntry,
            points: pointsToScoreUnits(rawEntry.points),
          },
        ];
      }),
    ),
  );
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
