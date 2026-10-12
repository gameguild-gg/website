/**
 * Safe Math Expression Evaluator
 * Evaluates math formulas with variable substitution without using eval/Function.
 * Accepts LaTeX input (converted to AsciiMath via MathLive) and supports:
 * +, -, *, /, ^, parentheses, and common math functions.
 */

// Use the SSR-only entry of MathLive: it ships *only* the LaTeX↔AsciiMath
// converters, with no <math-field> custom element, no fonts, no sounds.
// This avoids pulling the ~1MB browser bundle into the quiz-settings chunk,
// which used to block the dev compiler from serving other lazy chunks
// (mermaid, vega, code-studio) for several seconds after a quiz formula
// or numeric block was inserted.
import { convertLatexToAsciiMath } from "mathlive/ssr"

type TokenType =
  | "number"
  | "operator"
  | "lparen"
  | "rparen"
  | "function"
  | "comma"

interface Token {
  type: TokenType
  value: string
}

const FUNCTIONS: Record<string, (args: number[]) => number> = {
  sqrt: ([a]) => Math.sqrt(a!),
  abs: ([a]) => Math.abs(a!),
  sin: ([a]) => Math.sin(a!),
  cos: ([a]) => Math.cos(a!),
  tan: ([a]) => Math.tan(a!),
  log: ([a]) => Math.log10(a!),
  ln: ([a]) => Math.log(a!),
  exp: ([a]) => Math.exp(a!),
  ceil: ([a]) => Math.ceil(a!),
  floor: ([a]) => Math.floor(a!),
  round: ([a]) => Math.round(a!),
  min: (args) => Math.min(...args),
  max: (args) => Math.max(...args),
  pow: ([a, b]) => Math.pow(a!, b!),
}

const CONSTANTS: Record<string, number> = {
  pi: Math.PI,
  e: Math.E,
}

function tokenize(expr: string, variables: Record<string, number>): Token[] {
  const tokens: Token[] = []
  let i = 0

  while (i < expr.length) {
    const ch = expr.charAt(i)

    // Skip whitespace
    if (/\s/.test(ch)) {
      i++
      continue
    }

    // Number (including decimals)
    if (/[0-9.]/.test(ch)) {
      let num = ""
      while (i < expr.length && /[0-9.]/.test(expr[i]!)) {
        num += expr[i]
        i++
      }
      if (!/^(?:\d+(?:\.\d*)?|\.\d+)$/.test(num)) {
        throw new Error(`Invalid number: ${num}`)
      }
      tokens.push({ type: "number", value: num })
      continue
    }

    // Letter: could be variable, constant, or function name
    if (/[a-zA-Z_]/.test(ch)) {
      let name = ""
      while (i < expr.length && /[a-zA-Z_0-9]/.test(expr[i]!)) {
        name += expr[i]
        i++
      }
      const lower = name.toLowerCase()

      if (lower in CONSTANTS) {
        tokens.push({ type: "number", value: CONSTANTS[lower]!.toString() })
      } else if (name in variables) {
        tokens.push({ type: "number", value: variables[name]!.toString() })
      } else if (lower in FUNCTIONS) {
        tokens.push({ type: "function", value: lower })
      } else {
        throw new Error(`Unknown identifier: ${name}`)
      }
      continue
    }

    // Operators
    if ("+-*/^".includes(ch)) {
      // Handle unary minus/plus
      if (
        (ch === "-" || ch === "+") &&
        (tokens.length === 0 ||
          tokens[tokens.length - 1]!.type === "operator" ||
          tokens[tokens.length - 1]!.type === "lparen" ||
          tokens[tokens.length - 1]!.type === "comma")
      ) {
        // Unary: read the next number or insert a 0 before
        tokens.push({ type: "number", value: "0" })
        tokens.push({ type: "operator", value: ch })
      } else {
        tokens.push({ type: "operator", value: ch })
      }
      i++
      continue
    }

    if (ch === "(") {
      tokens.push({ type: "lparen", value: "(" })
      i++
      continue
    }

    if (ch === ")") {
      tokens.push({ type: "rparen", value: ")" })
      i++
      continue
    }

    if (ch === ",") {
      tokens.push({ type: "comma", value: "," })
      i++
      continue
    }

    throw new Error(`Unexpected character: ${ch}`)
  }

  return tokens
}

// Shunting-yard + evaluation using operator-precedence
function precedence(op: string): number {
  switch (op) {
    case "+":
    case "-":
      return 1
    case "*":
    case "/":
      return 2
    case "^":
      return 3
    default:
      return 0
  }
}

function isRightAssociative(op: string): boolean {
  return op === "^"
}

function applyOp(op: string, a: number, b: number): number {
  switch (op) {
    case "+":
      return a + b
    case "-":
      return a - b
    case "*":
      return a * b
    case "/":
      if (b === 0) throw new Error("Division by zero")
      return a / b
    case "^":
      return Math.pow(a, b)
    default:
      throw new Error(`Unknown operator: ${op}`)
  }
}

/**
 * Convert a LaTeX expression to the ASCII subset understood by the
 * tokenizer. If the input is already plain ASCII (no LaTeX markup), it
 * is returned unchanged. Normalizes Unicode operators that MathLive
 * may emit (·, ×, ÷, −) back to their ASCII counterparts.
 */
