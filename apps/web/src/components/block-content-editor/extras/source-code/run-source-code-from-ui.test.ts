import { describe, expect, it, vi } from 'vitest'
import { runSourceCodeFromUi } from './run-source-code-from-ui'

describe('synchronous source-code execution callbacks', () => {
  it('starts execution once, returns void, and preserves successful completion', async () => {
    const execute = vi.fn(() => Promise.resolve())
    const reportFailure = vi.fn()
    expect(runSourceCodeFromUi(execute, reportFailure)).toBeUndefined()
    expect(execute).toHaveBeenCalledTimes(1)
    await Promise.resolve()
    expect(reportFailure).not.toHaveBeenCalled()
  })

  it.each([
    { name: 'Error', rejection: new Error('compiler failed'), message: 'compiler failed' },
    { name: 'text', rejection: 'runtime stopped', message: 'runtime stopped' },
    { name: 'null', rejection: null, message: 'null' },
    { name: 'undefined', rejection: undefined, message: 'undefined' },
    { name: 'symbol', rejection: Symbol('runtime'), message: 'Symbol(runtime)' },
  ])('handles an asynchronous $name rejection', async ({ rejection, message }) => {
    const execute = vi.fn(() => Promise.reject(rejection))
    const reportFailure = vi.fn()
    expect(runSourceCodeFromUi(execute, reportFailure)).toBeUndefined()
    await Promise.resolve()
    expect(reportFailure).toHaveBeenCalledTimes(1)
    expect(reportFailure).toHaveBeenCalledWith(message)
    expect(execute).toHaveBeenCalledTimes(1)
  })
})
