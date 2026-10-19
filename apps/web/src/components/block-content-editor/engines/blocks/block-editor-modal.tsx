"use client"

import { useState, useCallback, type ReactNode } from "react"
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@game-guild/ui/components/dialog"
import { Button } from "@game-guild/ui/components/button"
import { Input } from "@game-guild/ui/components/input"
import { Label } from "@game-guild/ui/components/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@game-guild/ui/components/select"
import type { Block } from "@/components/block-content-editor/lib/storage/editor/block-structure"
import type { BlockCellType } from "@/components/block-content-editor/lib/storage/editor/block-structure"
import { BLOCK_REGISTRY } from "./block-component-registry"
import {
  CodeStudioEditor,
  HTMLEditor,
  MarkdownEditor,
  ModeSelectionDialog,
  QuizSettingsDialog,
  RichTextEditor,
} from "@/components/block-content-editor/lazy-client-components"

// Pattern A standalone editors
import type { CodeStudioMode } from "@/components/block-content-editor/extras/code-studio/types"
import { LANGUAGE_CONFIGS } from "@/components/block-content-editor/extras/code-studio/types"
import type { BaseMediaData } from "@/components/block-content-editor/nodes/base/media-node-base"
import type { HeaderData } from "@/components/block-content-editor/nodes/header-node"
import type { SourceData } from "@/components/block-content-editor/nodes/source-node"
import { HEADER_LEVELS, isHeaderStyle, isSourceStyle, parseHeaderLevel, type SourceTextField } from "./block-form-values"
import { getCurrentEditingBlock, isPresentationFormData } from "./block-editor-state"

// ============================================================================
// Simple form editors for Pattern B types (no standalone editor exists)
// ============================================================================

interface FormEditorProps<T> {
  data: T
  onChange: (data: T) => void
}

interface YouTubeFormData {
  videoId?: string
  title?: string
  caption?: string
  size?: number
  startAt?: number
  isNew?: boolean
  [key: string]: unknown
}

interface SpotifyFormData {
  spotifyId?: string
  type?: string
  title?: string
  isNew?: boolean
  [key: string]: unknown
}

function YouTubeForm({ data, onChange }: FormEditorProps<YouTubeFormData>) {
  return (
    <div className="space-y-4">
      <div>
        <Label>YouTube Video URL or ID</Label>
        <Input
          placeholder="https://youtube.com/watch?v=... or video ID"
          value={data.videoId || ""}
          onChange={(e) => {
            const val = e.target.value
            // Extract videoId from URL if needed
            const match = val.match(/(?:v=|youtu\.be\/)([a-zA-Z0-9_-]{11})/)
            onChange({ ...data, videoId: match ? match[1] : val, isNew: false })
          }}
        />
      </div>
      <div>
        <Label>Title</Label>
        <Input placeholder="Video title" value={data.title || ""} onChange={(e) => onChange({ ...data, title: e.target.value })} />
      </div>
      <div>
        <Label>Caption</Label>
        <Input placeholder="Caption text" value={data.caption || ""} onChange={(e) => onChange({ ...data, caption: e.target.value })} />
      </div>
      <div className="grid grid-cols-2 gap-4">
        <div>
          <Label>Size (%)</Label>
          <Input type="number" min={10} max={100} value={data.size ?? 100} onChange={(e) => onChange({ ...data, size: Number(e.target.value) })} />
        </div>
        <div>
          <Label>Start at (seconds)</Label>
          <Input type="number" min={0} value={data.startAt ?? 0} onChange={(e) => onChange({ ...data, startAt: Number(e.target.value) })} />
        </div>
      </div>
    </div>
  )
}

