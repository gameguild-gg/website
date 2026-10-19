/**
 * JavaScript environment bindings
 */
export function createJSBindings() {
  return {
    // Memory operations
    mem: {
      grow: (pages: number) => pages,
    },

    // String operations
    str: {
      fromCharCode: (code: number) => String.fromCharCode(code),
      charCodeAt: (str: string, index: number) => str.charCodeAt(index),
      length: (str: string) => str.length,
    },

    // Array operations
    arr: {
      length: (arr: readonly unknown[]) => arr.length,
      push: <T>(arr: T[], item: T) => arr.push(item),
      pop: <T>(arr: T[]) => arr.pop(),
    },

    // Object operations
    obj: {
      keys: (obj: NonNullable<unknown>) => Object.keys(obj),
      values: (obj: NonNullable<unknown>): unknown[] => Object.values(obj),
      entries: (obj: NonNullable<unknown>): [string, unknown][] => Object.entries(obj),
    },
  }
}
