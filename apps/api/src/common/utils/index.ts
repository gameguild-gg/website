export function buildWhereConditions<T>(input: Partial<T>, keys?: (keyof T)[]): Partial<T>[] {
  const keysToCheck = keys ?? (Object.keys(input) as (keyof T)[]);

  if (keysToCheck.length === 0) throw new Error('No key provided to build the conditions.');
  const definedKeys = keysToCheck.filter((key) => input[key] !== undefined && input[key] !== null);

  if (definedKeys.length === 0) throw new Error('No key has a defined value to build the condition.');
  return definedKeys.map((key) => ({ [key]: input[key] })) as Partial<T>[];
}

export function buildUniqueWhereCondition<T>(input: Partial<T>, keys: (keyof T)[]): Partial<T>[] {
  const definedKeys = keys.filter((key) => input[key] !== undefined && input[key] !== null);

  if (definedKeys.length !== 1) throw new Error(`Exactly one option must be provided among: ${keys.join(', ')}.`);
  return definedKeys.map((key) => ({ [key]: input[key] })) as Partial<T>[];
}
