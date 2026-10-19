/**
 * JavaScript environment bindings
 */
export function createJSBindings(
  stdout: (text: string) => void,
  stderr: (text: string) => void
) {
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
      length: (arr: any[]) => arr.length,
      push: (arr: any[], item: any) => arr.push(item),
      pop: (arr: any[]) => arr.pop(),
    },

    // Object operations
    obj: {
      keys: (obj: any) => Object.keys(obj),
      values: (obj: any) => Object.values(obj),
      entries: (obj: any) => Object.entries(obj),
    },
  }
}
