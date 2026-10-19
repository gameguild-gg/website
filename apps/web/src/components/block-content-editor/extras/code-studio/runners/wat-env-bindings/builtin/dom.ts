/**
 * DOM-like bindings (for AssemblyScript DOM features)
 * Limited implementation for sandbox environment
 */
export function createDOMBindings(
  stdout: (text: string) => void,
  stderr: (text: string) => void
) {
  return {
    globalThis: {},

    // Document stubs
    document: {
      createElement: (tagName: string) => {
        stdout(`[DOM] createElement: ${tagName}`)
        return {}
      },
      getElementById: (id: string) => {
        stdout(`[DOM] getElementById: ${id}`)
        return null
      },
      write: (content: string) => stdout(`[Document.write] ${content}`),
      writeln: (content: string) => stdout(`[Document.writeln] ${content}\n`),
    },
  }
}
