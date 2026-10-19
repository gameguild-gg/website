/**
 * Number bindings (Number namespace and numeric types)
 * Based on AssemblyScript std/assembly/number.ts implementation
 */

export function createNumberBindings() {
  return {
    // Global number functions
    NaN: NaN,
    Infinity: Infinity,
    isNaN: (value: number) => isNaN(value),
    isFinite: (value: number) => isFinite(value),
    parseInt: (value: string, radix = 10) => parseInt(value, radix),
    parseFloat: (value: string) => parseFloat(value),
    
    // I8 (signed 8-bit integer)
    I8: {
      MIN_VALUE: -128,
      MAX_VALUE: 127,
      parseInt: (value: string, radix = 10) => parseInt(value, radix) & 0xFF,
      toString: (val: number, radix = 10) => (val << 24 >> 24).toString(radix),
    },
    
    // I16 (signed 16-bit integer)
    I16: {
      MIN_VALUE: -32768,
      MAX_VALUE: 32767,
      parseInt: (value: string, radix = 10) => parseInt(value, radix) & 0xFFFF,
      toString: (val: number, radix = 10) => (val << 16 >> 16).toString(radix),
    },
    
    // I32 (signed 32-bit integer)
    I32: {
      MIN_VALUE: -2147483648,
      MAX_VALUE: 2147483647,
      parseInt: (value: string, radix = 10) => parseInt(value, radix) | 0,
      toString: (val: number, radix = 10) => (val | 0).toString(radix),
    },
    
    // I64 (signed 64-bit integer) - approximated with BigInt where possible
    I64: {
      MIN_VALUE: -9223372036854775808n,
      MAX_VALUE: 9223372036854775807n,
      parseInt: (value: string, radix = 10) => {
        try {
          return BigInt(parseInt(value, radix))
        } catch {
          return BigInt(0)
        }
      },
      toString: (val: bigint | number, radix = 10) => {
        try {
          return typeof val === 'bigint' ? val.toString(radix) : BigInt(val).toString(radix)
        } catch {
          return '0'
        }
      },
    },
    
    // Isize (pointer-sized signed integer, typically 32-bit in WASM)
    Isize: {
      MIN_VALUE: -2147483648,
      MAX_VALUE: 2147483647,
      parseInt: (value: string, radix = 10) => parseInt(value, radix) | 0,
      toString: (val: number, radix = 10) => (val | 0).toString(radix),
    },
    
    // U8 (unsigned 8-bit integer)
    U8: {
      MIN_VALUE: 0,
      MAX_VALUE: 255,
      parseInt: (value: string, radix = 10) => parseInt(value, radix) & 0xFF,
      toString: (val: number, radix = 10) => (val & 0xFF).toString(radix),
    },
    
    // U16 (unsigned 16-bit integer)
    U16: {
      MIN_VALUE: 0,
      MAX_VALUE: 65535,
      parseInt: (value: string, radix = 10) => parseInt(value, radix) & 0xFFFF,
      toString: (val: number, radix = 10) => (val & 0xFFFF).toString(radix),
    },
    
    // U32 (unsigned 32-bit integer)
    U32: {
      MIN_VALUE: 0,
      MAX_VALUE: 4294967295,
      parseInt: (value: string, radix = 10) => parseInt(value, radix) >>> 0,
      toString: (val: number, radix = 10) => (val >>> 0).toString(radix),
    },
    
    // U64 (unsigned 64-bit integer) - approximated with BigInt
    U64: {
      MIN_VALUE: 0n,
      MAX_VALUE: 18446744073709551615n,
      parseInt: (value: string, radix = 10) => {
        try {
          const parsed = BigInt(parseInt(value, radix))
          return parsed < 0n ? 0n : parsed
        } catch {
          return BigInt(0)
        }
      },
      toString: (val: bigint | number, radix = 10) => {
        try {
          return typeof val === 'bigint' ? val.toString(radix) : BigInt(val).toString(radix)
        } catch {
          return '0'
        }
      },
    },
    
    // Usize (pointer-sized unsigned integer, typically 32-bit in WASM)
    Usize: {
      MIN_VALUE: 0,
      MAX_VALUE: 4294967295,
      parseInt: (value: string, radix = 10) => parseInt(value, radix) >>> 0,
      toString: (val: number, radix = 10) => (val >>> 0).toString(radix),
    },
    
    // Bool (boolean)
    Bool: {
      MIN_VALUE: false,
      MAX_VALUE: true,
      toString: (val: boolean) => val ? 'true' : 'false',
    },
    Boolean: {
      MIN_VALUE: false,
      MAX_VALUE: true,
      toString: (val: boolean) => val ? 'true' : 'false',
    },
    
    // F32 (32-bit floating point)
    F32: {
      EPSILON: 1.1920928955078125e-7,
      MIN_VALUE: 1.401298464324817e-45,
      MAX_VALUE: 3.4028234663852886e+38,
      MIN_SAFE_INTEGER: -16777215,
      MAX_SAFE_INTEGER: 16777215,
      POSITIVE_INFINITY: Infinity,
      NEGATIVE_INFINITY: -Infinity,
      NaN: NaN,
      isNaN: (value: number) => isNaN(value),
      isFinite: (value: number) => isFinite(value),
      isSafeInteger: (value: number) => {
        const absVal = Math.abs(value)
        return absVal <= 16777215 && Math.trunc(value) === value
      },
      isInteger: (value: number) => isFinite(value) && Math.trunc(value) === value,
      parseInt: (value: string, radix = 10) => parseInt(value, radix),
      parseFloat: (value: string) => parseFloat(value),
      toString: (val: number) => val.toString(),
    },
    
    // F64 (64-bit floating point)
    F64: {
      EPSILON: 2.220446049250313e-16,
      MIN_VALUE: 5e-324,
      MAX_VALUE: 1.7976931348623157e+308,
      MIN_SAFE_INTEGER: -9007199254740991,
      MAX_SAFE_INTEGER: 9007199254740991,
      POSITIVE_INFINITY: Infinity,
      NEGATIVE_INFINITY: -Infinity,
      NaN: NaN,
      isNaN: (value: number) => isNaN(value),
      isFinite: (value: number) => isFinite(value),
      isSafeInteger: (value: number) => Number.isSafeInteger(value),
      isInteger: (value: number) => Number.isInteger(value),
      parseInt: (value: string, radix = 10) => parseInt(value, radix),
      parseFloat: (value: string) => parseFloat(value),
      toString: (val: number) => val.toString(),
    },
    
    // Number (alias for F64)
    Number: {
      EPSILON: Number.EPSILON,
      MIN_VALUE: Number.MIN_VALUE,
      MAX_VALUE: Number.MAX_VALUE,
      MIN_SAFE_INTEGER: Number.MIN_SAFE_INTEGER,
      MAX_SAFE_INTEGER: Number.MAX_SAFE_INTEGER,
      POSITIVE_INFINITY: Number.POSITIVE_INFINITY,
      NEGATIVE_INFINITY: Number.NEGATIVE_INFINITY,
      NaN: Number.NaN,
      isNaN: (value: number) => Number.isNaN(value),
      isFinite: (value: number) => Number.isFinite(value),
      isSafeInteger: (value: number) => Number.isSafeInteger(value),
      isInteger: (value: number) => Number.isInteger(value),
      parseInt: (value: string, radix = 10) => parseInt(value, radix),
      parseFloat: (value: string) => parseFloat(value),
      toString: (val: number, radix = 10) => val.toString(radix),
      toFixed: (val: number, digits = 0) => val.toFixed(digits),
      toExponential: (val: number, fractionDigits?: number) => 
        fractionDigits !== undefined ? val.toExponential(fractionDigits) : val.toExponential(),
      toPrecision: (val: number, precision?: number) => 
        precision !== undefined ? val.toPrecision(precision) : val.toPrecision(),
    },
  }
}
