import type { CodeRunner, FileMap, RunnerOptions, RunnerResult } from './types'
import type { LuaEngine, LuaFactory } from 'wasmoon'

let luaFactory: LuaFactory | null = null

async function getLuaFactory(): Promise<LuaFactory> {
  if (!luaFactory) {
    // Dynamically import wasmoon to avoid loading it during SSR/build
    const { LuaFactory } = await import('wasmoon')
    luaFactory = new LuaFactory()
  }
  return luaFactory
}

async function createFreshLuaEngine(): Promise<LuaEngine> {
  const factory = await getLuaFactory()
  return await factory.createEngine()
}

export class LuaRunner implements CodeRunner {
  private isInterrupted = false
  private readonly options: RunnerOptions

  constructor(options: RunnerOptions = {}) {
    this.options = {
      timeout: options.timeout || 30000,
      memoryLimit: options.memoryLimit || 64 * 1024 * 1024,
      onRequestInput: options.onRequestInput,
    }
  }

  async execute(code: string, stdin?: string): Promise<RunnerResult> {

    const startTime = performance.now()
    let stdout = ''
    let stderr = ''
    let exitCode = 0
    let lua: LuaEngine | null = null

    try {
      lua = await createFreshLuaEngine()
      this.isInterrupted = false

      // Setup print function to capture output
      await lua.doString(`
        local output_buffer = {}
        
        function print(...)
          local args = {...}
          local str_args = {}
          for i, v in ipairs(args) do
            table.insert(str_args, tostring(v))
          end
          local line = table.concat(str_args, "\\t")
          table.insert(output_buffer, line)
        end
        
        function get_output()
          return table.concat(output_buffer, "\\n")
        end
        
        function clear_output()
          output_buffer = {}
        end
      `)

      // Setup stdin - either from pre-provided string or interactive callback
      if (stdin !== undefined) {
        // Pre-provided stdin (static)
        const stdinLines = stdin.split('\n')
        await lua.doString(`
          _stdin_lines = {}
          _stdin_index = 1
        `)

        // Load stdin lines into Lua
        for (const line of stdinLines) {
          const escapedLine = line.replace(/\\/g, '\\\\').replace(/"/g, '\\"')
          await lua.doString(`table.insert(_stdin_lines, "${escapedLine}")`)
        }

        // Override io.read to use stdin
        await lua.doString(`
          io.read = function()
            if _stdin_index <= #_stdin_lines then
              local line = _stdin_lines[_stdin_index]
              _stdin_index = _stdin_index + 1
              return line
            end
            return nil
          end
        `)
      } else if (this.options.onRequestInput) {
        // Interactive input via callback
        const requestInput = this.options.onRequestInput

        lua.global.set('_request_input_js', (prompt?: string) => {
          // Get current output before requesting input
          if (!lua) return Promise.reject(new Error('Lua engine not initialized'))
          return lua.doString('return get_output()').then((currentOutput: unknown) => {
            const outputStr = String(currentOutput || '')
            return requestInput(prompt, outputStr)
          })
        })

        await lua.doString(`
          io.read = function(prompt)
            local promise = _request_input_js(prompt)
            return promise:await()
          end
        `)
      }

      // Clear previous output
      await lua.doString('clear_output()')

      // Execute with timeout
      const timeoutPromise = new Promise<never>((_, reject) => {
        setTimeout(() => reject(new Error('Execution timeout')), this.options.timeout)
      })

      const execPromise = lua.doString(code)

      await Promise.race([execPromise, timeoutPromise])

      // Get output
      const outputResult = await lua.doString('return get_output()')
      stdout = String(outputResult || '')

    } catch (error) {
      exitCode = 1
      const errorMsg = error instanceof Error ? error.message : String(error)
      stderr = errorMsg
    } finally {
      // Clean up the engine after execution
      if (lua) {
        lua.global.close()
      }
    }

    const executionTime = performance.now() - startTime

    return {
      stdout: stdout.trimEnd(),
      stderr: stderr.trimEnd(),
      exitCode,
      executionTime,
    }
  }

  async executeWithFiles(entryPoint: string, files: FileMap, stdin?: string): Promise<RunnerResult> {
    const startTime = performance.now()
    let stdout = ''
    let stderr = ''
    let exitCode = 0
    let lua: LuaEngine | null = null

    try {
      lua = await createFreshLuaEngine()
      this.isInterrupted = false

      // Setup print function to capture output
      await lua.doString(`
        local output_buffer = {}
        
        function print(...)
          local args = {...}
          local str_args = {}
          for i, v in ipairs(args) do
            table.insert(str_args, tostring(v))
          end
          local line = table.concat(str_args, "\\t")
          table.insert(output_buffer, line)
        end
        
        function get_output()
          return table.concat(output_buffer, "\\n")
        end
        
        function clear_output()
          output_buffer = {}
        end
      `)

      // Create a virtual file system in Lua using a table
      await lua.doString(`
        _virtual_fs = {}
        _loaded_modules = {}
        
        function _load_module(modulename)
          -- Check if module is already loaded
          if _loaded_modules[modulename] then
            return function() return _loaded_modules[modulename] end
          end
          
          -- Try to load from virtual FS
          local path = modulename:gsub("%.", "/")
          local content = nil
          local source_name = nil
          
          -- Try with .lua extension
          if _virtual_fs[path .. ".lua"] then
            content = _virtual_fs[path .. ".lua"]
            source_name = "@" .. path .. ".lua"
          -- Try exact match
          elseif _virtual_fs[path] then
            content = _virtual_fs[path]
            source_name = "@" .. path
          -- Try as filename directly (for require "module.lua")
          elseif _virtual_fs[modulename] then
            content = _virtual_fs[modulename]
            source_name = "@" .. modulename
          end
          
          if content then
            local chunk, err = load(content, source_name)
            if not chunk then
              error("Error loading module '" .. modulename .. "': " .. tostring(err))
            end
            
            -- Execute the module and cache its return value
            local module_result = chunk()
            if module_result == nil then
              module_result = true  -- If module returns nothing, cache true
            end
            _loaded_modules[modulename] = module_result
            
            return function() return module_result end
          end
          
          error("Module '" .. modulename .. "' not found in virtual filesystem")
        end
        
        -- Override require to use virtual FS with module caching
        local original_require = require
        require = function(modulename)
          local success, loader = pcall(_load_module, modulename)
          if success then
            return loader()
          else
            -- Fallback to original require for built-in modules
            local builtin_success, builtin_result = pcall(original_require, modulename)
            if builtin_success then
              return builtin_result
            else
              -- Re-throw the virtual FS error if builtin also fails
              error(loader)
            end
          end
        end
      `)

      // Load all files into the virtual file system
      for (const [filePath, content] of Object.entries(files)) {
        // Normalize path (remove leading ./ or /)
        const normalizedPath = filePath.replace(/^\.?\//, '')

        // Escape the content for Lua string
        const escapedContent = content
          .replace(/\\/g, '\\\\')
          .replace(/"/g, '\\"')
          .replace(/\n/g, '\\n')
          .replace(/\r/g, '\\r')
          .replace(/\t/g, '\\t')

        // Add to virtual FS with original path
        await lua.doString(`_virtual_fs["${normalizedPath}"] = "${escapedContent}"`)

        // Also add without .lua extension if present (for easier require)
        if (normalizedPath.endsWith('.lua')) {
          const pathWithoutExt = normalizedPath.slice(0, -4)
          await lua.doString(`_virtual_fs["${pathWithoutExt}"] = "${escapedContent}"`)
        }
      }

      // Setup stdin - either from pre-provided string or interactive callback
      if (stdin !== undefined) {
        // Pre-provided stdin (static)
        const stdinLines = stdin.split('\n')
        await lua.doString(`
          _stdin_lines = {}
          _stdin_index = 1
        `)

        for (const line of stdinLines) {
          const escapedLine = line.replace(/\\/g, '\\\\').replace(/"/g, '\\"')
          await lua.doString(`table.insert(_stdin_lines, "${escapedLine}")`)
        }

        await lua.doString(`
          io.read = function()
            if _stdin_index <= #_stdin_lines then
              local line = _stdin_lines[_stdin_index]
              _stdin_index = _stdin_index + 1
              return line
            end
            return nil
          end
        `)
      } else if (this.options.onRequestInput) {
        // Interactive input via callback
        const requestInput = this.options.onRequestInput

        lua.global.set('_request_input_js', (prompt?: string) => {
          // Get current output before requesting input
          if (!lua) return Promise.reject(new Error('Lua engine not initialized'))
          return lua.doString('return get_output()').then((currentOutput: unknown) => {
            const outputStr = String(currentOutput || '')
            return requestInput(prompt, outputStr)
          })
        })

        await lua.doString(`
          io.read = function(prompt)
            local promise = _request_input_js(prompt)
            return promise:await()
          end
        `)
      }

      // Clear previous output
      await lua.doString('clear_output()')

      // Get entry point content and execute
      const entryContent = files[entryPoint]
      if (!entryContent) {
        throw new Error(`Entry point file '${entryPoint}' not found`)
      }

      // Execute with timeout
      const timeoutPromise = new Promise<never>((_, reject) => {
        setTimeout(() => reject(new Error('Execution timeout')), this.options.timeout)
      })

      const execPromise = lua.doString(entryContent)

      await Promise.race([execPromise, timeoutPromise])

      // Get output
      const outputResult = await lua.doString('return get_output()')
      stdout = String(outputResult || '')

    } catch (error) {
      exitCode = 1
      const errorMsg = error instanceof Error ? error.message : String(error)
      stderr = errorMsg
    } finally {
      // Clean up the engine after execution
      if (lua) {
        lua.global.close()
      }
    }

    const executionTime = performance.now() - startTime

    return {
      stdout: stdout.trimEnd(),
      stderr: stderr.trimEnd(),
      exitCode,
      executionTime,
    }
  }

  async interrupt(): Promise<void> {
    this.isInterrupted = true
  }

  dispose(): void {
    // Wasmoon handles cleanup internally
    this.isInterrupted = false
  }
}
