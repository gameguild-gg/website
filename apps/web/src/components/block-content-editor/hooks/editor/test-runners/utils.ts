/**
 * Deep equality comparison for objects and arrays
 */
export function deepEqual(a: unknown, b: unknown): boolean {
  if (a === b) return true

  if (typeof a !== "object" || a === null || typeof b !== "object" || b === null) return false

  const aIsArray = Array.isArray(a)
  const bIsArray = Array.isArray(b)
  if (aIsArray !== bIsArray) return false

  if (aIsArray && bIsArray) {
    if (a.length !== b.length) return false
    for (let i = 0; i < a.length; i++) {
      if (!deepEqual(a[i], b[i])) return false
    }
    return true
  }

  const keysA = Object.keys(a)
  const keysB = Object.keys(b)

  if (keysA.length !== keysB.length) return false
  const keysBSet = new Set(keysB)

  for (const key of keysA) {
    if (!keysBSet.has(key)) return false
    if (!deepEqual((a as Record<string, unknown>)[key], (b as Record<string, unknown>)[key])) return false
  }

  return true
}
