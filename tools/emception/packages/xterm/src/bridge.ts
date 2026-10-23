/**
 * TTY bridge: xterm.js <-> stdin/stdout for WASM modules.
 *
 * Implements IOProvider so that consumers can depend on the abstract
 * interface rather than xterm.js directly.
 */

import { Terminal } from '@xterm/xterm';
import type { IOProvider } from 'emception';

export class TTYBridge implements IOProvider {
  private terminal: Terminal;
  private ownsTerminal: boolean;
  private inputBuffer: number[] = [];
  private inputResolvers: Array<(byte: number) => void> = [];
  private _echoStdin = false;
  private _echoedInputLength = 0;

  // Exclusive stdin: when active, terminal input is routed to a separate
  // channel so that the WASM program receives bytes instead of the shell.
  private _exclusiveStdin = false;
  private _exclusiveBuffer: number[] = [];
  private _exclusiveResolvers: Array<(byte: number) => void> = [];

  /**
   * @param containerOrTerminal  Either an HTMLElement (a new Terminal is created
   *   and opened inside it) or an existing Terminal instance to reuse.
   */
  constructor(containerOrTerminal: HTMLElement | Terminal) {
    // Duck-type check: Terminal instances have writeln/onData methods.
    // Using instanceof can fail when Turbopack bundles separate copies
    // of @xterm/xterm for different entry points.
    if (typeof (containerOrTerminal as Terminal).writeln === 'function') {
      this.terminal = containerOrTerminal as Terminal;
      this.ownsTerminal = false;
    } else {
      this.terminal = new Terminal({
        cols: 120,
        rows: 40,
        fontFamily: '"Fira Code", monospace',
        fontSize: 14,
        theme: { background: '#1e1e2e' },
      });
      this.terminal.open(containerOrTerminal as HTMLElement);
      this.ownsTerminal = true;
    }
    this.terminal.onData((data: string) => {
      for (let i = 0; i < data.length; i++) {
        const byte = data.charCodeAt(i);
        // Echo to terminal when stdin echo is enabled (for interactive programs)
        if (this._echoStdin) {
          if (byte === 13) {
            this.terminal.write('\r\n');
            this._echoedInputLength = 0;
          } else if (byte === 127 || byte === 8) {
            if (this._echoedInputLength > 0) {
              this.terminal.write('\b \b');
              this._echoedInputLength--;
            }
          } else if (byte >= 32) {
            this.terminal.write(data[i]);
            this._echoedInputLength++;
          }
        }
        // When exclusive stdin is active, route to the exclusive channel
        // so the WASM program gets the bytes instead of the shell.
        if (this._exclusiveStdin) {
          if (this._exclusiveResolvers.length > 0) {
            this._exclusiveResolvers.shift()!(byte);
          } else {
            this._exclusiveBuffer.push(byte);
          }
        } else {
          if (this.inputResolvers.length > 0) {
            this.inputResolvers.shift()!(byte);
          } else {
            this.inputBuffer.push(byte);
          }
        }
      }
    });
  }

  /**
   * Called by Emscripten's stdin hook. Returns one byte or a Promise that resolves with a byte.
   */
  readByte(): number | null | Promise<number> {
    if (this.inputBuffer.length > 0) {
      return this.inputBuffer.shift()!;
    }
    return new Promise<number>((resolve) => {
      this.inputResolvers.push(resolve);
    });
  }

  /** Subscribe to raw terminal input after the bridge's own byte routing. */
  onData(listener: (data: string) => void): ReturnType<Terminal['onData']> {
    return this.terminal.onData(listener);
  }

  writeLine(text: string): void {
    this.terminal.writeln(text);
  }

  write(text: string): void {
    this.terminal.write(text);
  }

  writeError(text: string): void {
    this.terminal.writeln(`\x1b[31m${text}\x1b[0m`);
  }

  clear(): void {
    this.terminal.clear();
  }

  /** Enable/disable local echo of stdin input (for interactive WASI programs). */
  setStdinEcho(enabled: boolean): void {
    this._echoStdin = enabled;
    if (!enabled) {
      this._echoedInputLength = 0;
    }
  }

  /** Whether exclusive stdin mode is currently active. */
  get isExclusiveStdin(): boolean {
    return this._exclusiveStdin;
  }

  /**
   * Enter exclusive stdin mode. All terminal input is routed to the
   * exclusive channel (readByteExclusive) instead of the normal readByte
   * queue. This prevents the shell from stealing input while a WASM
   * program is waiting for stdin.
   */
  enterExclusiveStdin(): void {
    this._exclusiveStdin = true;
    this._exclusiveBuffer.length = 0;
    this._exclusiveResolvers.length = 0;
  }

  /** Exit exclusive stdin mode. Input routing returns to normal. */
  exitExclusiveStdin(): void {
    this._exclusiveStdin = false;
    this._exclusiveBuffer.length = 0;
    // Resolve any pending exclusive readers with -1 (cancelled) so
    // awaiting code (e.g. feedStdin loop) can exit immediately
    // without consuming the next real keystroke.
    for (const resolve of this._exclusiveResolvers) {
      resolve(-1);
    }
    this._exclusiveResolvers.length = 0;
  }

  /** Read a byte from the exclusive stdin channel (only valid during exclusive mode). */
  readByteExclusive(): number | null | Promise<number> {
    if (this._exclusiveBuffer.length > 0) {
      return this._exclusiveBuffer.shift()!;
    }
    return new Promise<number>((resolve) => {
      this._exclusiveResolvers.push(resolve);
    });
  }
}
