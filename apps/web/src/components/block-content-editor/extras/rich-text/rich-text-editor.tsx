"use client"

/**
 * Modal Rich Text Editor — opens in `BlockEditorShell` and saves on
 * confirm. Thin wrapper around `<LexicalSurface />` with full feature set.
 */

import { useCallback, useMemo, useRef, useState } from "react"
import type { LexicalEditor, SerializedEditorState } from "lexical"
import { FileText, Save } from "lucide-react"

import { Button } from "@game-guild/ui/components/button"
import { Input } from "@game-guild/ui/components/input"
import { Label } from "@game-guild/ui/components/label"
import { LexicalSurface } from "@game-guild/lexical-surface"
import { useEditorSettings } from "@/components/block-content-editor/extras/settings-menu"
import { BlockEditorShell } from "@/components/block-content-editor/extras/block-editor-shell"
import type { RichTextData } from "../../nodes/rich-text-node"

interface RichTextEditorProps {
  initialData?: RichTextData
  onSave: (data: RichTextData) => void
  onCancel: () => void
}

export function RichTextEditor({ initialData, onSave, onCancel }: RichTextEditorProps) {
  const [title, setTitle] = useState(initialData?.title ?? "")
  const editorStateRef = useRef<SerializedEditorState | null>(initialData?.content ?? null)
  const settings = useEditorSettings("rich-text")

  const handleChange = useCallback((serialized: SerializedEditorState, _editor: LexicalEditor) => {
    editorStateRef.current = serialized
  }, [])

  const handleSave = useCallback(() => {
    onSave({
      content: editorStateRef.current,
      title: title || undefined,
    })
  }, [onSave, title])

  const initialState = useMemo(() => initialData?.content ?? null, [initialData])

  return (
    <BlockEditorShell
      settings={settings}
      includeMonacoTheme={false}
      onClose={onCancel}
      icon={<FileText className="h-5 w-5 text-blue-600 dark:text-blue-400" />}
      title="Rich Text Editor"
      headerActions={
        <div className="flex items-center gap-2">
          <Label htmlFor="rt-title" className="text-sm text-gray-600 dark:text-gray-400 whitespace-nowrap">
            Title
          </Label>
          <Input
            id="rt-title"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="Section title (optional)"
            className="h-8 w-48 text-sm"
          />
        </div>
      }
      footer={
        <div className="flex gap-2 justify-end">
          <Button
            variant="outline"
            onClick={onCancel}
            className="border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-800 bg-transparent"
          >
            Cancel
          </Button>
          <Button
            onClick={handleSave}
            className="flex items-center gap-2 bg-blue-600 hover:bg-blue-700 dark:bg-blue-500 dark:hover:bg-blue-600"
          >
            <Save className="h-4 w-4" />
            Save
          </Button>
        </div>
      }
    >
      <div className="flex-1 overflow-auto bg-gray-50 dark:bg-gray-950">
        <div className="w-full max-w-5xl mx-auto flex flex-col bg-white dark:bg-gray-900 border-x border-gray-200 dark:border-gray-800 shadow-sm min-h-full">
          <LexicalSurface
            namespace="RichTextEditor"
            initialState={initialState}
            onChange={handleChange}
            placeholder="Start writing your rich text content..."
            contentClassName="min-h-[400px] max-w-none"
            className="flex-1 flex flex-col"
          />
        </div>
      </div>
    </BlockEditorShell>
  )
}
