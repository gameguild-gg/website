import { describe, expect, it } from "vitest"
import { HEADER_LEVELS, isHeaderStyle, isSourceStyle, parseHeaderLevel } from "./block-form-values"

describe("header form values", () => {
  it("retains all six displayed header levels in order", () => {
    expect(HEADER_LEVELS).toEqual([1, 2, 3, 4, 5, 6])
  })

  it.each([1, 2, 3, 4, 5, 6])("parses the supported level %s", (level) => {
    expect(parseHeaderLevel(String(level))).toBe(level)
  })

  it.each(["02", "2.0"])("retains numeric conversion for %s", (value) => {
    expect(parseHeaderLevel(value)).toBe(2)
  })

  it.each(["0", "7", "2.5", "NaN", "Infinity", "", " ", "not-level"])(
    "does not persist the unsupported level %j", (value) => {
      expect(parseHeaderLevel(value)).toBeUndefined()
    },
  )

  it.each(["default", "underlined", "bordered", "gradient", "accent"])(
    "accepts the canonical header style %s", (style) => expect(isHeaderStyle(style)).toBe(true),
  )

  it.each(["", "unknown", "Default"])(
    "rejects an unsupported header style %j", (style) => expect(isHeaderStyle(style)).toBe(false),
  )
})

describe("source form values", () => {
  it.each(["apa", "mla", "chicago", "harvard", "ieee", "vancouver", "ama", "turabian", "abnt"])(
    "retains the canonical citation style %s", (style) => expect(isSourceStyle(style)).toBe(true),
  )

  it.each(["", "unknown", "APA"])(
    "rejects an unsupported citation style %j", (style) => expect(isSourceStyle(style)).toBe(false),
  )
})
