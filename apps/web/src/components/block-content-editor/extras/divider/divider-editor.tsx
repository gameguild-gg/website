"use client"

import { useState } from "react"
import { Button } from "@game-guild/ui/components/button"
import { Input } from "@game-guild/ui/components/input"
import { Label } from "@game-guild/ui/components/label"
import { Save, Minus, Eye } from "lucide-react"
import type { DividerData, DividerStyle, DividerThickness, DividerSpacing, ColorPalette } from "@/components/block-content-editor/nodes/divider-node"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@game-guild/ui/components/dropdown-menu"
import {
  getThicknessStyles,
  getSpacingStyles,
  getColorStyles,
  getStyleClasses,
  getPaletteColor,
} from "./divider-styles"
import { useEditorSettings } from "@/components/block-content-editor/extras/settings-menu"
import { BlockEditorShell } from "@/components/block-content-editor/extras/block-editor-shell"

interface DividerEditorProps {
  initialData?: DividerData
  onSave: (data: DividerData) => void
  onCancel: () => void
}

const styles: { value: DividerStyle; label: string; description: string }[] = [
  { value: "simple", label: "Simple", description: "Clean single line divider" },
  { value: "double", label: "Double", description: "Two parallel horizontal lines" },
  { value: "dashed", label: "Dashed", description: "Dashed line pattern" },
  { value: "dotted", label: "Dotted", description: "Dotted line pattern" },
  { value: "gradient", label: "Gradient", description: "Smooth color fade effect" },
]

const thicknesses: { value: DividerThickness; label: string }[] = [
  { value: "thin", label: "Thin" },
  { value: "medium", label: "Medium" },
  { value: "thick", label: "Thick" },
]

const spacings: { value: DividerSpacing; label: string }[] = [
  { value: "xs", label: "Extra Small" },
  { value: "sm", label: "Small" },
  { value: "md", label: "Medium" },
  { value: "lg", label: "Large" },
  { value: "xl", label: "Extra Large" },
]

const colorPalettes: { value: ColorPalette; label: string; description: string; color: string }[] = [
  { value: "blue", label: "Blue", description: "Professional and trustworthy", color: "#3b82f6" },
  { value: "green", label: "Green", description: "Fresh and natural", color: "#10b981" },
  { value: "orange", label: "Orange", description: "Energetic and warm", color: "#f97316" },
  { value: "red", label: "Red", description: "Bold and attention-grabbing", color: "#ef4444" },
  { value: "purple", label: "Purple", description: "Creative and elegant", color: "#a855f7" },
  { value: "custom", label: "Custom", description: "Your own color", color: "#6b7280" },
]

