/**
 * Component Model Type Definitions
 * Standard types used across WIT interfaces
 */

// Resource handles (opaque identifiers)
export type InputStream = number
export type OutputStream = number
export type Pollable = number
export type Descriptor = number

// Standard datetime representation
export interface Datetime {
  seconds: bigint
  nanoseconds: number
}

// Stream error variants (tagged union)
export type StreamError = 
  | { tag: 'last-operation-failed'; val: Error }
  | { tag: 'closed' }

// Test expectation interface
export interface TestExpectation {
  'to-be': (expected: unknown) => void
  'to-equal': (expected: unknown) => void
  'to-be-truthy': () => void
  'to-be-falsy': () => void
}
