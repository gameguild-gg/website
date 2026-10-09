import { afterEach, describe, expect, it, vi } from "vitest"

import { javascriptExecutor } from "./javascript-executor"
import type { ExecutionContext } from "./types"
import type { CodeFile, ProgrammingLanguage } from "@/components/block-content-editor/extras/source-code/types"

vi.mock("../../../extras/code-studio/runners/wasm-loader", async () => {
  const { createRequire } = await import("node:module")
  const { readFileSync } = await import("node:fs")
  const require = createRequire(import.meta.url)
  const dependencyRequire = createRequire(require.resolve("quickjs-emscripten"))
  const bytes = readFileSync(dependencyRequire.resolve("@jitl/quickjs-wasmfile-release-asyncify/wasm"))
  return { loadCompressedWasm: vi.fn(async () => Uint8Array.from(bytes).buffer) }
})

function createContext(): ExecutionContext {
  return {
    files: [] as CodeFile[],
    selectedLanguage: "javascript" as ProgrammingLanguage,
    addOutput: vi.fn(),
    setIsExecuting: vi.fn(),
  }
}

describe("javascriptExecutor.handleCommand", () => {
  afterEach(() => javascriptExecutor.stop())
  it("does not expose page globals to student code", async () => {
    const context = createContext()
    context.files = [{ id: "main", name: "main.js", language: "javascript", isVisible: true, isMain: true,
      content: "console.log(typeof document, typeof localStorage, typeof fetch)" }]

    const result = await javascriptExecutor.execute("main", context)

    expect(result.success).toBe(true)
    expect(context.addOutput).toHaveBeenCalledWith("undefined undefined undefined")
  })
  it("evaluates an expression and reports its value", async () => {
    const context = createContext()

    const handled = javascriptExecutor.handleCommand("2 + 3", context)

    expect(handled).toBe(true)
    await vi.waitFor(() => expect(context.addOutput).toHaveBeenCalledWith("5"))
  })

  it("evaluates an expression using globals such as Math", async () => {
    const context = createContext()

    const handled = javascriptExecutor.handleCommand("Math.max(1, 7)", context)

    expect(handled).toBe(true)
    await vi.waitFor(() => expect(context.addOutput).toHaveBeenCalledWith("7"))
  })

  it("falls back to statement evaluation and reports undefined for statements", async () => {
    const context = createContext()

    const handled = javascriptExecutor.handleCommand("let x = 5", context)

    expect(handled).toBe(true)
    await vi.waitFor(() => expect(context.addOutput).toHaveBeenCalledWith("undefined"))
  })

  it("reports an error for invalid input instead of throwing", async () => {
    const context = createContext()

    const handled = javascriptExecutor.handleCommand("}", context)

    expect(handled).toBe(true)
    await vi.waitFor(() => expect(context.addOutput).toHaveBeenCalledWith(expect.stringMatching(/^Error:/)))
  })

  it("declines console.log commands so they run through the terminal path", () => {
    const context = createContext()

    const handled = javascriptExecutor.handleCommand('console.log("hi")', context)

    expect(handled).toBe(false)
    expect(context.addOutput).not.toHaveBeenCalled()
  })

  it.each(["document", "localStorage", "sessionStorage", "fetch", "XMLHttpRequest", "navigator", "process", "require"])(
    "does not expose %s through terminal expressions", async name => {
      const context = createContext()
      expect(javascriptExecutor.handleCommand("typeof " + name, context)).toBe(true)
      await vi.waitFor(() => expect(context.addOutput).toHaveBeenCalledWith("undefined"))
    },
  )

  it.each(['(function() {}).constructor', 'console.log.constructor'])(
    "keeps %s constructor execution inside the guest", async constructor => {
      const context = createContext()
      // The console method is obtained through bracket notation; ordinary console.log commands still use their existing terminal path.
      const expression = constructor.replace('console.log', 'console["log"]') + '("return typeof document")()'
      expect(javascriptExecutor.handleCommand(expression, context)).toBe(true)
      await vi.waitFor(() => expect(context.addOutput).toHaveBeenCalledWith("undefined"))
    },
  )

  it("does not retry a runtime error as a statement", async () => {
    const context = createContext()
    javascriptExecutor.handleCommand('(() => { console.warn("once"); throw new Error("failed"); })()', context)
    await vi.waitFor(() => expect(context.addOutput).toHaveBeenCalledWith("Error: failed"))
    expect(vi.mocked(context.addOutput).mock.calls.filter(([line]) => line === "Warning: once")).toHaveLength(1)
  })

  it("preserves guest variables between terminal commands", async () => {
    const context = createContext()
    javascriptExecutor.handleCommand("let value = 42", context)
    await vi.waitFor(() => expect(context.addOutput).toHaveBeenCalledWith("undefined"))
    javascriptExecutor.handleCommand("value + 1", context)
    await vi.waitFor(() => expect(context.addOutput).toHaveBeenCalledWith("43"))
  })

  it("keeps exported import bindings in the guest and available to its terminal", async () => {
    const context = createContext()
    context.files = [
      { id: "main", name: "main.js", language: "javascript", isVisible: true, isMain: true,
        content: 'import { isolatedAdd } from "./math.js"; console.log(isolatedAdd(2, 3));' },
      { id: "math", name: "math.js", language: "javascript", isVisible: true, isMain: false,
        content: 'export function isolatedAdd(a, b) { return a + b; }' },
    ]
    const result = await javascriptExecutor.execute("main", context)
    expect(result).toEqual({ success: true, output: ["5"] })
    expect("isolatedAdd" in window).toBe(false)
    javascriptExecutor.handleCommand("isolatedAdd(4, 5)", context)
    await vi.waitFor(() => expect(context.addOutput).toHaveBeenCalledWith("9"))
  })

  it("preserves named default imports", async () => {
    const context = createContext()
    context.files = [
      { id: "main", name: "main.js", language: "javascript", isVisible: true, isMain: true,
        content: 'import sum from "./math.js"; console.log(sum(3, 4));' },
      { id: "math", name: "math.js", language: "javascript", isVisible: true, isMain: false,
        content: 'export default function add(a, b) { return a + b; }' },
    ]
    expect(await javascriptExecutor.execute("main", context)).toEqual({ success: true, output: ["7"] })
    expect("sum" in window).toBe(false)
  })

  it("does not read page cookies or mutate the page global", async () => {
    const cookie = vi.spyOn(document, "cookie", "get")
    const context = createContext()
    context.files = [{ id: "main", name: "main.js", language: "javascript", isVisible: true, isMain: true,
      content: 'window.__guestIsolationCanary = "guest"; console.log(typeof window.document);' }]
    try {
      expect(await javascriptExecutor.execute("main", context)).toEqual({ success: true, output: ["undefined"] })
      expect(cookie).not.toHaveBeenCalled()
      expect("__guestIsolationCanary" in window).toBe(false)
    } finally { cookie.mockRestore() }
  })

  it("preserves prompt, alert and confirmation through primitive-only callbacks", async () => {
    const context = createContext()
    context.files = [{ id: "main", name: "main.js", language: "javascript", isVisible: true, isMain: true,
      content: 'const name = prompt("Name", "Learner"); alert(name); const accepted = confirm("Continue?"); console.log(name, accepted);' }]
    const execution = javascriptExecutor.execute("main", context)
    await vi.waitFor(() => expect(window.__awaitingPromptInput).toBe(true))
    window.promptCallback("Ada")
    await vi.waitFor(() => expect(window.__awaitingAlertAck).toBe(true))
    window.alertCallback()
    await vi.waitFor(() => expect(window.__awaitingConfirmInput).toBe(true))
    window.confirmCallback("yes")
    expect(await execution).toEqual({ success: true, output: ["PROMPT: Name [default: Learner]", "ALERT: Ada", "CONFIRM: Continue?", "Ada true"] })
    expect(window.__awaitingPromptInput).toBe(false)
    expect(window.__awaitingAlertAck).toBe(false)
    expect(window.__awaitingConfirmInput).toBe(false)
  })

  it("stops pending input, clears its callback, and permits a fresh run", async () => {
    const context = createContext()
    context.files = [{ id: "main", name: "main.js", language: "javascript", isVisible: true, isMain: true,
      content: 'const name = prompt("Name"); console.log(name);' }]
    const execution = javascriptExecutor.execute("main", context)
    await vi.waitFor(() => expect(window.__awaitingPromptInput).toBe(true))
    const staleCallback = window.promptCallback
    javascriptExecutor.stop()
    expect((await execution).success).toBe(false)
    expect(window.__awaitingPromptInput).toBe(false)
    staleCallback("late input")
    context.files[0]!.content = 'console.log("fresh")'
    expect(await javascriptExecutor.execute("main", context)).toEqual({ success: true, output: ["fresh"] })
  })

  it("reports runtime failures and clears the execution flag", async () => {
    const context = createContext()
    context.files = [{ id: "main", name: "main.js", language: "javascript", isVisible: true, isMain: true,
      content: 'throw new Error("student failure")' }]
    expect(await javascriptExecutor.execute("main", context)).toEqual({ success: false, output: ["Error: student failure"] })
    expect(context.setIsExecuting).toHaveBeenLastCalledWith(false)
  })
})
