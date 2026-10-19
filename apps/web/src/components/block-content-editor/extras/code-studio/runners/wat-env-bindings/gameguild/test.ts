/**
 * GameGuild Test Interface (gameguild:runtime/test)
 * Custom component interface for testing utilities
 * 
 * WIT definition:
 * ```wit
 * package gameguild:runtime@0.1.0;
 * 
 * interface test {
 *   describe: func(name: string, callback: func());
 *   it: func(name: string, callback: func());
 * }
 * ```
 */
import type { TestExpectation } from '../types.js'

export interface GameGuildTest {
  describe: (name: string, fn: () => void) => void
  it: (name: string, fn: () => void) => void
  expect: (value: unknown) => TestExpectation
}

export function createTest(
  stdout: (text: string) => void,
  stderr: (text: string) => void
): GameGuildTest {
  let testsPassed = 0
  let testsFailed = 0

  return {
    describe: (name: string, fn: () => void) => {
      stdout(`\n[Suite] ${name}`)
      fn()
    },

    it: (name: string, fn: () => void) => {
      try {
        fn()
        testsPassed++
        stdout(`  ✓ ${name}`)
      } catch (error) {
        testsFailed++
        stderr(`  ✗ ${name}`)
        stderr(`    ${error instanceof Error ? error.message : String(error)}`)
      }
    },

    expect: (actual: unknown) => ({
      'to-be': (expected: unknown) => {
        if (actual !== expected) {
          throw new Error(`Expected ${expected}, got ${actual}`)
        }
      },
      'to-equal': (expected: unknown) => {
        if (JSON.stringify(actual) !== JSON.stringify(expected)) {
          throw new Error(
            `Expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`
          )
        }
      },
      'to-be-truthy': () => {
        if (!actual) {
          throw new Error(`Expected truthy value, got ${actual}`)
        }
      },
      'to-be-falsy': () => {
        if (actual) {
          throw new Error(`Expected falsy value, got ${actual}`)
        }
      },
    }),
  }
}
