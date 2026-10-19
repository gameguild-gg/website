import { describe, expect, it, vi } from "vitest"
import { getImportErrorMessage, readEntriesAsFiles } from "./import-project-files"

// These stubs model only the native callback boundary used by the reader.
function fileEntry(file: File): FileSystemFileEntry {
  return {
    isFile: true,
    isDirectory: false,
    name: file.name,
    file: (success: FileCallback) => success(file),
  } as FileSystemFileEntry
}

function directoryEntry(name: string, batches: FileSystemEntry[][]) {
  let index = 0
  const readEntries = vi.fn((success: FileSystemEntriesCallback) => success(batches[index++] ?? []))
  const reader: FileSystemDirectoryReader = { readEntries }
  const entry = {
    isFile: false,
    isDirectory: true,
    name,
    createReader: () => reader,
  } as FileSystemDirectoryEntry
  return { entry, readEntries }
}

describe("dropped project files", () => {
  it("returns the original file and assigns its root relative path", async () => {
    const file = new File(["{}"], "index.json")
    expect(await readEntriesAsFiles(fileEntry(file))).toEqual([file])
    expect(file.webkitRelativePath).toBe("index.json")
  })

  it("retains a provided directory prefix", async () => {
    const file = new File(["asset"], "image.png")
    expect(await readEntriesAsFiles(fileEntry(file), "project/assets/")).toEqual([file])
    expect(file.webkitRelativePath).toBe("project/assets/image.png")
  })

  it("keeps the file when its relative path cannot be redefined", async () => {
    const file = new File(["data"], "project.block-content-editor")
    Object.defineProperty(file, "webkitRelativePath", { value: "selected/project.block-content-editor", configurable: false })
    expect(await readEntriesAsFiles(fileEntry(file))).toEqual([file])
    expect(file.webkitRelativePath).toBe("selected/project.block-content-editor")
  })

  it("finishes an empty directory after its first empty batch", async () => {
    const directory = directoryEntry("project", [[]])
    expect(await readEntriesAsFiles(directory.entry)).toEqual([])
    expect(directory.readEntries).toHaveBeenCalledTimes(1)
  })

  it("reads all directory batches until the empty terminator", async () => {
    const first = new File(["{}"], "index.json")
    const second = new File(["{}"], "data.block-content-editor")
    const directory = directoryEntry("project", [[fileEntry(first)], [fileEntry(second)], []])
    expect(await readEntriesAsFiles(directory.entry)).toEqual([first, second])
    expect(directory.readEntries).toHaveBeenCalledTimes(3)
    expect([first.webkitRelativePath, second.webkitRelativePath]).toEqual([
      "project/index.json",
      "project/data.block-content-editor",
    ])
  })

  it("preserves nested folder paths and entry ordering", async () => {
    const metadata = new File(["{}"], "index.json")
    const asset = new File(["asset"], "image.png")
    const data = new File(["{}"], "data.block-content-editor")
    const assets = directoryEntry("assets", [[fileEntry(asset)], []])
    const root = directoryEntry("project", [[fileEntry(metadata), assets.entry, fileEntry(data)], []])
    expect(await readEntriesAsFiles(root.entry)).toEqual([metadata, asset, data])
    expect([metadata.webkitRelativePath, asset.webkitRelativePath, data.webkitRelativePath]).toEqual([
      "project/index.json",
      "project/assets/image.png",
      "project/data.block-content-editor",
    ])
  })

  it("propagates a native file read failure", async () => {
    const error = new DOMException("Cannot read file", "NotReadableError")
    const entry = {
      isFile: true,
      isDirectory: false,
      file: (_success: FileCallback, failure?: ErrorCallback) => failure?.(error),
    } as FileSystemFileEntry
    await expect(readEntriesAsFiles(entry)).rejects.toBe(error)
  })

  it("propagates a native directory read failure", async () => {
    const error = new DOMException("Cannot read directory", "NotReadableError")
    const entry = {
      isFile: false,
      isDirectory: true,
      createReader: () => ({ readEntries: (_success: FileSystemEntriesCallback, failure?: ErrorCallback) => failure?.(error) }),
    } as FileSystemDirectoryEntry
    await expect(readEntriesAsFiles(entry)).rejects.toBe(error)
  })

  it("returns no files for an unsupported entry", async () => {
    const entry = { isFile: false, isDirectory: false } as FileSystemEntry
    expect(await readEntriesAsFiles(entry)).toEqual([])
  })
})

describe("project import error messages", () => {
  it.each([
    { name: "Error message", error: new Error("Invalid project"), expected: "Invalid project" },
    { name: "plain object message", error: { message: "Invalid metadata" }, expected: "Invalid metadata" },
    { name: "DOMException message", error: new DOMException("Access denied", "NotAllowedError"), expected: "Access denied" },
    { name: "empty Error", error: new Error(""), expected: "fallback" },
    { name: "empty object message", error: { message: "" }, expected: "fallback" },
    { name: "numeric message", error: { message: 12 }, expected: "fallback" },
    { name: "object message", error: { message: {} }, expected: "fallback" },
    { name: "null", error: null, expected: "fallback" },
    { name: "undefined", error: undefined, expected: "fallback" },
    { name: "string", error: "invalid", expected: "fallback" },
    { name: "number", error: 12, expected: "fallback" },
    { name: "boolean", error: false, expected: "fallback" },
  ])("handles $name", ({ error, expected }) => {
    expect(getImportErrorMessage(error, "fallback")).toBe(expected)
  })
})
