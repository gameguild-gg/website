import { describe, expect, it } from "vitest"
import type { Block } from "../../lib/storage/editor/block-structure"
import { getCurrentEditingBlock, isPresentationFormData } from "./block-editor-state"

const original: Block<"header"> = { id: "1", type: "header", data: { text: "Original", level: 1, style: "default" } }
const edited: Block<"header"> = { ...original, data: { ...original.data, text: "Edited" } }

describe("getCurrentEditingBlock", () => {
  it("returns the selected block when there is no draft", () => {
    expect(getCurrentEditingBlock(original, null)).toBe(original)
  })
  it("returns a draft belonging to the same block ID and type", () => {
    expect(getCurrentEditingBlock(original, edited)).toBe(edited)
  })
  it("does not apply a draft to a different block ID", () => {
    const next = { ...original, id: "2" }
    expect(getCurrentEditingBlock(next, edited)).toBe(next)
  })
  it("does not apply a draft to a different type with the same ID", () => {
    const next: Block<"source"> = { id: original.id, type: "source", data: { sources: [] } }
    expect(getCurrentEditingBlock(next, edited)).toBe(next)
  })
  it("does not keep an editor alive when selection is cleared", () => {
    expect(getCurrentEditingBlock(null, edited)).toBeNull()
  })
  it("retains an opaque presentation payload without normalization", () => {
    const payload = { title: "Slides", slides: [{ retained: true }], extension: { version: 2 } }
    const block: Block<"presentation"> = { id: "p", type: "presentation", data: payload }
    expect(getCurrentEditingBlock(block, null)?.data).toBe(payload)
  })
})

describe("isPresentationFormData", () => {
  it.each([
    { name: "empty object", value: {}, expected: true },
    { name: "opaque slide object", value: { slides: [], extension: true }, expected: true },
    { name: "null", value: null, expected: false },
    { name: "undefined", value: undefined, expected: false },
    { name: "array", value: [], expected: false },
    { name: "string", value: "slides", expected: false },
    { name: "number", value: 7, expected: false },
    { name: "boolean", value: false, expected: false },
  ])("checks the editable object boundary for $name", ({ value, expected }) => {
    expect(isPresentationFormData(value)).toBe(expected)
  })
})
