import type { PropsWithChildren } from "react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { cleanup, render, screen } from "@testing-library/react"
import userEvent from "@testing-library/user-event"
import type { Block, BlockCellType } from "../../lib/storage/editor/block-structure"
import type { CodeStudioData, CodeStudioMode } from "../../extras/code-studio/types"
import { BlockEditorModal } from "./block-editor-modal"

// Exercise the real modal's state and payload callbacks through small UI adapters.
// Browser focus, portal and visual acceptance remain separate checks.
vi.mock("@game-guild/ui/components/dialog", () => {
  const Section = ({ children }: PropsWithChildren) => <div>{children}</div>
  return {
    Dialog: ({ open, children }: PropsWithChildren<{ open: boolean }>) => open ? <div role="dialog">{children}</div> : null,
    DialogContent: Section, DialogHeader: Section, DialogTitle: Section, DialogFooter: Section,
  }
})

vi.mock("@game-guild/ui/components/select", () => {
  const Content = ({ children }: PropsWithChildren) => <>{children}</>
  return {
    Select: ({ value, onValueChange, children }: PropsWithChildren<{ value: string; onValueChange: (value: string | null) => void }>) => <div>
      <select value={value} onChange={(event) => onValueChange(event.target.value)}>{children}</select>
      <button type="button" onClick={() => onValueChange(null)}>Clear selected value</button>
    </div>,
    SelectContent: Content,
    SelectItem: ({ value, children }: PropsWithChildren<{ value: string }>) => <option value={value}>{children}</option>,
    SelectTrigger: () => null, SelectValue: () => null,
  }
})

vi.mock("@/components/block-content-editor/lazy-client-components", () => {
  const Standalone = ({ initialData, onSave }: { initialData: unknown; onSave: (data: unknown) => void }) => (
    <button onClick={() => onSave(initialData)}>Save standalone</button>
  )
  return {
    CodeStudioEditor: ({ data, onSave }: { data: CodeStudioData; onSave: (data: CodeStudioData) => void }) => (
      <button onClick={() => onSave(data)}>Save code studio</button>
    ),
    ModeSelectionDialog: ({ onSelect }: { onSelect: (mode: CodeStudioMode) => void }) => <>
      <button onClick={() => onSelect("execution")}>Choose execution</button>
      <button onClick={() => onSelect("test")}>Choose test</button>
    </>,
    HTMLEditor: Standalone, MarkdownEditor: Standalone, RichTextEditor: Standalone,
    QuizSettingsDialog: () => null,
  }
})

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

function setup(block: Block) {
  const onSave = vi.fn<(data: unknown) => void>()
  const onOpenChange = vi.fn<(open: boolean) => void>()
  const { rerender } = render(<BlockEditorModal open block={block} blockType={block.type} onSave={onSave} onOpenChange={onOpenChange} />)
  const selectBlock = (nextBlock: Block | null, blockType: BlockCellType | null = nextBlock?.type ?? null) => {
    rerender(<BlockEditorModal open block={nextBlock} blockType={blockType} onSave={onSave} onOpenChange={onOpenChange} />)
  }
  return { onSave, onOpenChange, selectBlock, user: userEvent.setup() }
}

const sourceData = {
  title: "References", style: "apa" as const, isNew: false,
  retainedMetadata: { original: true },
  sources: [{ id: "source-1", type: "paper" as const, author: "Author", title: "Paper",
    url: "https://example.test/old", year: "2020", doi: "10.0000/example", notes: "Keep notes" }],
}