export function DividerEditor({ initialData, onSave, onCancel }: DividerEditorProps) {
  const [data, setData] = useState<DividerData>(
    initialData || {
      style: "simple",
      thickness: "medium",
      spacing: "md",
      colorPalette: "blue",
      customColor: "#3b82f6",
    }
  )
  const settings = useEditorSettings("divider")

  const handleSave = () => {
    onSave(data)
  }

  const handleCancel = () => {
    onCancel()
  }

  const renderPreviewDivider = () => {
    const spacingClass = getSpacingStyles(data.spacing)
    const thicknessClass = getThicknessStyles(data.thickness, data.style)
    const colorClass = getColorStyles(data.colorPalette, data.style)
    const styleClass = getStyleClasses(data.style)
    const paletteColor = getPaletteColor(data.colorPalette, data.customColor)

    const customStyle = data.colorPalette === "custom" && data.customColor ? {
      borderColor: data.customColor,
      backgroundColor: data.customColor,
    } : {}

    switch (data.style) {
      case "gradient":
        return (
          <div className={`${spacingClass} ${thicknessClass} ${colorClass}`} style={customStyle} aria-hidden="true" />
        )
      case "double": {
        // Duas linhas perpendiculares (paralelas horizontais)
        const doubleThickness = data.thickness === "thin" ? "1px" : data.thickness === "medium" ? "2px" : "3px"
        const doubleGap = data.thickness === "thin" ? "2px" : data.thickness === "medium" ? "3px" : "4px"
        return (
          <div className={spacingClass}>
            <div 
              className="relative"
              style={{ 
                height: `calc(${doubleThickness} * 2 + ${doubleGap})`,
              }}
            >
              <div 
                className="absolute top-0 left-0 right-0"
                style={{ 
                  height: doubleThickness,
                  backgroundColor: paletteColor
                }}
              />
              <div 
                className="absolute bottom-0 left-0 right-0"
                style={{ 
                  height: doubleThickness,
                  backgroundColor: paletteColor
                }}
              />
            </div>
          </div>
        )
      }
      default:
        return <hr className={`${spacingClass} ${thicknessClass} ${colorClass} ${styleClass}`} style={customStyle} />
    }
  }

  return (
    <BlockEditorShell
      settings={settings}
      includeMonacoTheme={false}
      onClose={handleCancel}
      icon={<Minus className="h-5 w-5 text-blue-600 dark:text-blue-400" />}
      title="Divider Editor"
      headerMeta={
        <>
          <div className="flex items-center gap-2 text-sm">
            <span className="text-gray-600 dark:text-gray-400">Style:</span>
            <span className="font-medium text-gray-800 dark:text-gray-200 capitalize bg-gray-100 dark:bg-gray-800 px-2 py-1 rounded">
              {styles.find((s) => s.value === data.style)?.label}
            </span>
          </div>
          <div className="flex items-center gap-2 text-sm">
            <span className="text-gray-600 dark:text-gray-400">Thickness:</span>
            <span className="font-medium text-gray-800 dark:text-gray-200 capitalize bg-gray-100 dark:bg-gray-800 px-2 py-1 rounded">
              {thicknesses.find((t) => t.value === data.thickness)?.label}
            </span>
          </div>
        </>
      }
      secondaryHeader={
        <div className="flex items-center gap-4 p-4">
          <div className="flex items-center gap-2">
            <Label className="text-sm font-medium text-gray-700 dark:text-gray-300">
              Style:
            </Label>
            <DropdownMenu>
              <DropdownMenuTrigger render={<Button variant="outline"
                  className="gap-2 bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-700" />}>
                <span>{styles.find((s) => s.value === data.style)?.label}</span>
              </DropdownMenuTrigger>
              <DropdownMenuContent className="w-[300px] bg-white dark:bg-gray-800 border-gray-200 dark:border-gray-700 p-3">
                <div className="space-y-2">
                  {styles.map((style) => (
                    <button
                      key={style.value}
                      onClick={() => setData((prev) => ({ ...prev, style: style.value }))}
                      className={`w-full text-left p-3 rounded transition-all hover:bg-gray-100 dark:hover:bg-gray-700 ${
                        data.style === style.value
                          ? "bg-blue-50 dark:bg-blue-950/30 ring-2 ring-blue-500 dark:ring-blue-400"
                          : "border border-gray-200 dark:border-gray-600"
                      }`}
                    >
                      <div className="font-medium text-sm text-gray-900 dark:text-gray-100 mb-1">
                        {style.label}
                      </div>
                      <div className="text-xs text-gray-500 dark:text-gray-400">
                        {style.description}
                      </div>
                    </button>
                  ))}
                </div>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>

          <div className="flex items-center gap-2">
            <Label className="text-sm font-medium text-gray-700 dark:text-gray-300">
              Thickness:
            </Label>
            <DropdownMenu>
              <DropdownMenuTrigger render={<Button variant="outline"
                  className="gap-2 bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-700" />}>
                <span>{thicknesses.find((t) => t.value === data.thickness)?.label}</span>
              </DropdownMenuTrigger>
              <DropdownMenuContent className="bg-white dark:bg-gray-800 border-gray-200 dark:border-gray-700">
                {thicknesses.map((thickness) => (
                  <DropdownMenuItem
                    key={thickness.value}
                    onClick={() => setData((prev) => ({ ...prev, thickness: thickness.value }))}
                    className="cursor-pointer dark:hover:bg-gray-700 dark:focus:bg-gray-700"
                  >
                    {thickness.label}
                  </DropdownMenuItem>
                ))}
              </DropdownMenuContent>
            </DropdownMenu>
          </div>

          <div className="flex items-center gap-2">
            <Label className="text-sm font-medium text-gray-700 dark:text-gray-300">
              Spacing:
            </Label>
            <DropdownMenu>
              <DropdownMenuTrigger render={<Button variant="outline"
                  className="gap-2 bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-700" />}>
                <span>{spacings.find((s) => s.value === data.spacing)?.label}</span>
              </DropdownMenuTrigger>
              <DropdownMenuContent className="bg-white dark:bg-gray-800 border-gray-200 dark:border-gray-700">
                {spacings.map((spacing) => (
                  <DropdownMenuItem
                    key={spacing.value}
                    onClick={() => setData((prev) => ({ ...prev, spacing: spacing.value }))}
                    className="cursor-pointer dark:hover:bg-gray-700 dark:focus:bg-gray-700"
                  >
                    {spacing.label}
                  </DropdownMenuItem>
                ))}
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
      }
      footer={
        <div className="flex items-center justify-end gap-2">
          <Button
            variant="outline"
            onClick={handleCancel}
            className="border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-800 bg-transparent"
          >
            Cancel
          </Button>
          <Button
            onClick={handleSave}
            className="flex items-center gap-2 bg-blue-600 hover:bg-blue-700 dark:bg-blue-500 dark:hover:bg-blue-600"
          >
            <Save className="h-4 w-4" />
            Save Divider
          </Button>
        </div>
      }
    >
      {/* Editor Content */}
      <div className="flex-1 flex min-h-0">
          {/* Left Panel - Settings */}
          <div className="w-1/2 border-r border-gray-200 dark:border-gray-800 flex flex-col bg-white dark:bg-gray-900">
            <div className="p-4 border-b border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900">
              <h3 className="font-medium flex items-center gap-2 text-gray-800 dark:text-gray-200">
                <Minus className="h-4 w-4 text-blue-600 dark:text-blue-400" />
                Divider Settings
              </h3>
            </div>

            <div className="flex-1 p-6 overflow-auto bg-white dark:bg-gray-950">
              <div className="space-y-6">
                {/* Color Palette */}
                <div className="space-y-4 p-4 bg-purple-50 dark:bg-purple-950/20 rounded-lg border border-purple-200 dark:border-purple-800">
                  <h4 className="text-sm font-semibold text-purple-900 dark:text-purple-100">Color Palette</h4>
                  
                  <div className="grid grid-cols-2 gap-3">
                    {colorPalettes.map((palette) => (
                      <button
                        key={palette.value}
                        onClick={() => setData((prev) => ({ 
                          ...prev, 
                          colorPalette: palette.value,
                          customColor: palette.value === "custom" ? (prev.customColor || "#3b82f6") : prev.customColor
                        }))}
                        className={`p-3 rounded-lg border-2 transition-all text-left ${
                          data.colorPalette === palette.value
                            ? "border-purple-500 bg-purple-100 dark:bg-purple-900/30"
                            : "border-gray-200 dark:border-gray-700 hover:border-purple-300 dark:hover:border-purple-700"
                        }`}
                      >
                        <div className="flex items-center gap-2 mb-1">
                          <div 
                            className="w-4 h-4 rounded"
                            style={{ backgroundColor: palette.color }}
                          />
                          <span className="font-medium text-sm text-gray-900 dark:text-gray-100">
                            {palette.label}
                          </span>
                        </div>
                        <p className="text-xs text-gray-600 dark:text-gray-400">
                          {palette.description}
                        </p>
                      </button>
                    ))}
                  </div>

                  {/* Custom Color Picker */}
                  {data.colorPalette === "custom" && (
                    <div className="pt-3 border-t border-purple-200 dark:border-purple-800">
                      <Label htmlFor="custom-color" className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-2 block">
                        Custom Color
                      </Label>
                      <div className="flex items-center gap-2">
                        <Input
                          id="custom-color"
                          type="color"
                          value={data.customColor || "#3b82f6"}
                          onChange={(e) => setData((prev) => ({ ...prev, customColor: e.target.value }))}
                          className="h-10 w-full cursor-pointer"
                        />
                      </div>
                    </div>
                  )}
                </div>

                {/* Info Section */}
                <div className="p-4 bg-blue-50 dark:bg-blue-950/30 border border-blue-200 dark:border-blue-800 rounded-lg">
                  <h4 className="text-sm font-medium text-blue-900 dark:text-blue-100 mb-2">
                    About Dividers
                  </h4>
                  <p className="text-xs text-blue-700 dark:text-blue-300">
                    Dividers help organize content by creating visual separation between sections. Choose from various styles to match your design aesthetic.
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
            <div className="flex-1 p-8 overflow-auto bg-white dark:bg-gray-950 flex items-center justify-center">
              <div className="space-y-4 w-full max-w-2xl">
                <div className="text-center text-sm text-gray-600 dark:text-gray-400 mb-8">
                  Divider appearance in your content
                </div>
                
                {/* Preview with context */}
                <div className="space-y-6">
                  <div className="p-4 bg-gray-50 dark:bg-gray-800/30 rounded-lg">
                    <p className="text-sm text-gray-700 dark:text-gray-300 mb-4">
                      Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.
                    </p>
                    
                    {renderPreviewDivider()}
                    
                    <p className="text-sm text-gray-700 dark:text-gray-300 mt-4">
                      Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.
                    </p>
                  </div>
                </div>

                {/* Visual indicators */}
                <div className="mt-8 p-4 bg-gray-50 dark:bg-gray-800/50 rounded-lg border border-gray-200 dark:border-gray-700">
                  <div className="space-y-2 text-xs">
                    <div className="flex justify-between">
                      <span className="text-gray-600 dark:text-gray-400">Style:</span>
                      <span className="font-medium text-gray-900 dark:text-gray-100">{styles.find((s) => s.value === data.style)?.label}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-gray-600 dark:text-gray-400">Thickness:</span>
                      <span className="font-medium text-gray-900 dark:text-gray-100">{thicknesses.find((t) => t.value === data.thickness)?.label}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-gray-600 dark:text-gray-400">Spacing:</span>
                      <span className="font-medium text-gray-900 dark:text-gray-100">{spacings.find((s) => s.value === data.spacing)?.label}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-gray-600 dark:text-gray-400">Color:</span>
                      <span className="font-medium text-gray-900 dark:text-gray-100">{colorPalettes.find((c) => c.value === data.colorPalette)?.label}</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
    </BlockEditorShell>
  )
}
