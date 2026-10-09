import { afterEach, describe, expect, it, vi } from "vitest"
import { typescriptExecutor } from "./typescript-executor"
import type { ExecutionContext } from "./types"
import { javascriptExecutor } from "./javascript-executor"
import typeScriptManifest from "typescript/package.json"

vi.mock("../../../extras/code-studio/runners/wasm-loader", async () => {
  const { createRequire } = await import("node:module")
  const { readFileSync } = await import("node:fs")
  const require = createRequire(import.meta.url)
  const dependencyRequire = createRequire(require.resolve("quickjs-emscripten"))
  const bytes = readFileSync(dependencyRequire.resolve("@jitl/quickjs-wasmfile-release-asyncify/wasm"))
  return { loadCompressedWasm: vi.fn(async () => Uint8Array.from(bytes).buffer) }
})

function createContext(content: string): ExecutionContext {
  return {
    files: [{ id: "main", name: "main.ts", language: "typescript", isVisible: true, isMain: true, content }],
    selectedLanguage: "typescript", addOutput: vi.fn(), setIsExecuting: vi.fn(),
  }
}

describe("TypeScript student execution", () => {
  afterEach(() => { typescriptExecutor.stop(); javascriptExecutor.stop() })
  it("does not expose page globals to transpiled student code", async () => {
    const context = createContext("console.log(typeof document, typeof localStorage, typeof fetch)")
    expect((await typescriptExecutor.execute("main", context)).success).toBe(true)
    expect(context.addOutput).toHaveBeenCalledWith("undefined undefined undefined")
  })

  it.each(["document", "localStorage", "sessionStorage", "fetch", "XMLHttpRequest", "navigator", "process", "require"])(
    "keeps %s unavailable after transpilation", async name => {
      const context = createContext("console.log(typeof " + name + ")")
      expect((await typescriptExecutor.execute("main", context)).success).toBe(true)
      expect(context.addOutput).toHaveBeenCalledWith("undefined")
    },
  )

  it("keeps constructor-created functions inside the guest", async () => {
    const context = createContext('console.log((function() {}).constructor("return typeof document")())')
    expect((await typescriptExecutor.execute("main", context)).success).toBe(true)
    expect(context.addOutput).toHaveBeenCalledWith("undefined")
  })

  it("uses the installed compiler for generics, enums and comparison operators", async () => {
    const context = createContext("enum Level { Ready = 7 }; function identity<T>(value: T): T { return value }; const below: boolean = 2 < 3; console.log(identity<number>(Level.Ready), below)")
    expect((await typescriptExecutor.execute("main", context)).success).toBe(true)
    expect(context.addOutput).toHaveBeenCalledWith("7 true")
  })

  it("bundles relative typed imports inside the guest without changing source files", async () => {
    const context = createContext('import { double } from "./helper"; console.log(double(21))')
    context.files.push({ id: "helper", name: "helper.ts", language: "typescript", isVisible: true, isMain: false,
      content: "export function double(value: number): number { return value * 2 }" })
    const original = structuredClone(context.files)
    expect((await typescriptExecutor.execute("main", context)).success).toBe(true)
    expect(context.addOutput).toHaveBeenCalledWith("42")
    expect(context.files).toEqual(original)
    expect(Reflect.has(window, "double")).toBe(false)
  })

  it("preserves named default imports with an explicit TypeScript extension", async () => {
    const context = createContext('import greet from "./helper.ts"; console.log(greet("student"))')
    context.files.push({ id: "helper", name: "helper.ts", language: "typescript", isVisible: true, isMain: false,
      content: 'export default function greet(name: string): string { return "Hello " + name }' })
    expect((await typescriptExecutor.execute("main", context)).success).toBe(true)
    expect(context.addOutput).toHaveBeenCalledWith("Hello student")
  })

  it("executes type-only modules after the compiler erases their exports", async () => {
    const context = createContext('export interface User { name: string }; const user: User = { name: "student" }; console.log(user.name)')
    expect((await typescriptExecutor.execute("main", context)).success).toBe(true)
    expect(context.addOutput).toHaveBeenCalledWith("student")
  })

  it("reports compiler syntax errors before executing any source", async () => {
    const context = createContext('console.log("must not run"); const value: number = ;')
    const result = await typescriptExecutor.execute("main", context)
    expect(result.success).toBe(false)
    expect(result.output.some(line => line.startsWith("Error main.ts("))).toBe(true)
    expect(context.addOutput).not.toHaveBeenCalledWith("must not run")
    expect(context.setIsExecuting).toHaveBeenLastCalledWith(false)
  })

  it("reports runtime failures as unsuccessful", async () => {
    const context = createContext('throw new Error("failed")')
    expect((await typescriptExecutor.execute("main", context)).success).toBe(false)
    expect(context.addOutput).toHaveBeenCalledWith("Error: failed")
  })

  it("keeps JavaScript and TypeScript guest state independent", async () => {
    const javascriptContext: ExecutionContext = { ...createContext(""), files: [], selectedLanguage: "javascript" }
    javascriptExecutor.handleCommand("let separateCanary = 42", javascriptContext)
    await vi.waitFor(() => expect(javascriptContext.addOutput).toHaveBeenCalledWith("undefined"))
    const context = createContext("console.log(typeof separateCanary)")
    expect((await typescriptExecutor.execute("main", context)).success).toBe(true)
    expect(context.addOutput).toHaveBeenCalledWith("undefined")
    javascriptExecutor.handleCommand("separateCanary", javascriptContext)
    await vi.waitFor(() => expect(javascriptContext.addOutput).toHaveBeenCalledWith("42"))
  })

  it("cancels while loading the compiler and permits a fresh run", async () => {
    const context = createContext('console.log("cancelled source")')
    const pending = typescriptExecutor.execute("main", context)
    typescriptExecutor.stop()
    expect((await pending).success).toBe(false)
    expect(context.addOutput).not.toHaveBeenCalledWith("cancelled source")
    expect(context.setIsExecuting).toHaveBeenLastCalledWith(false)
    const fresh = createContext('console.log("fresh source")')
    expect((await typescriptExecutor.execute("main", fresh)).success).toBe(true)
    expect(fresh.addOutput).toHaveBeenCalledWith("fresh source")
  })

  it("preserves interactive input and clears a stopped pending prompt", async () => {
    const context = createContext('const name: string = prompt("Name?", "student"); console.log(name)')
    const pending = typescriptExecutor.execute("main", context)
    await vi.waitFor(() => expect(window.__awaitingPromptInput).toBe(true))
    window.promptCallback("new student")
    expect((await pending).success).toBe(true)
    expect(context.addOutput).toHaveBeenCalledWith("new student")
    const stopped = typescriptExecutor.execute("main", context)
    await vi.waitFor(() => expect(window.__awaitingPromptInput).toBe(true))
    const lateCallback = window.promptCallback
    typescriptExecutor.stop()
    expect((await stopped).success).toBe(false)
    expect(window.__awaitingPromptInput).toBe(false)
    expect(() => lateCallback("late")).not.toThrow()
  })

  it("reports the compiler version actually installed", () => {
    const context = createContext("")
    expect(typescriptExecutor.handleCommand("tsc --version", context)).toBe(true)
    expect(context.addOutput).toHaveBeenCalledWith("TypeScript Version " + typeScriptManifest.version)
    expect(typescriptExecutor.getSupportedLanguages()).toEqual(["typescript"])
    expect(typescriptExecutor.getFileExtension()).toBe("ts")
  })
})