describe("BlockEditorModal canonical form payloads", () => {
  it("updates header text while retaining level, style and extra stored fields", async () => {
    const data = { text: "Old", level: 3 as const, style: "bordered" as const, retainedMetadata: { original: true } }
    const { user, onSave, onOpenChange } = setup({ id: "1", type: "header", data })
    await user.clear(screen.getByPlaceholderText("Section title"))
    await user.type(screen.getByPlaceholderText("Section title"), "New")
    await user.click(screen.getByRole("button", { name: "Save", exact: true }))
    expect(onSave).toHaveBeenCalledExactlyOnceWith({ ...data, text: "New" })
    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it("saves supported header level and style selections", async () => {
    const { user, onSave } = setup({ id: "1", type: "header", data: { text: "Title", level: 1, style: "default" } })
    const [level, style] = screen.getAllByRole("combobox")
    if (!level || !style) throw new Error("Expected both header selectors")
    await user.selectOptions(level, "6")
    await user.selectOptions(style, "accent")
    await user.click(screen.getByRole("button", { name: "Save", exact: true }))
    expect(onSave).toHaveBeenCalledExactlyOnceWith({ text: "Title", level: 6, style: "accent" })
  })

  it.each([{ field: "level", index: 0 }, { field: "style", index: 1 }])(
    "retains canonical header data when the $field selector emits null", async ({ index }) => {
      const data = { text: "Retained", level: 3 as const, style: "bordered" as const, retainedMetadata: { original: true } }
      const { user, onSave } = setup({ id: "1", type: "header", data })
      const clear = screen.getAllByRole("button", { name: "Clear selected value", exact: true })[index]
      if (!clear) throw new Error("Expected the header selector's clear adapter")
      await user.click(clear)
      await user.click(screen.getByRole("button", { name: "Save", exact: true }))
      expect(onSave).toHaveBeenCalledExactlyOnceWith(data)
    },
  )

  it("discards edited header data on cancel", async () => {
    const { user, onSave, onOpenChange } = setup({ id: "1", type: "header", data: { text: "Old", level: 2, style: "default" } })
    await user.type(screen.getByPlaceholderText("Section title"), " unsaved")
    await user.click(screen.getByRole("button", { name: "Cancel", exact: true }))
    expect(onSave).not.toHaveBeenCalled()
    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it("updates editable source fields while retaining IDs, type, DOI, notes and metadata", async () => {
    const { user, onSave } = setup({ id: "2", type: "source", data: sourceData })
    const changes: Array<[string, string]> = [["Author", "New author"], ["Title", "New title"], ["URL", "https://example.test/new"], ["Year", "2026"]]
    for (const [placeholder, value] of changes) {
      await user.clear(screen.getByPlaceholderText(placeholder))
      await user.type(screen.getByPlaceholderText(placeholder), value)
    }
    await user.click(screen.getByRole("button", { name: "Save", exact: true }))
    expect(onSave).toHaveBeenCalledExactlyOnceWith({ ...sourceData,
      sources: [{ ...sourceData.sources[0], author: "New author", title: "New title", url: "https://example.test/new", year: "2026" }],
    })
  })

  it("appends a default website source without changing existing entries", async () => {
    vi.spyOn(crypto, "randomUUID").mockReturnValue("00000000-0000-4000-8000-000000000001")
    const { user, onSave } = setup({ id: "2", type: "source", data: sourceData })
    await user.click(screen.getByRole("button", { name: "+ Add Source", exact: true }))
    await user.click(screen.getByRole("button", { name: "Save", exact: true }))
    expect(onSave).toHaveBeenCalledExactlyOnceWith({ ...sourceData, sources: [...sourceData.sources,
      { id: "00000000-0000-4000-8000-000000000001", type: "website", author: "", title: "", url: "" }],
    })
  })

  it("removes only the selected source", async () => {
    const data = { ...sourceData, sources: [...sourceData.sources, { id: "source-2", type: "book" as const, author: "Other", title: "Other" }] }
    const { user, onSave } = setup({ id: "2", type: "source", data })
    const removeSecond = screen.getAllByRole("button", { name: "Remove", exact: true })[1]
    if (!removeSecond) throw new Error("Expected a second source")
    await user.click(removeSecond)
    await user.click(screen.getByRole("button", { name: "Save", exact: true }))
    expect(onSave).toHaveBeenCalledExactlyOnceWith(sourceData)
  })

  it("updates a supported citation style while retaining source content", async () => {
    const { user, onSave } = setup({ id: "2", type: "source", data: sourceData })
    await user.selectOptions(screen.getByRole("combobox"), "abnt")
    await user.click(screen.getByRole("button", { name: "Save", exact: true }))
    expect(onSave).toHaveBeenCalledExactlyOnceWith({ ...sourceData, style: "abnt" })
  })

  it("retains canonical citation data when the style selector emits null", async () => {
    const { user, onSave } = setup({ id: "2", type: "source", data: sourceData })
    await user.click(screen.getByRole("button", { name: "Clear selected value", exact: true }))
    await user.click(screen.getByRole("button", { name: "Save", exact: true }))
    expect(onSave).toHaveBeenCalledExactlyOnceWith(sourceData)
  })

  it("discards source edits on cancel", async () => {
    const { user, onSave } = setup({ id: "2", type: "source", data: sourceData })
    await user.type(screen.getByPlaceholderText("Author"), " unsaved")
    await user.click(screen.getByRole("button", { name: "Cancel", exact: true }))
    expect(onSave).not.toHaveBeenCalled()
  })

  it("preserves an existing canonical citation style absent from the current menu", async () => {
    const data = { ...sourceData, style: "vancouver" as const }
    const { user, onSave } = setup({ id: "2", type: "source", data })
    await user.click(screen.getByRole("button", { name: "Save", exact: true }))
    expect(onSave).toHaveBeenCalledExactlyOnceWith(data)
  })

  it("does not carry unsaved header changes into a different block ID", async () => {
    const { user, onSave, selectBlock } = setup({ id: "1", type: "header", data: { text: "First", level: 1, style: "default" } })
    await user.type(screen.getByPlaceholderText("Section title"), " unsaved")
    const data = { text: "Second", level: 2 as const, style: "accent" as const }
    selectBlock({ id: "2", type: "header", data })
    expect(screen.getByPlaceholderText("Section title")).toHaveValue("Second")
    await user.click(screen.getByRole("button", { name: "Save", exact: true }))
    expect(onSave).toHaveBeenCalledExactlyOnceWith(data)
  })

  it("does not carry a draft into a different block type with the same ID", async () => {
    const { user, onSave, selectBlock } = setup({ id: "1", type: "header", data: { text: "First", level: 1, style: "default" } })
    await user.type(screen.getByPlaceholderText("Section title"), " unsaved")
    selectBlock({ id: "1", type: "source", data: sourceData })
    expect(screen.getByPlaceholderText("Author")).toHaveValue("Author")
    await user.click(screen.getByRole("button", { name: "Save", exact: true }))
    expect(onSave).toHaveBeenCalledExactlyOnceWith(sourceData)
  })

  it("updates project fields while retaining editor state and reference metadata", async () => {
    const data = { projectId: "old-id", projectName: "Old name", editorState: { retained: true },
      isLocalCopy: true, isReference: true, originalProjectId: "origin", extra: "retained" }
    const { user, onSave } = setup({ id: "p", type: "project", data })
    await user.clear(screen.getByPlaceholderText("Enter project ID to embed"))
    await user.type(screen.getByPlaceholderText("Enter project ID to embed"), "new-id")
    await user.clear(screen.getByPlaceholderText("Display name"))
    await user.type(screen.getByPlaceholderText("Display name"), "New name")
    await user.click(screen.getByRole("button", { name: "Save", exact: true }))
    expect(onSave).toHaveBeenCalledExactlyOnceWith({ ...data, projectId: "new-id", projectName: "New name" })
  })

  it("updates presentation title without losing opaque slide or extension data", async () => {
    const data = { title: "Old title", slides: [{ content: { retained: true } }], extension: { version: 2 } }
    const { user, onSave } = setup({ id: "p", type: "presentation", data })
    await user.clear(screen.getByRole("textbox"))
    await user.type(screen.getByRole("textbox"), "New title")
    await user.click(screen.getByRole("button", { name: "Save", exact: true }))
    expect(onSave).toHaveBeenCalledExactlyOnceWith({ ...data, title: "New title" })
  })

  it.each([{ data: null }, { data: undefined }, { data: [] }, { data: "invalid" }, { data: 7 }, { data: false }])(
    "does not edit a non-object presentation payload $data", ({ data }) => {
      const { onSave } = setup({ id: "p", type: "presentation", data })
      expect(screen.queryByRole("dialog")).toBeNull()
      expect(onSave).not.toHaveBeenCalled()
    },
  )

  it("does not dispatch a block through a mismatched requested type", () => {
    const { onSave, selectBlock } = setup({ id: "1", type: "header", data: { text: "Title", level: 1, style: "default" } })
    selectBlock({ id: "2", type: "source", data: sourceData }, "header")
    expect(screen.queryByRole("dialog")).toBeNull()
    expect(onSave).not.toHaveBeenCalled()
  })

  it.each(["execution", "test"] as const)("initializes a new Code Studio block in %s mode", async (mode) => {
    const data = { id: "studio-id", files: [], folders: [], openTabs: [], mode: "execution" as const,
      language: "javascript" as const, title: "Retained title", extra: { retained: true } }
    const { user, onSave } = setup({ id: "c", type: "code-studio", data })
    await user.click(screen.getByRole("button", { name: `Choose ${mode}` }))
    await user.click(screen.getByRole("button", { name: "Save code studio" }))
    expect(onSave).toHaveBeenCalledExactlyOnceWith(expect.objectContaining({
      id: "studio-id", mode, title: "Retained title", extra: { retained: true },
      folders: [], openTabs: ["1"], activeFileId: "1",
      files: [expect.objectContaining({ id: "1", name: "main.js", path: "main.js", language: "javascript", isFile: "f", isVisible: true })],
    }))
  })
})
