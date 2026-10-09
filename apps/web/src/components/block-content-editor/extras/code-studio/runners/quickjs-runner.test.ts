import { beforeAll, describe, expect, it, vi } from 'vitest'
import { QuickJSRunner } from './quickjs-runner'

vi.mock('./wasm-loader', async () => {
  const { createRequire } = await import('node:module')
  const { readFileSync } = await import('node:fs')
  const require = createRequire(import.meta.url)
  const dependencyRequire = createRequire(require.resolve('quickjs-emscripten'))
  const bytes = readFileSync(dependencyRequire.resolve('@jitl/quickjs-wasmfile-release-asyncify/wasm'))
  return { loadCompressedWasm: vi.fn(async () => Uint8Array.from(bytes).buffer) }
})

describe('actual installed QuickJS runtime', () => {
  beforeAll(async () => {
    const result = await new QuickJSRunner().execute('console.log("warm")')
    expect(result.exitCode).toBe(0)
  })

  it('streams console channels and accepts stdin', async () => {
    const onOutput = vi.fn()
    const result = await new QuickJSRunner({ onOutput }).execute('console.log(__stdin, 2); console.warn("careful"); console.error("failure")', 'input')
    expect(result).toMatchObject({ exitCode: 0, stdout: 'input 2\nWarning: careful', stderr: 'failure' })
    expect(onOutput.mock.calls).toEqual([['input 2', 'stdout'], ['Warning: careful', 'stdout'], ['failure', 'stderr']])
  })

  it('interrupts a synchronous infinite loop inside the interpreter', async () => {
    const result = await new QuickJSRunner({ timeout: 40 }).execute('while (true) {}')
    expect(result).toMatchObject({ exitCode: 1, stderr: 'Execution timeout.' })
    expect(result.executionTime).toBeLessThan(2000)
  })

  it('enforces the runtime memory limit', async () => {
    const result = await new QuickJSRunner({ memoryLimit: 1024 * 1024, timeout: 3000 }).execute('const values = []; while (true) values.push("x".repeat(1024))')
    expect(result.exitCode).toBe(1)
    expect(result.stderr).toMatch(/memory/i)
  })

  it('caps host output accumulation', async () => {
    const result = await new QuickJSRunner().execute('for (let i = 0; i < 300; i++) console.log("x".repeat(10000))')
    expect(result.exitCode).toBe(1)
    expect(result.stderr).toContain('Output limit exceeded.')
    expect(result.stdout.length).toBeLessThanOrEqual(1024 * 1024)
  })

  it('caps dialog text before calling the UI', async () => {
    const onDialog = vi.fn(async () => undefined)
    const result = await new QuickJSRunner({ onDialog }).execute('await customAlert("x".repeat(2 * 1024 * 1024))')
    expect(result).toMatchObject({ exitCode: 1, stderr: 'Output limit exceeded.' })
    expect(onDialog).not.toHaveBeenCalled()
  })

  it('caps a terminal result', async () => {
    const result = await new QuickJSRunner().evaluate('"x".repeat(2 * 1024 * 1024)')
    expect(result).toMatchObject({ exitCode: 1, stderr: 'Result limit exceeded.' })
  })

  it('drains promise jobs and reports async rejection', async () => {
    const runner = new QuickJSRunner()
    expect(await runner.execute('await Promise.resolve(); console.log("continued")')).toMatchObject({ exitCode: 0, stdout: 'continued' })
    expect(await runner.execute('await Promise.reject(new Error("rejected"))')).toMatchObject({ exitCode: 1, stderr: 'rejected' })
  })

  it('times out a promise that never settles', async () => {
    const result = await new QuickJSRunner({ timeout: 40 }).execute('await new Promise(() => {})')
    expect(result).toMatchObject({ exitCode: 1, stderr: 'Execution timeout.' })
  })

  it('honors the timeout event even when the wall clock has not reached its deadline', async () => {
    const now = Date.now()
    const clock = vi.spyOn(Date, 'now').mockReturnValue(now)
    try {
      const result = await new QuickJSRunner({ timeout: 25 }).execute('await new Promise(() => {})')
      expect(result).toMatchObject({ exitCode: 1, stderr: 'Execution timeout.' })
    } finally { clock.mockRestore() }
  })

  it('stops a pending promise without disposing an executing VM', async () => {
    const runner = new QuickJSRunner()
    const pending = runner.execute('await new Promise(() => {})')
    await new Promise(resolve => setTimeout(resolve, 20))
    await runner.interrupt()
    expect(await pending).toMatchObject({ exitCode: 1, stderr: 'Execution stopped.' })
    expect(await runner.execute('console.log("restart")')).toMatchObject({ exitCode: 0, stdout: 'restart' })
  })

  it('rejects overlapping operations and leaves the owned VM intact', async () => {
    const runner = new QuickJSRunner()
    const pending = runner.execute('await new Promise(() => {})')
    expect(await runner.evaluate('1 + 1')).toMatchObject({ exitCode: 1, stderr: 'Execution is already running.' })
    await runner.interrupt()
    expect((await pending).stderr).toBe('Execution stopped.')
  })

  it('does not share guest globals across independent runs', async () => {
    const runner = new QuickJSRunner()
    expect((await runner.execute('window.canary = "guest"')).exitCode).toBe(0)
    expect(await runner.execute('console.log(typeof window.canary)')).toMatchObject({ exitCode: 0, stdout: 'undefined' })
  })
})
