/** Unvalidated JSON object shared by Vega data and compiler boundaries. */
export type VegaSpecRecord = Record<string, unknown>;

export function isVegaSpecRecord(value: unknown): value is VegaSpecRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
