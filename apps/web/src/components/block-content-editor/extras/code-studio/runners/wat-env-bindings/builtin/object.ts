/**
 * Object bindings (Object namespace)
 */
export function createObjectBindings() {
  return {
    is: (a: any, b: any) => Object.is(a, b),
    keys: (obj: any) => Object.keys(obj),
    values: (obj: any) => Object.values(obj),
    entries: (obj: any) => Object.entries(obj),
  }
}
