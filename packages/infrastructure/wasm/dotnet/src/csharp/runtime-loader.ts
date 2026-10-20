/**
 * RuntimeLoader for C# WASM Runtime
 * 
 * This loader dynamically loads the main.js script which initializes
 * the .NET WASM runtime and exposes window.CSharpCompiler
 */

import type { CSharpCompilerExports } from '../types'

type CSharpCompilerWindow = Window & { CSharpCompiler?: CSharpCompilerExports }

export class RuntimeLoader {
  private isLoaded = false
  private loadPromise: Promise<void> | null = null

  async initialize(basePath: string = ''): Promise<void> {
    if (this.isLoaded) {
      return Promise.resolve()
    }

    if (this.loadPromise) {
      return this.loadPromise
    }

    this.loadPromise = this._doInitialize(basePath)
    return this.loadPromise
  }

  private async _doInitialize(basePath: string): Promise<void> {
    console.log('[DotNet] Initializing C# runtime...')

    // Check if already loaded
    if ((window as CSharpCompilerWindow).CSharpCompiler) {
      this.isLoaded = true
      console.log('[DotNet] ✓ C# runtime already loaded')
      return
    }

    // Load main.js using script tag (ES module)
    const scriptUrl = `${basePath}/main.js`
    console.log(`[DotNet] Loading runtime from ${scriptUrl}`)
    
    try {
      // Create and append script tag
      const script = document.createElement('script')
      script.type = 'module'
      script.src = scriptUrl
      
      // Wait for script to load
      await new Promise<void>((resolve, reject) => {
        script.onload = () => resolve()
        script.onerror = () => reject(new Error(`Failed to load script: ${scriptUrl}`))
        document.head.appendChild(script)
      })
      
      console.log('[DotNet] Script loaded, waiting for initialization...')
    } catch (error) {
      console.error('[DotNet] Failed to load main.js:', error)
      throw new Error(`Failed to load C# runtime from ${scriptUrl}: ${error instanceof Error ? error.message : String(error)}`)
    }
    
    // Wait for window.CSharpCompiler to be available
    const maxAttempts = 100 // 10 seconds
    for (let i = 0; i < maxAttempts; i++) {
      if ((window as CSharpCompilerWindow).CSharpCompiler) {
        this.isLoaded = true
        console.log('[DotNet] ✓ C# runtime ready')
        return
      }
      await new Promise(resolve => setTimeout(resolve, 100))
    }

    throw new Error('Failed to initialize C# runtime: CSharpCompiler not found after loading main.js')
  }

  isInitialized(): boolean {
    return this.isLoaded
  }
}
