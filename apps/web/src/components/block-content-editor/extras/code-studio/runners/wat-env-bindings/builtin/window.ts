/**
 * Window bindings (browser environment stub)
 */
export function createWindowBindings(
  stdout: (text: string) => void,
  stderr: (text: string) => void
) {
  return {
    alert: (message: string) => stdout(`[Alert] ${message}`),
    confirm: (message: string) => {
      stdout(`[Confirm] ${message}`)
      return false
    },
    prompt: (message: string, defaultValue?: string) => {
      stdout(`[Prompt] ${message}`)
      return defaultValue || ''
    },
    setTimeout: (callback: () => void, ms: number) => 0,
    setInterval: (callback: () => void, ms: number) => 0,
    clearTimeout: (id: number) => {},
    clearInterval: (id: number) => {},
    requestAnimationFrame: (callback: FrameRequestCallback) => 0,
    cancelAnimationFrame: (id: number) => {},
  }
}
