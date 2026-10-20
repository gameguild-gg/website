"use client"

import { Label } from "@game-guild/ui/components/label"
import type { BaseMediaData } from "@/components/block-content-editor/nodes/base/media-node-base"

interface VideoOptionsProps {
  data: BaseMediaData
  onChange: <Field extends keyof BaseMediaData>(field: Field, value: BaseMediaData[Field]) => void
}

export function VideoOptions({ data, onChange }: VideoOptionsProps) {
  return (
    <>
      <div className="flex items-center gap-2">
        <Label className="text-sm font-medium text-gray-700 dark:text-gray-300 min-w-[80px]">
          Source:
        </Label>
        <div className="flex-1">
          <span className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-blue-50 dark:bg-blue-900/20 text-blue-700 dark:text-blue-300 rounded-md text-sm font-medium border border-blue-200 dark:border-blue-800">
            {data.embedType === "direct" && "📁 Direct File"}
            {data.embedType === "youtube" && "▶️ YouTube"}
            {data.embedType === "vimeo" && "🎬 Vimeo"}
            {data.embedType === "dailymotion" && "📺 Dailymotion"}
            {!data.embedType && "📁 Direct File"}
          </span>
        </div>
      </div>
      
      {data.embedType === "direct" && (
        <div className="flex items-center gap-2">
          <Label className="text-sm font-medium text-gray-700 dark:text-gray-300 min-w-[80px]">
            Format:
          </Label>
          <div className="flex-1 px-3 py-2 bg-gray-50 dark:bg-gray-900 border border-gray-300 dark:border-gray-600 rounded-md text-sm text-gray-700 dark:text-gray-300">
            {data.videoType === "video/mp4" && "📹 MP4 (H.264)"}
            {data.videoType === "video/webm" && "📹 WebM (VP8/VP9)"}
            {data.videoType === "video/ogg" && "📹 Ogg (Theora)"}
            {!data.videoType && "📹 MP4 (H.264)"}
          </div>
          <span className="text-xs text-gray-500 dark:text-gray-400" title="Detected from file extension">
            ℹ️
          </span>
        </div>
      )}
    </>
  )
}