function SpotifyForm({ data, onChange }: FormEditorProps<SpotifyFormData>) {
  return (
    <div className="space-y-4">
      <div>
        <Label>Spotify URL or ID</Label>
        <Input
          placeholder="https://open.spotify.com/track/... or ID"
          value={data.spotifyId || ""}
          onChange={(e) => {
            const val = e.target.value
            const match = val.match(/(?:track|album|playlist|artist)\/([a-zA-Z0-9]+)/)
            if (match) {
              const typeMatch = val.match(/(track|album|playlist|artist)\//)
              onChange({ ...data, spotifyId: match[1], type: typeMatch?.[1] || data.type, isNew: false })
            } else {
              onChange({ ...data, spotifyId: val, isNew: false })
            }
          }}
        />
      </div>
      <div>
        <Label>Type</Label>
        <Select value={data.type || "track"} onValueChange={(v) => {
          if (v !== null) onChange({ ...data, type: v })
        }}>
          <SelectTrigger><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="track">Track</SelectItem>
            <SelectItem value="album">Album</SelectItem>
            <SelectItem value="playlist">Playlist</SelectItem>
            <SelectItem value="artist">Artist</SelectItem>
          </SelectContent>
        </Select>
      </div>
      <div>
        <Label>Title</Label>
        <Input placeholder="Title" value={data.title || ""} onChange={(e) => onChange({ ...data, title: e.target.value })} />
      </div>
    </div>
  )
}

function HeaderForm({ data, onChange }: FormEditorProps<HeaderData>) {
  return (
    <div className="space-y-4">
      <div>
        <Label>Header Text</Label>
        <Input placeholder="Section title" value={data.text || ""} onChange={(e) => onChange({ ...data, text: e.target.value })} autoFocus />
      </div>
      <div className="grid grid-cols-2 gap-4">
        <div>
          <Label>Level</Label>
          <Select value={String(data.level ?? 1)} onValueChange={(v) => {
            if (v === null) return
            const level = parseHeaderLevel(v)
            if (level !== undefined) onChange({ ...data, level })
          }}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              {HEADER_LEVELS.map((l) => (
                <SelectItem key={l} value={String(l)}>H{l}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div>
          <Label>Style</Label>
          <Select value={data.style || "default"} onValueChange={(v) => {
            if (v === null) return
            if (isHeaderStyle(v)) onChange({ ...data, style: v })
          }}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="default">Default</SelectItem>
              <SelectItem value="underlined">Underlined</SelectItem>
              <SelectItem value="bordered">Bordered</SelectItem>
              <SelectItem value="gradient">Gradient</SelectItem>
              <SelectItem value="accent">Accent</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>
    </div>
  )
}

function SourceForm({ data, onChange }: FormEditorProps<SourceData>) {
  const sources = data.sources || []

  const addSource = () => {
    onChange({
      ...data,
      sources: [...sources, { id: crypto.randomUUID(), type: "website", author: "", title: "", url: "" }],
    })
  }

  const updateSource = (index: number, field: SourceTextField, value: string) => {
    const newSources = sources.map((s, i) => (i === index ? { ...s, [field]: value } : s))
    onChange({ ...data, sources: newSources })
  }

  const removeSource = (index: number) => {
    onChange({ ...data, sources: sources.filter((_, i) => i !== index) })
  }

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-4">
        <div>
          <Label>Title</Label>
          <Input value={data.title || "References"} onChange={(e) => onChange({ ...data, title: e.target.value })} />
        </div>
        <div>
          <Label>Citation Style</Label>
          <Select value={data.style || "apa"} onValueChange={(v) => {
            if (v === null) return
            if (isSourceStyle(v)) onChange({ ...data, style: v })
          }}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              {["apa", "mla", "chicago", "harvard", "ieee", "abnt"].map((s) => (
                <SelectItem key={s} value={s}>{s.toUpperCase()}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>
      {sources.map((src, i) => (
        <div key={src.id || i} className="border rounded p-3 space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-gray-500">Source #{i + 1}</span>
            <button type="button" onClick={() => removeSource(i)} className="text-xs text-red-500 hover:text-red-700">Remove</button>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <Input placeholder="Author" value={src.author || ""} onChange={(e) => updateSource(i, "author", e.target.value)} />
            <Input placeholder="Title" value={src.title || ""} onChange={(e) => updateSource(i, "title", e.target.value)} />
            <Input placeholder="URL" value={src.url || ""} onChange={(e) => updateSource(i, "url", e.target.value)} />
            <Input placeholder="Year" value={src.year || ""} onChange={(e) => updateSource(i, "year", e.target.value)} />
          </div>
        </div>
      ))}
      <Button variant="outline" size="sm" onClick={addSource}>+ Add Source</Button>
    </div>
  )
}

// ============================================================================
// Main Block Editor Modal
// ============================================================================

interface BlockEditorModalProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  block: Block | null
  blockType: BlockCellType | null
  onSave: (data: unknown) => void
}

export function BlockEditorModal({ open, onOpenChange, block, blockType, onSave }: BlockEditorModalProps) {
  const [editedBlock, setEditedBlock] = useState<Block | null>(null)
  const [showModeSelection, setShowModeSelection] = useState(false)

  const currentBlock = getCurrentEditingBlock(block, editedBlock)
  const hasCurrentEdits = currentBlock !== null && currentBlock === editedBlock

  const handleSave = useCallback((data: unknown) => {
    onSave(data)
    setEditedBlock(null)
    setShowModeSelection(false)
    onOpenChange(false)
  }, [onSave, onOpenChange])

  const handleCancel = useCallback(() => {
    setEditedBlock(null)
    setShowModeSelection(false)
    onOpenChange(false)
  }, [onOpenChange])

  const handleModeSelect = useCallback((mode: CodeStudioMode) => {
    if (currentBlock?.type !== "code-studio") return
    const currentData = currentBlock.data
    const defaultLanguage = currentData.language || "javascript"
    const languageConfig = LANGUAGE_CONFIGS[defaultLanguage]
    const fileId = "1"
    setEditedBlock({
      ...currentBlock,
      data: {
        ...currentData,
        mode,
        files: [
          {
            id: fileId,
            name: `main${languageConfig.defaultExtension}`,
            content: languageConfig.defaultTemplate,
            language: defaultLanguage,
            isFile: 'f' as const,
            isVisible: true,
            path: `main${languageConfig.defaultExtension}`,
          },
        ],
        folders: [],
        openTabs: [fileId],
        activeFileId: fileId,
      },
    })
    setShowModeSelection(false)
  }, [currentBlock])

  if (!blockType || !currentBlock || !currentBlock.data || currentBlock.type !== blockType) return null

  const config = BLOCK_REGISTRY[currentBlock.type]
  if (!config) return null

  // ─── Pattern A: Standalone editors that manage their own UI ───

  // Quiz — has its own dialog
  if (currentBlock.type === "quiz") {
    return (
      <QuizSettingsDialog
        open={open}
        onOpenChange={(nextOpen) => {
          if (!nextOpen) handleCancel()
        }}
        value={currentBlock.data}
        onCommit={handleSave}
      />
    )
  }

  // Code Studio — mode selection for new blocks, then full-screen editor
  if (currentBlock.type === "code-studio") {
    if (!open) return null
    const currentData = currentBlock.data
    const isNewCodeStudio = !currentData.files || currentData.files.length === 0
    if (isNewCodeStudio && !showModeSelection && !hasCurrentEdits) {
      // Auto-show mode selection for new Code Studio blocks
      return <ModeSelectionDialog onSelect={handleModeSelect} onCancel={handleCancel} />
    }
    return <CodeStudioEditor data={currentData} onSave={handleSave} onCancel={handleCancel} />
  }

  // Markdown — renders its own full-screen overlay
  if (currentBlock.type === "markdown") {
    if (!open) return null
    return <MarkdownEditor initialData={currentBlock.data} onSave={handleSave} onCancel={handleCancel} />
  }

  // HTML — renders its own full-screen overlay
  if (currentBlock.type === "html") {
    if (!open) return null
    return <HTMLEditor initialData={currentBlock.data} onSave={handleSave} onCancel={handleCancel} />
  }

  // Rich Text — renders its own full-screen overlay
  if (currentBlock.type === "rich-text") {
    if (!open) return null
    return <RichTextEditor initialData={currentBlock.data} onSave={handleSave} onCancel={handleCancel} />
  }

  // ─── Pattern B: Simple form editors ───

  const formMap = {
    "youtube": YouTubeForm,
    "spotify": SpotifyForm,
    "header": HeaderForm,
    "source": SourceForm,
  }
  let formEditor: ReactNode = null
  if (currentBlock.type === "header") {
    const FormComponent = formMap.header
    formEditor = <FormComponent data={currentBlock.data} onChange={(data) => setEditedBlock({ ...currentBlock, data })} />
  } else if (currentBlock.type === "source") {
    const FormComponent = formMap.source
    formEditor = <FormComponent data={currentBlock.data} onChange={(data) => setEditedBlock({ ...currentBlock, data })} />
  }

  // Presentation and Project get a simple form too
  if (currentBlock.type === "presentation") {
    const currentData = currentBlock.data
    if (!isPresentationFormData(currentData)) return null
    return (
      <Dialog open={open} onOpenChange={(v) => { if (!v) handleCancel() }}>
        <DialogContent className="max-w-2xl">
          <DialogHeader><DialogTitle>Presentation Settings</DialogTitle></DialogHeader>
          <div className="space-y-4">
            <div>
              <Label>Title</Label>
              <Input
                value={typeof currentData.title === "string" ? currentData.title : ""}
                onChange={(e) => setEditedBlock({ ...currentBlock, data: { ...currentData, title: e.target.value } })}
                autoFocus
              />
            </div>
            <p className="text-xs text-gray-500">Full presentation editing is available in the preview. Configure the title here.</p>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={handleCancel}>Cancel</Button>
            <Button onClick={() => handleSave(currentData)}>Save</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    )
  }

  if (currentBlock.type === "project") {
    const currentData = currentBlock.data
    return (
      <Dialog open={open} onOpenChange={(v) => { if (!v) handleCancel() }}>
        <DialogContent className="max-w-2xl">
          <DialogHeader><DialogTitle>Project Reference</DialogTitle></DialogHeader>
          <div className="space-y-4">
            <div>
              <Label>Project ID</Label>
              <Input
                placeholder="Enter project ID to embed"
                value={currentData.projectId || ""}
                onChange={(e) => setEditedBlock({ ...currentBlock, data: { ...currentData, projectId: e.target.value } })}
                autoFocus
              />
            </div>
            <div>
              <Label>Project Name</Label>
              <Input
                placeholder="Display name"
                value={currentData.projectName || ""}
                onChange={(e) => setEditedBlock({ ...currentBlock, data: { ...currentData, projectName: e.target.value } })}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={handleCancel}>Cancel</Button>
            <Button onClick={() => handleSave(currentData)}>Save</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    )
  }

  // Generic form-based editor for Pattern B types
  if (formEditor) {
    const formData = currentBlock.data
    return (
      <Dialog open={open} onOpenChange={(v) => { if (!v) handleCancel() }}>
        <DialogContent className="max-w-2xl max-h-[85vh] overflow-auto">
          <DialogHeader>
            <DialogTitle>{config.label} Editor</DialogTitle>
          </DialogHeader>
          {formEditor}
          <DialogFooter>
            <Button variant="outline" onClick={handleCancel}>Cancel</Button>
            <Button onClick={() => handleSave(formData)}>Save</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    )
  }

  // Fallback
  return (
    <Dialog open={open} onOpenChange={(v) => { if (!v) handleCancel() }}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{config.label}</DialogTitle>
        </DialogHeader>
        <p className="text-sm text-gray-500">Editor not available for this block type.</p>
        <DialogFooter>
          <Button variant="outline" onClick={handleCancel}>Close</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