function toEvaluableExpression(input: string): string {
  if (!input) return ""
  const looksLikeLatex = input.includes("\\") || input.includes("{") || input.includes("}")
  let ascii = input
  if (looksLikeLatex) {
    try {
      ascii = convertLatexToAsciiMath(input)
    } catch {
      ascii = input
    }
  }
  return ascii
    .replace(/·/g, "*")
    .replace(/×/g, "*")
    .replace(/÷/g, "/")
    .replace(/−/g, "-")
    .replace(/\u2212/g, "-")
    .replace(/\u00A0/g, " ")
}

/**
 * Evaluates a math expression with given variable values.
 * Accepts LaTeX or plain ASCII expressions.
 * Supports: +, -, *, /, ^ (power), parentheses, and functions
 * (sqrt, abs, sin, cos, tan, log, ln, exp, ceil, floor, round, min, max, pow).
 * Constants: pi, e.
 *
 * Implicit multiplication (e.g. "2x") is NOT supported — use "2*x".
 */
export function evaluateFormula(
  expression: string,
  variables: Record<string, number>,
): number {
  const tokens = tokenize(toEvaluableExpression(expression), variables)

  const output: number[] = []
  const ops: Array<{ type: "op" | "func" | "lparen"; value: string }> = []
  // Track function argument counts
  const argCounts: number[] = []

  for (let i = 0; i < tokens.length; i++) {
    const token = tokens[i]!

    if (token.type === "number") {
      output.push(parseFloat(token.value))
      continue
    }

    if (token.type === "function") {
      ops.push({ type: "func", value: token.value })
      argCounts.push(1) // at least 1 arg
      continue
    }

    if (token.type === "comma") {
      // Pop operators until left paren
      while (ops.length > 0 && ops[ops.length - 1]!.type !== "lparen") {
        const op = ops.pop()!
        if (op.type === "op") {
          const b = output.pop()!
          const a = output.pop()!
          output.push(applyOp(op.value, a, b))
        }
      }
      // Increment arg count
      if (argCounts.length > 0) {
        argCounts[argCounts.length - 1]!++
      }
      continue
    }

    if (token.type === "operator") {
      while (
        ops.length > 0 &&
        ops[ops.length - 1]!.type === "op" &&
        (precedence(ops[ops.length - 1]!.value) > precedence(token.value) ||
          (precedence(ops[ops.length - 1]!.value) === precedence(token.value) &&
            !isRightAssociative(token.value)))
      ) {
        const op = ops.pop()!
        const b = output.pop()!
        const a = output.pop()!
        output.push(applyOp(op.value, a, b))
      }
      ops.push({ type: "op", value: token.value })
      continue
    }

    if (token.type === "lparen") {
      ops.push({ type: "lparen", value: "(" })
      continue
    }

    if (token.type === "rparen") {
      while (ops.length > 0 && ops[ops.length - 1]!.type !== "lparen") {
        const op = ops.pop()!
        if (op.type === "op") {
          const b = output.pop()!
          const a = output.pop()!
          output.push(applyOp(op.value, a, b))
        }
      }
      if (ops.length === 0) throw new Error("Mismatched parentheses")
      ops.pop() // Remove left paren

      // If top of ops is a function, apply it
      if (ops.length > 0 && ops[ops.length - 1]!.type === "func") {
        const func = ops.pop()!
        const numArgs = argCounts.pop() ?? 1
        const fn = FUNCTIONS[func.value]
        if (!fn) throw new Error(`Unknown function: ${func.value}`)
        const args: number[] = []
        for (let j = 0; j < numArgs; j++) {
          args.unshift(output.pop()!)
        }
        output.push(fn(args))
      }
      continue
    }
  }

  // Pop remaining operators
  while (ops.length > 0) {
    const op = ops.pop()!
    if (op.type === "lparen") throw new Error("Mismatched parentheses")
    if (op.type === "op") {
      const b = output.pop()!
      const a = output.pop()!
      output.push(applyOp(op.value, a, b))
    }
  }

  if (output.length !== 1 || !Number.isFinite(output[0])) throw new Error("Invalid expression")
  return output[0]!
}

/**
 * Generate a random value for a variable within its range, respecting decimal places.
 */
export function generateVariableValue(min: number, max: number, decimals: number): number {
  const raw = min + Math.random() * (max - min)
  const factor = Math.pow(10, decimals)
  return Math.round(raw * factor) / factor
}

/**
 * Validate that a formula expression is syntactically valid with given variable names.
 * Returns null if valid, or an error message string.
 */
export function validateFormula(
  expression: string,
  variableNames: string[],
): string | null {
  if (!expression.trim()) return "Formula cannot be empty"
  try {
    // Create a test variables map with value 1 for each
    const testVars: Record<string, number> = {}
    for (const name of variableNames) {
      testVars[name] = 1
    }
    const result = evaluateFormula(expression, testVars)
    if (!isFinite(result)) return "Formula produces an invalid result (infinity or NaN)"
    return null
  } catch (err) {
    return (err as Error).message
  }
}
