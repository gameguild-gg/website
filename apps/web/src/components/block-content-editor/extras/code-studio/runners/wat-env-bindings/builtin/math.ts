/**
 * Math bindings (Math namespace)
 * Complete set of JavaScript Math functions
 */
export function createMathBindings() {
  return {
    // Constants
    E: Math.E,
    LN2: Math.LN2,
    LN10: Math.LN10,
    LOG2E: Math.LOG2E,
    LOG10E: Math.LOG10E,
    PI: Math.PI,
    SQRT1_2: Math.SQRT1_2,
    SQRT2: Math.SQRT2,

    // Methods
    abs: (x: number) => Math.abs(x),
    acos: (x: number) => Math.acos(x),
    acosh: (x: number) => Math.acosh(x),
    asin: (x: number) => Math.asin(x),
    asinh: (x: number) => Math.asinh(x),
    atan: (x: number) => Math.atan(x),
    atan2: (y: number, x: number) => Math.atan2(y, x),
    atanh: (x: number) => Math.atanh(x),
    cbrt: (x: number) => Math.cbrt(x),
    ceil: (x: number) => Math.ceil(x),
    clz32: (x: number) => Math.clz32(x),
    cos: (x: number) => Math.cos(x),
    cosh: (x: number) => Math.cosh(x),
    exp: (x: number) => Math.exp(x),
    expm1: (x: number) => Math.expm1(x),
    floor: (x: number) => Math.floor(x),
    fround: (x: number) => Math.fround(x),
    hypot: (x: number, y: number) => Math.hypot(x, y),
    imul: (a: number, b: number) => Math.imul(a, b),
    ln: (x: number) => Math.log(x),
    log10: (x: number) => Math.log10(x),
    log1p: (x: number) => Math.log1p(x),
    log2: (x: number) => Math.log2(x),
    max: (a: number, b: number) => Math.max(a, b),
    min: (a: number, b: number) => Math.min(a, b),
    pow: (base: number, exponent: number) => Math.pow(base, exponent),
    random: () => Math.random(),
    round: (x: number) => Math.round(x),
    sign: (x: number) => Math.sign(x),
    sin: (x: number) => Math.sin(x),
    sinh: (x: number) => Math.sinh(x),
    sqrt: (x: number) => Math.sqrt(x),
    tan: (x: number) => Math.tan(x),
    tanh: (x: number) => Math.tanh(x),
    trunc: (x: number) => Math.trunc(x),
  }
}
