import type { PropsWithChildren } from "react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { cleanup, render, screen } from "@testing-library/react"
import userEvent from "@testing-library/user-event"
import type { Block } from "../../lib/storage/editor/block-structure"
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
    Select: ({ value, onValueChange, children }: PropsWithChildren<{ value: string; onValueChange: (value: string) => void }>) => (
      <select value={value} onChange={(event) => onValueChange(event.target.value)}>{children}</select>
    ),
    SelectContent: Content,
    SelectItem: ({ value, children }: PropsWithChildren<{ value: string }>) => <option value={value}>{children}</option>,
    SelectTrigger: () => null, SelectValue: () => null,
  }
})

vi.mock("@/components/block-content-editor/lazy-client-components", () => ({
  CodeStudioEditor: () => null, HTMLEditor: () => null, MarkdownEditor: () => null,
  ModeSelectionDialog: () => null, QuizSettingsDialog: () => null, RichTextEditor: () => null,
}))

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

function setup(block: Block) {
  const onSave = vi.fn<(data: unknown) => void>()
  const onOpenChange = vi.fn<(open: boolean) => void>()
  render(<BlockEditorModal open block={block} blockType={block.type} onSave={onSave} onOpenChange={onOpenChange} />)
  return { onSave, onOpenChange, user: userEvent.setup() }
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
})
