import { describe, expect, it } from "vitest"
import { isGoogleDriveProjectContent, isGoogleDriveProjectMetadata } from "./google-drive-project-validation"

const identity = {
  id: "project-1",
  name: "Example project",
  tags: ["course", "example"],
  createdAt: "2026-10-19T00:00:00.000Z",
  updatedAt: "2026-10-19T01:00:00.000Z",
}

const metadata = { ...identity, size: 0, hash: "" }
const content = { ...identity, data: "{}" }

describe("Google Drive project validation", () => {
  it("accepts folder index metadata without requiring embedded project data", () => {
    expect(isGoogleDriveProjectMetadata(metadata)).toBe(true)
    expect(isGoogleDriveProjectContent(metadata)).toBe(false)
  })

  it("accepts project content without requiring folder metadata fields", () => {
    expect(isGoogleDriveProjectContent(content)).toBe(true)
    expect(isGoogleDriveProjectMetadata(content)).toBe(false)
  })

  it("preserves zero size, empty hash, empty strings and empty tag arrays", () => {
    const emptyIdentity = { id: "", name: "", tags: [], createdAt: "", updatedAt: "" }
    expect(isGoogleDriveProjectMetadata({ ...emptyIdentity, size: 0, hash: "" })).toBe(true)
    expect(isGoogleDriveProjectContent({ ...emptyIdentity, data: "" })).toBe(true)
  })

  it("accepts additional export fields without stripping them", () => {
    const exported = { ...metadata, data: "{}", version: "4.0", preferences: { theme: "dark" } }
    const before = JSON.stringify(exported)
    expect(isGoogleDriveProjectMetadata(exported)).toBe(true)
    expect(isGoogleDriveProjectContent(exported)).toBe(true)
    expect(JSON.stringify(exported)).toBe(before)
  })

  it.each([
    { name: "null", value: null },
    { name: "undefined", value: undefined },
    { name: "false", value: false },
    { name: "number", value: 1 },
    { name: "string", value: "project" },
    { name: "array", value: [] },
  ])("rejects $name as a project object", ({ value }) => {
    expect(isGoogleDriveProjectMetadata(value)).toBe(false)
    expect(isGoogleDriveProjectContent(value)).toBe(false)
  })

  it.each([
    { name: "missing id", field: "id", value: undefined },
    { name: "numeric id", field: "id", value: 1 },
    { name: "missing name", field: "name", value: undefined },
    { name: "object name", field: "name", value: {} },
    { name: "missing tags", field: "tags", value: undefined },
    { name: "string tags", field: "tags", value: "course" },
    { name: "numeric tag", field: "tags", value: [1] },
    { name: "mixed tags", field: "tags", value: ["course", null] },
    { name: "object tag", field: "tags", value: [{}] },
    { name: "missing creation time", field: "createdAt", value: undefined },
    { name: "numeric creation time", field: "createdAt", value: 1 },
    { name: "missing update time", field: "updatedAt", value: undefined },
    { name: "null update time", field: "updatedAt", value: null },
  ])("rejects $name in metadata and content", ({ field, value }) => {
    expect(isGoogleDriveProjectMetadata({ ...metadata, [field]: value })).toBe(false)
    expect(isGoogleDriveProjectContent({ ...content, [field]: value })).toBe(false)
  })

  it.each([
    { name: "missing size", field: "size", value: undefined },
    { name: "string size", field: "size", value: "0" },
    { name: "missing hash", field: "hash", value: undefined },
    { name: "numeric hash", field: "hash", value: 1 },
  ])("rejects $name in folder metadata", ({ field, value }) => {
    expect(isGoogleDriveProjectMetadata({ ...metadata, [field]: value })).toBe(false)
  })

  it.each([
    { name: "missing data", value: undefined },
    { name: "object data", value: {} },
    { name: "null data", value: null },
  ])("rejects $name in project content", ({ value }) => {
    expect(isGoogleDriveProjectContent({ ...content, data: value })).toBe(false)
  })
})
