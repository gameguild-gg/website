"use client"

import { useState } from "react"
import { Button } from "@game-guild/ui/components/button"
import { Input } from "@game-guild/ui/components/input"
import { Label } from "@game-guild/ui/components/label"
import { Textarea } from "@game-guild/ui/components/textarea"
import { Save, FileText, Eye, AlertCircle, Check } from "lucide-react"
import {
  Notebook,
  Info,
  Flame,
  CheckCircle,
  HelpCircle,
  AlertTriangle,
  Skull,
  Bug,
  List,
  Quote,
  Zap,
  ShieldAlert,
  Bell,
  Lightbulb,
  BookMarked,
} from "lucide-react"
import type { AdmonitionData } from "@/components/block-content-editor/nodes/admonition-node"
import type { AdmonitionType } from "@/components/block-content-editor/extras/admonition"
import { Admonition } from "@/components/block-content-editor/extras/admonition"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@game-guild/ui/components/dropdown-menu"
import { BlockEditorShell } from "@/components/block-content-editor/extras/block-editor-shell"
import { useEditorSettings } from "@/components/block-content-editor/extras/settings-menu/use-editor-settings"

interface AdmonitionEditorProps {
  initialData?: AdmonitionData
  onSave: (data: AdmonitionData) => void
  onCancel: () => void
}

const typeToIcon = {
  note: <Notebook className="h-4 w-4 text-blue-500" />,
  abstract: <FileText className="h-4 w-4 text-sky-500" />,
  info: <Info className="h-4 w-4 text-cyan-500" />,
  tip: <Flame className="h-4 w-4 text-lime-500" />,
  success: <CheckCircle className="h-4 w-4 text-green-500" />,
  question: <HelpCircle className="h-4 w-4 text-amber-500" />,
  warning: <AlertTriangle className="h-4 w-4 text-yellow-500" />,
  failure: <AlertCircle className="h-4 w-4 text-red-500" />,
  danger: <Skull className="h-4 w-4 text-orange-500" />,
  bug: <Bug className="h-4 w-4 text-stone-500" />,
  example: <List className="h-4 w-4 text-teal-500" />,
  quote: <Quote className="h-4 w-4 text-pink-500" />,
  important: <Zap className="h-4 w-4 text-purple-500" />,
  caution: <ShieldAlert className="h-4 w-4 text-rose-500" />,
  attention: <Bell className="h-4 w-4 text-fuchsia-500" />,
  hint: <Lightbulb className="h-4 w-4 text-emerald-500" />,
  check: <Check className="h-4 w-4 text-indigo-500" />,
  summary: <BookMarked className="h-4 w-4 text-violet-500" />,
}

// Icons for miniatures (without color class for inline styling)
const typeToIconMiniature: Record<AdmonitionType, React.ReactNode> = {
  note: <Notebook className="w-3 h-3" />,
  abstract: <FileText className="w-3 h-3" />,
  info: <Info className="w-3 h-3" />,
  tip: <Flame className="w-3 h-3" />,
  success: <CheckCircle className="w-3 h-3" />,
  question: <HelpCircle className="w-3 h-3" />,
  warning: <AlertTriangle className="w-3 h-3" />,
  failure: <AlertCircle className="w-3 h-3" />,
  danger: <Skull className="w-3 h-3" />,
  bug: <Bug className="w-3 h-3" />,
  example: <List className="w-3 h-3" />,
  quote: <Quote className="w-3 h-3" />,
  important: <Zap className="w-3 h-3" />,
  caution: <ShieldAlert className="w-3 h-3" />,
  attention: <Bell className="w-3 h-3" />,
  hint: <Lightbulb className="w-3 h-3" />,
  check: <Check className="w-3 h-3" />,
  summary: <BookMarked className="w-3 h-3" />,
}

