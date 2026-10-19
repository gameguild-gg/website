/**
 * Document bindings (DOM stub)
 */
export function createDocumentBindings(
  stdout: (text: string) => void,
  stderr: (text: string) => void
) {
  return {
    createElement: (tagName: string) => {
      stdout(`[DOM] createElement: ${tagName}`)
      return {}
    },
    createTextNode: (text: string) => {
      stdout(`[DOM] createTextNode: ${text}`)
      return {}
    },
    getElementById: (id: string) => {
      stdout(`[DOM] getElementById: ${id}`)
      return null
    },
    querySelector: (selector: string) => {
      stdout(`[DOM] querySelector: ${selector}`)
      return null
    },
    querySelectorAll: (selector: string) => {
      stdout(`[DOM] querySelectorAll: ${selector}`)
      return []
    },
    write: (content: string) => stdout(`[Document.write] ${content}`),
    writeln: (content: string) => stdout(`[Document.writeln] ${content}\n`),
  }
}
