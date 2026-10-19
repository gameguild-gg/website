/**
 * Core environment bindings (env namespace)
 * Most commonly used by all WebAssembly modules
 */
export function createEnvBindings(
  stdout: (text: string) => void,
  stderr: (text: string) => void,
  memory: WebAssembly.Memory
) {
  // Text decoder/encoder for string operations
  const textDecoder = new TextDecoder('utf-8')
  const textEncoder = new TextEncoder()

  // Helper to read string from memory
  const readString = (ptr: number, len: number): string => {
    const bytes = new Uint8Array(memory.buffer, ptr, len)
    return textDecoder.decode(bytes)
  }

  // Helper to write string to memory
  const writeString = (ptr: number, str: string): number => {
    const bytes = textEncoder.encode(str)
    const view = new Uint8Array(memory.buffer, ptr, bytes.length)
    view.set(bytes)
    return bytes.length
  }

  return {
    // Memory
    memory,

    // Abort function (used by many runtimes)
    abort: (messagePtr?: number, fileNamePtr?: number, line?: number, column?: number) => {
      const message = messagePtr ? readString(messagePtr, 1024) : 'abort called'
      const fileName = fileNamePtr ? readString(fileNamePtr, 1024) : 'unknown'
      const errorMsg = `Abort: ${message} at ${fileName}:${line ?? 0}:${column ?? 0}`
      stderr(errorMsg)
      throw new Error(errorMsg)
    },

    // Seed for random number generation
    seed: () => Math.random(),

    // Tracing (for debugging)
    trace: (msgPtr: number, n?: number, a0?: number, a1?: number, a2?: number, a3?: number, a4?: number) => {
      const msg = readString(msgPtr, 256)
      stdout(
        `[TRACE] ${msg} n=${n ?? 0} args=[${a0 ?? 0}, ${a1 ?? 0}, ${a2 ?? 0}, ${a3 ?? 0}, ${a4 ?? 0}]`
      )
    },

    // Console output functions (various numeric types)
    print_i32: (value: number) => stdout(value.toString()),
    print_i64: (value: bigint) => stdout(value.toString()),
    print_f32: (value: number) => stdout(value.toString()),
    print_f64: (value: number) => stdout(value.toString()),
    print_char: (charCode: number) => stdout(String.fromCharCode(charCode)),
    print_newline: () => stdout('\n'),
    print: (ptr: number, len: number) => stdout(readString(ptr, len)),
    println: (ptr: number, len: number) => stdout(readString(ptr, len) + '\n'),

    // Error output
    error_i32: (value: number) => stderr(value.toString()),
    error_i64: (value: bigint) => stderr(value.toString()),
    error_f32: (value: number) => stderr(value.toString()),
    error_f64: (value: number) => stderr(value.toString()),
    eprint: (ptr: number, len: number) => stderr(readString(ptr, len)),
    eprintln: (ptr: number, len: number) => stderr(readString(ptr, len) + '\n'),

    // Generic logging
    log: (value: number) => stdout(value.toString() + '\n'),
    debug: (value: number) => stdout(`[DEBUG] ${value}\n`),

    // Math operations
    abs_i32: (x: number) => Math.abs(x) | 0,
    abs_i64: (x: bigint) => (x < 0n ? -x : x),
    abs_f32: (x: number) => Math.abs(x),
    abs_f64: (x: number) => Math.abs(x),

    min_i32: (a: number, b: number) => Math.min(a, b) | 0,
    min_i64: (a: bigint, b: bigint) => (a < b ? a : b),
    min_f32: (a: number, b: number) => Math.min(a, b),
    min_f64: (a: number, b: number) => Math.min(a, b),

    max_i32: (a: number, b: number) => Math.max(a, b) | 0,
    max_i64: (a: bigint, b: bigint) => (a > b ? a : b),
    max_f32: (a: number, b: number) => Math.max(a, b),
    max_f64: (a: number, b: number) => Math.max(a, b),

    // Float operations
    ceil_f32: (x: number) => Math.ceil(x),
    ceil_f64: (x: number) => Math.ceil(x),
    floor_f32: (x: number) => Math.floor(x),
    floor_f64: (x: number) => Math.floor(x),
    trunc_f32: (x: number) => Math.trunc(x),
    trunc_f64: (x: number) => Math.trunc(x),
    round_f32: (x: number) => Math.round(x),
    round_f64: (x: number) => Math.round(x),
    sqrt_f32: (x: number) => Math.sqrt(x),
    sqrt_f64: (x: number) => Math.sqrt(x),

    // Trigonometric functions
    sin: (x: number) => Math.sin(x),
    cos: (x: number) => Math.cos(x),
    tan: (x: number) => Math.tan(x),
    asin: (x: number) => Math.asin(x),
    acos: (x: number) => Math.acos(x),
    atan: (x: number) => Math.atan(x),
    atan2: (y: number, x: number) => Math.atan2(y, x),

    // Exponential and logarithmic
    exp: (x: number) => Math.exp(x),
    ln: (x: number) => Math.log(x),
    log10: (x: number) => Math.log10(x),
    log2: (x: number) => Math.log2(x),
    pow: (base: number, exp: number) => Math.pow(base, exp),

    // Time
    now: () => Date.now(),
    'Date.now': () => Date.now(),

    // Random
    random: () => Math.random(),
    'Math.random': () => Math.random(),

    // Global constants
    NaN: NaN,
    Infinity: Infinity,
    'Math.E': Math.E,
    'Math.PI': Math.PI,
    'Math.LN2': Math.LN2,
    'Math.LN10': Math.LN10,
    'Math.LOG2E': Math.LOG2E,
    'Math.LOG10E': Math.LOG10E,
    'Math.SQRT1_2': Math.SQRT1_2,
    'Math.SQRT2': Math.SQRT2,

    // Bitwise operations
    clz32: (x: number) => Math.clz32(x),
    ctz32: (x: number) => {
      if (x === 0) return 32
      let count = 0
      while ((x & 1) === 0) {
        x >>>= 1
        count++
      }
      return count
    },
    popcnt32: (x: number) => {
      x = x - ((x >>> 1) & 0x55555555)
      x = (x & 0x33333333) + ((x >>> 2) & 0x33333333)
      return (((x + ((x >>> 4) & 0xf0f0f0f)) * 0x1010101) >>> 24)
    },

    // Memory copy operations
    memcpy: (dest: number, src: number, n: number) => {
      const destView = new Uint8Array(memory.buffer, dest, n)
      const srcView = new Uint8Array(memory.buffer, src, n)
      destView.set(srcView)
      return dest
    },

    memmove: (dest: number, src: number, n: number) => {
      const view = new Uint8Array(memory.buffer)
      view.copyWithin(dest, src, src + n)
      return dest
    },

    memset: (ptr: number, value: number, n: number) => {
      const view = new Uint8Array(memory.buffer, ptr, n)
      view.fill(value)
      return ptr
    },

    memcmp: (ptr1: number, ptr2: number, n: number) => {
      const view1 = new Uint8Array(memory.buffer, ptr1, n)
      const view2 = new Uint8Array(memory.buffer, ptr2, n)
      for (let i = 0; i < n; i++) {
        const v1 = view1[i] ?? 0
        const v2 = view2[i] ?? 0
        if (v1 !== v2) {
          return v1 - v2
        }
      }
      return 0
    },

    // String operations (C-style)
    strlen: (ptr: number) => {
      const view = new Uint8Array(memory.buffer)
      let length = 0
      while (view[ptr + length] !== 0) {
        length++
      }
      return length
    },

    strcmp: (ptr1: number, ptr2: number) => {
      const view = new Uint8Array(memory.buffer)
      let i = 0
      while ((view[ptr1 + i] ?? 0) !== 0 && (view[ptr2 + i] ?? 0) !== 0) {
        const v1 = view[ptr1 + i] ?? 0
        const v2 = view[ptr2 + i] ?? 0
        if (v1 !== v2) {
          return v1 - v2
        }
        i++
      }
      return (view[ptr1 + i] ?? 0) - (view[ptr2 + i] ?? 0)
    },

    strcpy: (dest: number, src: number) => {
      const view = new Uint8Array(memory.buffer)
      let i = 0
      do {
        view[dest + i] = view[src + i] ?? 0
        i++
      } while ((view[src + i - 1] ?? 0) !== 0)
      return dest
    },

    strcat: (dest: number, src: number) => {
      const view = new Uint8Array(memory.buffer)
      let destLen = 0
      while ((view[dest + destLen] ?? 0) !== 0) destLen++
      let i = 0
      do {
        view[dest + destLen + i] = view[src + i] ?? 0
        i++
      } while ((view[src + i - 1] ?? 0) !== 0)
      return dest
    },

    // Allocation helpers
    __heap_base: 65536,
    __data_end: 65536,

    // Table operations
    __table_base: 0,
    __indirect_function_table: new WebAssembly.Table({ initial: 0, element: 'anyfunc' }),

    // String operations helpers
    readString,
    writeString,
  }
}