const admonitionTypes: { value: AdmonitionType; label: string }[] = [
  { value: "note", label: "Note" },
  { value: "abstract", label: "Abstract" },
  { value: "info", label: "Info" },
  { value: "tip", label: "Tip" },
  { value: "success", label: "Success" },
  { value: "question", label: "Question" },
  { value: "warning", label: "Warning" },
  { value: "failure", label: "Failure" },
  { value: "danger", label: "Danger" },
  { value: "bug", label: "Bug" },
  { value: "example", label: "Example" },
  { value: "quote", label: "Quote" },
  { value: "important", label: "Important" },
  { value: "caution", label: "Caution" },
  { value: "attention", label: "Attention" },
  { value: "hint", label: "Hint" },
  { value: "check", label: "Check" },
  { value: "summary", label: "Summary" },
]

const designStyles: { value: "default" | "compact" | "bordered" | "vertical-bar"; label: string; description: string }[] = [
  { value: "default", label: "Default", description: "Full colored background" },
  { value: "compact", label: "Compact", description: "Left border with subtle background" },
  { value: "bordered", label: "Bordered", description: "Full border outline" },
  { value: "vertical-bar", label: "Vertical Bar", description: "Left accent bar" },
]

// Map of colors by type (Tailwind color values)
const typeToColorValues: Record<AdmonitionType, { border: string; bg: string; bgDark: string; text: string; textDark: string }> = {
  note: { border: "#3b82f6", bg: "#1e3a8a", bgDark: "#1e3a8a", text: "#dbeafe", textDark: "#60a5fa" },
  abstract: { border: "#0ea5e9", bg: "#0c4a6e", bgDark: "#0c4a6e", text: "#e0f2fe", textDark: "#38bdf8" },
  info: { border: "#06b6d4", bg: "#164e63", bgDark: "#164e63", text: "#cffafe", textDark: "#22d3ee" },
  tip: { border: "#84cc16", bg: "#365314", bgDark: "#365314", text: "#ecfccb", textDark: "#a3e635" },
  success: { border: "#22c55e", bg: "#14532d", bgDark: "#14532d", text: "#dcfce7", textDark: "#4ade80" },
  question: { border: "#f59e0b", bg: "#78350f", bgDark: "#78350f", text: "#fef3c7", textDark: "#fbbf24" },
  warning: { border: "#eab308", bg: "#713f12", bgDark: "#713f12", text: "#fef9c3", textDark: "#facc15" },
  failure: { border: "#ef4444", bg: "#7f1d1d", bgDark: "#7f1d1d", text: "#fee2e2", textDark: "#f87171" },
  danger: { border: "#f97316", bg: "#7c2d12", bgDark: "#7c2d12", text: "#ffedd5", textDark: "#fb923c" },
  bug: { border: "#78716c", bg: "#292524", bgDark: "#292524", text: "#e7e5e4", textDark: "#a8a29e" },
  example: { border: "#14b8a6", bg: "#134e4a", bgDark: "#134e4a", text: "#ccfbf1", textDark: "#2dd4bf" },
  quote: { border: "#ec4899", bg: "#831843", bgDark: "#831843", text: "#fce7f3", textDark: "#f472b6" },
  important: { border: "#a855f7", bg: "#581c87", bgDark: "#581c87", text: "#f3e8ff", textDark: "#c084fc" },
  caution: { border: "#f43f5e", bg: "#881337", bgDark: "#881337", text: "#ffe4e6", textDark: "#fb7185" },
  attention: { border: "#d946ef", bg: "#701a75", bgDark: "#701a75", text: "#fae8ff", textDark: "#e879f9" },
  hint: { border: "#10b981", bg: "#064e3b", bgDark: "#064e3b", text: "#d1fae5", textDark: "#34d399" },
  check: { border: "#6366f1", bg: "#3730a3", bgDark: "#3730a3", text: "#e0e7ff", textDark: "#818cf8" },
  summary: { border: "#8b5cf6", bg: "#4c1d95", bgDark: "#4c1d95", text: "#ede9fe", textDark: "#a78bfa" },
}

