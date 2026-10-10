import {
  newQuickJSAsyncWASMModule, newVariant, RELEASE_ASYNC,
  type QuickJSContext, type QuickJSDeferredPromise, type QuickJSHandle,
} from 'quickjs-emscripten'
import type { CodeRunner, RunnerResult, RunnerOptions } from './types'
import quickJSManifest from 'quickjs-emscripten/package.json'
import { loadCompressedWasm } from './wasm-loader'

let modulePromise: ReturnType<typeof newQuickJSAsyncWASMModule> | null = null

async function getQuickJSModule() {
  if (!modulePromise) {
    modulePromise = (async () => {
      const wasmBinary = await loadCompressedWasm('/langs/quickjs-asyncify.wasm.gz?version=' + quickJSManifest.version)
      return newQuickJSAsyncWASMModule(newVariant(RELEASE_ASYNC, { wasmBinary }))
    })().catch(error => {
      modulePromise = null
      throw error
    })
  }
  return modulePromise
}

interface Operation {
  cancelled: boolean
  timedOut: boolean
  deadline: number
  stdout: string
  stderr: string
  outputLength: number
  onOutput?: RunnerOptions['onOutput']
  wake?: () => void
  deferred: Set<QuickJSDeferredPromise>
}

export interface QuickJSEvaluationResult extends RunnerResult {
  value?: string
}

const MAX_OUTPUT_CHARACTERS = 1024 * 1024

export class QuickJSRunner implements CodeRunner {
  private context: QuickJSContext | null = null
  private operation: Operation | null = null

  constructor(private readonly options: RunnerOptions = {}) {}

  execute(code: string, stdin?: string): Promise<RunnerResult> {
    return this.run(code, false, stdin)
  }

  evaluate(command: string, onOutput?: RunnerOptions['onOutput']): Promise<QuickJSEvaluationResult> {
    return this.run(command, true, undefined, onOutput)
  }

  private createContext(quickJSModule: Awaited<ReturnType<typeof getQuickJSModule>>): QuickJSContext {
    const vm = quickJSModule.newContext()
    vm.runtime.setMemoryLimit(this.options.memoryLimit ?? 64 * 1024 * 1024)
    vm.runtime.setMaxStackSize(1024 * 1024)
    vm.runtime.setInterruptHandler(() => !this.operation || this.operation.cancelled || this.operation.timedOut || Date.now() >= this.operation.deadline)

    const emit = vm.newFunction('__emitOutput', (channel, text) => {
      const operation = this.operation
      if (!operation) return vm.undefined
      if (vm.typeof(channel) !== 'string' || vm.typeof(text) !== 'string') {
        return { error: vm.newError('Console output must be text.') }
      }
      const output = vm.getString(text)
      if (operation.outputLength + output.length + 1 > MAX_OUTPUT_CHARACTERS) {
        return { error: vm.newError('Output limit exceeded.') }
      }
      operation.outputLength += output.length + 1
      const stream = vm.getString(channel) === 'stderr' ? 'stderr' : 'stdout'
      operation[stream] += output + '\n'
      operation.onOutput?.(output, stream)
      return vm.undefined
    })
    vm.setProp(vm.global, '__emitOutput', emit)
    emit.dispose()

    const dialog = vm.newFunction('__requestDialog', (kindHandle, messageHandle, defaultHandle) => {
      const operation = this.operation
      if (!operation || !this.options.onDialog) return { error: vm.newError('Interactive input is unavailable.') }
      if ([kindHandle, messageHandle, defaultHandle].some(handle => vm.typeof(handle) !== 'string')) {
        return { error: vm.newError('Dialog arguments must be text.') }
      }
      const kind = vm.getString(kindHandle)
      if (kind !== 'alert' && kind !== 'prompt' && kind !== 'confirm') return { error: vm.newError('Unknown dialog type.') }
      if (operation.deferred.size >= 1024) return { error: vm.newError('Dialog limit exceeded.') }
      const message = vm.getString(messageHandle)
      const defaultValue = vm.getString(defaultHandle)
      const dialogLength = message.length + defaultValue.length + 32
      if (operation.outputLength + dialogLength > MAX_OUTPUT_CHARACTERS) {
        return { error: vm.newError('Output limit exceeded.') }
      }
      operation.outputLength += dialogLength
      const deferred = vm.newPromise()
      operation.deferred.add(deferred)
      void Promise.resolve().then(() => {
        if (this.operation !== operation || operation.cancelled) return undefined
        return this.options.onDialog!(kind, message, defaultValue)
      }).then(value => {
        if (this.operation !== operation || !vm.alive || !deferred.alive) return
        const handle = typeof value === 'boolean' ? (value ? vm.true : vm.false)
          : typeof value === 'string' ? vm.newString(value) : vm.undefined
        deferred.resolve(handle)
        if (typeof value === 'string') handle.dispose()
        operation.wake?.()
      }, error => {
        if (this.operation !== operation || !vm.alive || !deferred.alive) return
        const handle = vm.newError(error instanceof Error ? error.message : String(error))
        deferred.reject(handle)
        handle.dispose()
        operation.wake?.()
      })
      return deferred.handle
    })
    vm.setProp(vm.global, '__requestDialog', dialog)
    dialog.dispose()

    // This window is the guest global, never the page's Window or its credentials.
    const bootstrap = vm.evalCode(`
      globalThis.window = globalThis;
      globalThis.console = Object.freeze({
        log: (...args) => __emitOutput('stdout', args.map(String).join(' ')),
        error: (...args) => __emitOutput('stderr', args.map(String).join(' ')),
        warn: (...args) => __emitOutput('stdout', 'Warning: ' + args.map(String).join(' ')),
      });
      globalThis.customAlert = message => __requestDialog('alert', String(message), '');
      globalThis.customPrompt = (message, value = '') => __requestDialog('prompt', String(message), String(value));
      globalThis.customConfirm = message => __requestDialog('confirm', String(message), '');
    `)
    if (bootstrap.error) {
      const message = this.errorText(vm, bootstrap.error)
      bootstrap.error.dispose()
      vm.dispose()
      throw new Error(message)
    }
    bootstrap.value.dispose()
    return vm
  }

