/**
 * Object bindings (Object namespace)
 */
export function createObjectBindings() {
  return {
    is: (a: unknown, b: unknown) => Object.is(a, b),
    keys: (obj: NonNullable<unknown>) => Object.keys(obj),
    values: (obj: NonNullable<unknown>): unknown[] => Object.values(obj),
    entries: (obj: NonNullable<unknown>): [string, unknown][] => Object.entries(obj),
  }
}