export function AdmonitionEditor({ initialData, onSave, onCancel }: AdmonitionEditorProps) {
  const [data, setData] = useState<AdmonitionData>(
    initialData || {
      title: "",
      content: "",
      type: "note",
      design: "default",
    }
  )
  const [typeDropdownOpen, setTypeDropdownOpen] = useState(false)
  const settings = useEditorSettings("admonition")

  const handleSave = () => {
    onSave(data)
  }

  return (
    <BlockEditorShell
      settings={settings}
      icon={<AlertCircle className="h-5 w-5 text-blue-600 dark:text-blue-400" />}
      title="Admonition Editor"
      headerMeta={
        <>
          <div className="flex items-center gap-2 text-sm">
            <span className="text-gray-600 dark:text-gray-400">Type:</span>
            <div className="flex items-center gap-1 font-medium text-gray-800 dark:text-gray-200 capitalize bg-gray-100 dark:bg-gray-800 px-2 py-1 rounded">
              {typeToIcon[data.type]}
              <span>{data.type}</span>
            </div>
          </div>
          <div className="flex items-center gap-2 text-sm">
            <span className="text-gray-600 dark:text-gray-400">Design:</span>
            <span className="font-medium text-gray-800 dark:text-gray-200 capitalize bg-gray-100 dark:bg-gray-800 px-2 py-1 rounded">
              {designStyles.find(s => s.value === (data.design || "default"))?.label}
            </span>
          </div>
        </>
      }
      secondaryHeader={
        <div className="flex items-center gap-4 p-4">
          <div className="flex items-center gap-2">
            <Label className="text-sm font-medium text-gray-700 dark:text-gray-300">
              Admonition Type:
            </Label>
            <DropdownMenu open={typeDropdownOpen} onOpenChange={setTypeDropdownOpen}>
              <DropdownMenuTrigger render={<Button variant="outline" 
                  className="gap-2 bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-700" />}>
                {typeToIcon[data.type]}
                <span className="capitalize">{data.type}</span>
              </DropdownMenuTrigger>
              <DropdownMenuContent className="w-[420px] bg-white dark:bg-gray-800 border-gray-200 dark:border-gray-700">
                <div className="grid grid-cols-2 gap-1 p-1">
                  {admonitionTypes.map((type) => (
                    <DropdownMenuItem
                      key={type.value}
                      onSelect={(e) => {
                        e.preventDefault()
                        setData((prev) => ({ ...prev, type: type.value }))
                      }}
                      className="cursor-pointer dark:hover:bg-gray-700 dark:focus:bg-gray-700"
                    >
                      <div className="flex items-center gap-2">
                        {typeToIcon[type.value]}
                        <span>{type.label}</span>
                      </div>
                    </DropdownMenuItem>
                  ))}
                </div>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>

          <div className="flex items-center gap-2">
            <Label className="text-sm font-medium text-gray-700 dark:text-gray-300">
              Design Style:
            </Label>
            <DropdownMenu>
              <DropdownMenuTrigger render={<Button variant="outline" 
                  className="gap-2 bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-700" />}>
                <span className="capitalize">{designStyles.find(s => s.value === (data.design || "default"))?.label}</span>
              </DropdownMenuTrigger>
              <DropdownMenuContent className="w-[500px] bg-white dark:bg-gray-800 border-gray-200 dark:border-gray-700 p-3">
                <div className="space-y-2">
                  {designStyles.map((style) => (
                    <button
                      key={style.value}
                      onClick={() => setData((prev) => ({ ...prev, design: style.value }))}
                      className={`w-full text-left p-3 rounded-lg border-2 transition-all hover:border-blue-500 dark:hover:border-blue-400 ${
                        data.design === style.value || (!data.design && style.value === "default")
                          ? "border-blue-500 dark:border-blue-400 bg-blue-50 dark:bg-blue-950/30"
                          : "border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900"
                      }`}
                    >
                      <div className="flex items-start gap-3">
                        <div className="flex-1">
                          <div className="font-medium text-gray-900 dark:text-gray-100 mb-1">
                            {style.label}
                          </div>
                          <div className="text-xs text-gray-500 dark:text-gray-400 mb-2">
                            {style.description}
                          </div>
                          {/* Miniature preview */}
                          <div className="relative">
                            {style.value === "default" && (
                              <div 
                                className="h-12 rounded border p-1.5 text-xs flex flex-col gap-0.5"
                                style={{
                                  borderColor: typeToColorValues[data.type].border,
                                  backgroundColor: typeToColorValues[data.type].bgDark + "cc", // 80% opacity
                                }}
                              >
                                <div className="font-semibold flex items-center gap-1" style={{ color: typeToColorValues[data.type].text }}>
                                  <div className="w-3 h-3 flex items-center justify-center" style={{ color: typeToColorValues[data.type].text }}>
                                    {typeToIconMiniature[data.type]}
                                  </div>
                                  <span>Example</span>
                                </div>
                                <div className="bg-white dark:bg-gray-950 text-gray-900 dark:text-gray-100 rounded px-1.5 py-0.5 text-[10px]">
                                  Sample text
                                </div>
                              </div>
                            )}
                            {style.value === "compact" && (
                              <div 
                                className="h-12 rounded border-l-4 p-2 text-xs"
                                style={{
                                  borderColor: typeToColorValues[data.type].border,
                                  backgroundColor: typeToColorValues[data.type].border + "40", // 25% opacity
                                }}
                              >
                                <div className="font-semibold flex items-center gap-1">
                                  <div className="w-3 h-3 rounded-full" style={{ backgroundColor: typeToColorValues[data.type].border }}></div>
                                  <span className="text-gray-900 dark:text-gray-100">Example</span>
                                </div>
                                <div className="text-gray-700 dark:text-gray-300 ml-4">Sample text</div>
                              </div>
                            )}
                            {style.value === "bordered" && (
                              <div 
                                className="h-12 rounded border-2 bg-white dark:bg-gray-900 p-2 text-xs"
                                style={{ borderColor: typeToColorValues[data.type].border + "4d" }} // 30% opacity
                              >
                                <div className="font-semibold" style={{ color: typeToColorValues[data.type].textDark }}>Example</div>
                                <div className="text-gray-700 dark:text-gray-300">Sample text</div>
                              </div>
                            )}
                            {style.value === "vertical-bar" && (
                              <div className="h-12 rounded bg-gray-100 dark:bg-gray-800 p-2 text-xs relative pl-3">
                                <div className="absolute left-0 top-0 bottom-0 w-1 rounded-l" style={{ backgroundColor: typeToColorValues[data.type].border }}></div>
                                <div className="font-semibold" style={{ color: typeToColorValues[data.type].textDark }}>Example</div>
                                <div className="text-gray-700 dark:text-gray-300">Sample text</div>
                              </div>
                            )}
                          </div>
                        </div>
                        {(data.design === style.value || (!data.design && style.value === "default")) && (
                          <Check className="h-5 w-5 text-blue-600 dark:text-blue-400 shrink-0" />
                        )}
                      </div>
                    </button>
                  ))}
                </div>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
      }
      footer={
        <div className="flex items-center justify-end gap-2">
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
            Save Admonition
          </Button>
        </div>
      }
      onClose={onCancel}
    >
      {/* Editor Content */}
      <div className="flex-1 flex min-h-0">
          {/* Left Panel - Editor */}
          <div className="w-1/2 border-r border-gray-200 dark:border-gray-800 flex flex-col bg-white dark:bg-gray-900">
            <div className="p-4 border-b border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900">
              <h3 className="font-medium flex items-center gap-2 text-gray-800 dark:text-gray-200">
                <FileText className="h-4 w-4 text-blue-600 dark:text-blue-400" />
                Content Editor
              </h3>
            </div>

            <div className="flex-1 p-4 overflow-auto bg-white dark:bg-gray-950">
              <div className="space-y-4">
                {/* Title */}
                <div className="space-y-2">
                  <Label htmlFor="title" className="text-sm font-medium text-gray-700 dark:text-gray-300">
                    Title (optional)
                  </Label>
                  <Input
                    id="title"
                    value={data.title || ""}
                    onChange={(e) => setData((prev) => ({ ...prev, title: e.target.value }))}
                    placeholder="Custom title (leave empty for default)"
                    className="bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600 focus:border-blue-500 dark:focus:border-blue-400"
                  />
                  <p className="text-xs text-gray-500 dark:text-gray-400">
                    Leave empty to use the default title for the selected type
                  </p>
                </div>

                {/* Content */}
                <div className="space-y-2">
                  <Label htmlFor="content" className="text-sm font-medium text-gray-700 dark:text-gray-300">
                    Content *
                  </Label>
                  <Textarea
                    id="content"
                    value={data.content}
                    onChange={(e) => { setData((prev) => ({ ...prev, content: e.target.value })); }}
                    placeholder="Enter your admonition content here..."
                    rows={12}
                    className="bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600 focus:border-blue-500 dark:focus:border-blue-400 font-mono text-sm"
                  />
                </div>

                {/* Custom Colors */}
                <div className="space-y-4 pt-4 border-t border-gray-200 dark:border-gray-800">
                  <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300">Custom Colors (optional)</h4>

                  <div className="space-y-2">
                    <Label htmlFor="border-color" className="text-sm font-medium text-gray-700 dark:text-gray-300">
                      Border & Background Color
                    </Label>
                    <div className="flex gap-2 items-center">
                      <Input
                        id="border-color"
                        type="color"
                        value={data.customBorderColor || "#3b82f6"}
                        onChange={(e) => setData((prev) => ({ ...prev, customBorderColor: e.target.value }))}
                        className="w-16 h-10 cursor-pointer p-1"
                      />
                      <Input
                        type="text"
                        value={data.customBorderColor || ""}
                        onChange={(e) => setData((prev) => ({ ...prev, customBorderColor: e.target.value }))}
                        placeholder="#3b82f6"
                        className="flex-1 bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600"
                      />
                      {data.customBorderColor && (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setData((prev) => ({ ...prev, customBorderColor: "" }))}
                          className="border-gray-300 dark:border-gray-600"
                        >
                          Reset
                        </Button>
                      )}
                    </div>
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="text-color" className="text-sm font-medium text-gray-700 dark:text-gray-300">
                      Text Color
                    </Label>
                    <div className="flex gap-2 items-center">
                      <Input
                        id="text-color"
                        type="color"
                        value={data.customTextColor || "#ffffff"}
                        onChange={(e) => setData((prev) => ({ ...prev, customTextColor: e.target.value }))}
                        className="w-16 h-10 cursor-pointer p-1"
                      />
                      <Input
                        type="text"
                        value={data.customTextColor || ""}
                        onChange={(e) => setData((prev) => ({ ...prev, customTextColor: e.target.value }))}
                        placeholder="#ffffff"
                        className="flex-1 bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600"
                      />
                      {data.customTextColor && (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setData((prev) => ({ ...prev, customTextColor: "" }))}
                          className="border-gray-300 dark:border-gray-600"
                        >
                          Reset
                        </Button>
                      )}
                    </div>
                  </div>

                  <p className="text-xs text-gray-500 dark:text-gray-400">
                    Custom colors override the default colors for the selected type
                  </p>
                </div>
              </div>
            </div>
          </div>

          {/* Right Panel - Preview */}
          <div className="w-1/2 flex flex-col bg-white dark:bg-gray-900">
            <div className="p-4 border-b border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900">
              <h3 className="font-medium flex items-center gap-2 text-gray-800 dark:text-gray-200">
                <Eye className="h-4 w-4 text-blue-600 dark:text-blue-400" />
                Live Preview
              </h3>
            </div>
            <div className="flex-1 p-8 overflow-auto bg-white dark:bg-gray-950">
              <Admonition
                title={data.title}
                content={data.content || "Enter your content here..."}
                type={data.type}
                customBorderColor={data.customBorderColor}
                customTextColor={data.customTextColor}
                design={data.design}
              />
            </div>
          </div>
        </div>
    </BlockEditorShell>
  )
}