  private errorText(vm: QuickJSContext, handle: QuickJSHandle): string {
    const error: unknown = vm.dump(handle)
    const message = error && typeof error === 'object' && 'message' in error ? String(error.message) : String(error)
    return message.slice(0, MAX_OUTPUT_CHARACTERS)
  }

  private async settle(vm: QuickJSContext, handle: QuickJSHandle, operation: Operation): Promise<string> {
    while (true) {
      if (operation.cancelled) throw new Error('Execution stopped.')
      if (operation.timedOut || Date.now() >= operation.deadline) throw new Error('Execution timeout.')
      const state = vm.getPromiseState(handle)
      if (state.type === 'rejected') {
        const message = this.errorText(vm, state.error)
        state.error.dispose()
        throw new Error(message)
      }
      if (state.type === 'fulfilled') {
        try {
          const stringify = vm.getProp(vm.global, 'String')
          try {
            const result = vm.callFunction(stringify, vm.undefined, state.value)
            if (result.error) {
              const message = this.errorText(vm, result.error)
              result.error.dispose()
              throw new Error(message)
            }
            const value = vm.getString(result.value)
            result.value.dispose()
            if (value.length > MAX_OUTPUT_CHARACTERS) throw new Error('Result limit exceeded.')
            return value
          } finally { stringify.dispose() }
        } finally { if (!state.notAPromise) state.value.dispose() }
      }
      if (vm.runtime.hasPendingJob()) {
        const jobs = vm.runtime.executePendingJobs(256)
        if (jobs.error) {
          const message = this.errorText(vm, jobs.error)
          jobs.error.dispose()
          throw new Error(message)
        }
        // Yield between bounded batches so Stop and timeout events can run.
        await new Promise<void>(resolve => setTimeout(resolve, 0))
      } else {
        await new Promise<void>(resolve => { operation.wake = resolve })
        operation.wake = undefined
      }
    }
  }

  private async run(code: string, expression: boolean, stdin?: string, onOutput?: RunnerOptions['onOutput']): Promise<QuickJSEvaluationResult> {
    const started = performance.now()
    if (this.operation) return { stdout: '', stderr: 'Execution is already running.', exitCode: 1, executionTime: 0 }
    const operation: Operation = {
      cancelled: false, timedOut: false, deadline: Date.now() + (this.options.timeout ?? 30000),
      stdout: '', stderr: '', outputLength: 0, deferred: new Set(),
      onOutput: onOutput ?? this.options.onOutput,
    }
    this.operation = operation
    const timeout = setTimeout(() => {
      operation.timedOut = true
      operation.wake?.()
    }, Math.max(0, operation.deadline - Date.now()))
    let value: string | undefined
    let exitCode = 0
    let resultHandle: QuickJSHandle | undefined
    try {
      const quickJSModule = await Promise.race([
        getQuickJSModule(),
        new Promise<never>((_, reject) => {
          operation.wake = () => reject(new Error(operation.cancelled ? 'Execution stopped.' : 'Execution timeout.'))
        }),
      ])
      operation.wake = undefined
      if (operation.cancelled) throw new Error('Execution stopped.')
      if (operation.timedOut || Date.now() >= operation.deadline) throw new Error('Execution timeout.')
      if (!expression || !this.options.persistContext || !this.context?.alive) {
        if (this.context?.alive) this.context.dispose()
        this.context = this.createContext(quickJSModule)
      }
      const vm = this.context
      if (stdin !== undefined) {
        const input = vm.newString(stdin)
        vm.setProp(vm.global, '__stdin', input)
        input.dispose()
      }
      let result = vm.evalCode(expression ? '(' + code + '\n)' : '(async () => {\n' + code + '\n})()')
      if (expression && result.error) {
        const name = vm.getProp(result.error, 'name')
        const syntaxError = vm.getString(name) === 'SyntaxError'
        name.dispose()
        if (syntaxError) {
          result.error.dispose()
          result = vm.evalCode(code)
        }
      }
      if (result.error) {
        const message = this.errorText(vm, result.error)
        result.error.dispose()
        throw new Error(message)
      }
      resultHandle = result.value
      value = await this.settle(vm, resultHandle, operation)
    } catch (error) {
      exitCode = 1
      const message = operation.cancelled ? 'Execution stopped.'
        : operation.timedOut || Date.now() >= operation.deadline ? 'Execution timeout.'
        : error instanceof Error ? error.message : String(error)
      operation.stderr += message
      operation.onOutput?.(message, 'stderr')
    } finally {
      clearTimeout(timeout)
      resultHandle?.dispose()
      for (const deferred of operation.deferred) if (deferred.alive) deferred.dispose()
      if (!this.options.persistContext || exitCode !== 0) {
        if (this.context?.alive) this.context.dispose()
        this.context = null
      }
      this.operation = null
    }
    return { stdout: operation.stdout.trimEnd(), stderr: operation.stderr.trimEnd(), exitCode, executionTime: performance.now() - started, value }
  }

  async interrupt(): Promise<void> { this.dispose() }

  dispose(): void {
    if (this.operation) {
      this.operation.cancelled = true
      this.operation.wake?.()
    } else {
      if (this.context?.alive) this.context.dispose()
      this.context = null
    }
  }
}
