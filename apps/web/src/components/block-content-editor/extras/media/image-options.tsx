"use client"

import { Label } from "@game-guild/ui/components/label"
import { Input } from "@game-guild/ui/components/input"
import type { BaseMediaData } from "@/components/block-content-editor/nodes/base/media-node-base"

interface ImageOptionsProps {
  data: BaseMediaData
  onChange: <Field extends keyof BaseMediaData>(field: Field, value: BaseMediaData[Field]) => void
}

export function ImageOptions({ data, onChange }: ImageOptionsProps) {
  return (
    <div className="flex items-center gap-2">
      <Label htmlFor="alt" className="text-sm font-medium text-gray-700 dark:text-gray-300 min-w-[80px]">
        Alt Text:
      </Label>
      <Input
        id="alt"
        value={data.alt || ""}
        onChange={(e) => onChange("alt", e.target.value)}
        placeholder="Image description"
        className="flex-1 bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600"
      />
    </div>
  )
}
