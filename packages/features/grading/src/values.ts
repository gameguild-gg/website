export const ACADEMIC_VALUE_SCALE = 100 as const;
export const MAX_SCORE_VALUE_UNITS = 2_147_483_647 as const;
export const MAX_PERCENT_VALUE_UNITS = 10_000 as const;
export const ZERO_SCORE_VALUE = 0 as ScoreValue;
export const ZERO_PERCENT_VALUE = 0 as PercentValue;
export const HUNDRED_PERCENT_VALUE = MAX_PERCENT_VALUE_UNITS as PercentValue;

declare const scoreValueBrand: unique symbol;
declare const percentValueBrand: unique symbol;

export type ScoreValue = number & { readonly [scoreValueBrand]: "ScoreValue" };
export type PercentValue = number & { readonly [percentValueBrand]: "PercentValue" };

export function parseScoreValue(value: unknown): ScoreValue {
  return parseUnits(value, MAX_SCORE_VALUE_UNITS, "ScoreValue") as ScoreValue;
}

export function parsePercentValue(value: unknown): PercentValue {
  return parseUnits(value, MAX_PERCENT_VALUE_UNITS, "PercentValue") as PercentValue;
}

export function scoreValueFromPoints(value: string): ScoreValue {
  return parseHumanValue(value, MAX_SCORE_VALUE_UNITS, "ScoreValue") as ScoreValue;
}

export function percentValueFromPercentage(value: string): PercentValue {
  return parseHumanValue(value, MAX_PERCENT_VALUE_UNITS, "PercentValue") as PercentValue;
}

export function formatScoreValue(value: ScoreValue): string {
  return formatUnits(parseScoreValue(value));
}

export function formatPercentValue(value: PercentValue): string {
  return formatUnits(parsePercentValue(value));
}

export function addScoreValues(values: readonly ScoreValue[]): ScoreValue {
  let total = 0n;
  for (const value of values) total += BigInt(parseScoreValue(value));
  return scoreValueFromWideUnits(total);
}

export function averageScoreValues(values: readonly ScoreValue[]): ScoreValue {
  if (values.length === 0) return ZERO_SCORE_VALUE;
  const total = values.reduce((sum, value) => sum + BigInt(parseScoreValue(value)), 0n);
  return scoreValueFromWideUnits(divideRoundHalfUp(total, BigInt(values.length)));
}

export function scoreValueByRatio(
  maximum: ScoreValue,
  earnedUnits: bigint,
  totalUnits: bigint,
): ScoreValue {
  if (earnedUnits < 0n || totalUnits <= 0n || earnedUnits > totalUnits) {
    throw new RangeError("Score ratio requires 0 <= earnedUnits <= totalUnits.");
  }
  const numerator = BigInt(parseScoreValue(maximum)) * earnedUnits;
  return scoreValueFromWideUnits(divideRoundHalfUp(numerator, totalUnits));
}

export function percentValueFromRatio(earnedUnits: bigint, totalUnits: bigint): PercentValue {
  if (earnedUnits < 0n || totalUnits <= 0n || earnedUnits > totalUnits) {
    throw new RangeError("Percent ratio requires 0 <= earnedUnits <= totalUnits.");
  }
  const units = divideRoundHalfUp(earnedUnits * BigInt(MAX_PERCENT_VALUE_UNITS), totalUnits);
  return parsePercentValue(Number(units));
}

export function compareScoreValues(left: ScoreValue, right: ScoreValue): number {
  return parseScoreValue(left) - parseScoreValue(right);
}

export function comparePercentValues(left: PercentValue, right: PercentValue): number {
  return parsePercentValue(left) - parsePercentValue(right);
}

function parseUnits(value: unknown, maximum: number, label: string): number {
  if (typeof value !== "number" || !Number.isSafeInteger(value) || value < 0 || value > maximum) {
    throw new TypeError(`${label} must be an integer between 0 and ${maximum}.`);
  }
  return value;
}

function parseHumanValue(value: string, maximum: number, label: string): number {
  const match = /^(0|[1-9]\d*)(?:\.(\d{1,2}))?$/.exec(value.trim());
  if (!match) {
    throw new TypeError(`${label} must be a non-negative decimal with at most two fractional digits.`);
  }
  const fraction = (match[2] ?? "").padEnd(2, "0");
  const units = BigInt(match[1]!) * BigInt(ACADEMIC_VALUE_SCALE) + BigInt(fraction || "0");
  if (units > BigInt(maximum)) throw new RangeError(`${label} is outside the supported range.`);
  return Number(units);
}

function formatUnits(value: number): string {
  const whole = Math.floor(value / ACADEMIC_VALUE_SCALE);
  const fraction = value % ACADEMIC_VALUE_SCALE;
  if (fraction === 0) return String(whole);
  if (fraction % 10 === 0) return `${whole}.${fraction / 10}`;
  return `${whole}.${String(fraction).padStart(2, "0")}`;
}

function scoreValueFromWideUnits(value: bigint): ScoreValue {
  if (value < 0n || value > BigInt(MAX_SCORE_VALUE_UNITS)) {
    throw new RangeError("ScoreValue is outside the supported range.");
  }
  return Number(value) as ScoreValue;
}

function divideRoundHalfUp(numerator: bigint, denominator: bigint): bigint {
  return (numerator + denominator / 2n) / denominator;
}
