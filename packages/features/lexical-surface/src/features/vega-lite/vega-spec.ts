/** Unvalidated JSON object accepted at the Vega/Vega-Lite compiler boundary. */
export type VegaSpecRecord = Record<string, unknown>;

export function isVegaSpecRecord(value: unknown): value is VegaSpecRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
