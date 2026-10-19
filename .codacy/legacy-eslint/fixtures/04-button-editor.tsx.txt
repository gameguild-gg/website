"use client"

import { useState } from "react"
import { Button } from "@game-guild/ui/components/button"
import { Input } from "@game-guild/ui/components/input"
import { Label } from "@game-guild/ui/components/label"
import { Switch } from "@game-guild/ui/components/switch"
import { 
  Save, MousePointerClick, Eye, ExternalLink, Download, Copy, Mail,
  Link, Link2,
  ArrowDownToLine, FileDown, ClipboardCopy, CopyCheck,
  AtSign, Send,
  MoveUp, MoveDown, MoveLeft, MoveRight
} from "lucide-react"
import type { ButtonData, ButtonActionType, ButtonVariant, ButtonSize, IconVariant, IconPosition, IconSize, ColorPalette } from "@/components/block-content-editor/nodes/button-node"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@game-guild/ui/components/dropdown-menu"
import {
  BASE_BUTTON_STYLES,
  getSizeStyles,
  getVariantBaseStyles,
  getLayoutStyles,
  getIconSpacingClass,
  getIconSizeClass,
  getColorStyles,
  getFontFamilyClass,
  getFontSizeClass,
} from "@/components/block-content-editor/extras/button/button-styles"
import { useEditorSettings } from "@/components/block-content-editor/extras/settings-menu"
import { BlockEditorShell } from "@/components/block-content-editor/extras/block-editor-shell"

interface ButtonEditorProps {
  initialData?: ButtonData
  onSave: (data: ButtonData) => void
  onCancel: () => void
}

const actionTypes: { value: ButtonActionType; label: string; icon: React.ReactNode }[] = [
  { value: "url", label: "Open Page", icon: <ExternalLink className="h-4 w-4" /> },
  { value: "download", label: "Download File", icon: <Download className="h-4 w-4" /> },
  { value: "copy", label: "Copy Text", icon: <Copy className="h-4 w-4" /> },
  { value: "email", label: "Send Email", icon: <Mail className="h-4 w-4" /> },
]

const variants: { value: ButtonVariant; label: string; description: string }[] = [
  { value: "solid", label: "Solid", description: "Bold button with vibrant gradient background" },
  { value: "outline", label: "Outline", description: "Elegant border with hover fill effect" },
  { value: "soft", label: "Soft", description: "Subtle background with smooth transitions" },
  { value: "minimal", label: "Minimal", description: "Clean underline style, text-focused" },
]

const sizes: { value: ButtonSize; label: string }[] = [
  { value: "sm", label: "Small" },
  { value: "md", label: "Medium" },
  { value: "lg", label: "Large" },
  { value: "xl", label: "Extra Large" },
  { value: "xxl", label: "Ultra Large" },
]

const iconSizes: { value: IconSize; label: string }[] = [
  { value: "sm", label: "Small" },
  { value: "md", label: "Medium" },
  { value: "lg", label: "Large" },
]

const iconPositions: { value: IconPosition; label: string; icon: React.ReactNode }[] = [
  { value: "left", label: "Left", icon: <MoveLeft className="h-4 w-4" /> },
  { value: "right", label: "Right", icon: <MoveRight className="h-4 w-4" /> },
  { value: "top", label: "Top", icon: <MoveUp className="h-4 w-4" /> },
  { value: "bottom", label: "Bottom", icon: <MoveDown className="h-4 w-4" /> },
]

const getIconVariantsByType = (type: ButtonActionType) => {
  const variants = {
    url: [
      { value: 0 as IconVariant, icon: <ExternalLink className="h-5 w-5" />, label: "External Link" },
      { value: 1 as IconVariant, icon: <Link2 className="h-5 w-5" />, label: "Chain Link" },
      { value: 2 as IconVariant, icon: <Link className="h-5 w-5" />, label: "Simple Link" },
    ],
    download: [
      { value: 0 as IconVariant, icon: <Download className="h-5 w-5" />, label: "Download Arrow" },
      { value: 1 as IconVariant, icon: <ArrowDownToLine className="h-5 w-5" />, label: "Arrow to Line" },
      { value: 2 as IconVariant, icon: <FileDown className="h-5 w-5" />, label: "File Download" },
    ],
    copy: [
      { value: 0 as IconVariant, icon: <Copy className="h-5 w-5" />, label: "Copy" },
      { value: 1 as IconVariant, icon: <ClipboardCopy className="h-5 w-5" />, label: "Clipboard" },
      { value: 2 as IconVariant, icon: <CopyCheck className="h-5 w-5" />, label: "Copy Check" },
    ],
    email: [
      { value: 0 as IconVariant, icon: <Mail className="h-5 w-5" />, label: "Mail" },
      { value: 1 as IconVariant, icon: <AtSign className="h-5 w-5" />, label: "At Sign" },
      { value: 2 as IconVariant, icon: <Send className="h-5 w-5" />, label: "Send" },
    ],
  }
  return variants[type] || variants.url
}

const colorPalettes: { value: ColorPalette; label: string; description: string; colors: { primary: string; secondary: string } }[] = [
  { 
    value: "blue", 
    label: "Blue", 
    description: "Trust, professionalism, stability",
    colors: { primary: "#2563eb", secondary: "#6366f1" }
  },
  { 
    value: "green", 
    label: "Green", 
    description: "Success, growth, eco-friendly",
    colors: { primary: "#16a34a", secondary: "#10b981" }
  },
  { 
    value: "orange", 
    label: "Orange", 
    description: "Energy, creativity, enthusiasm",
    colors: { primary: "#ea580c", secondary: "#f59e0b" }
  },
  { 
    value: "red", 
    label: "Red", 
    description: "Urgency, passion, importance",
    colors: { primary: "#dc2626", secondary: "#f43f5e" }
  },
  { 
    value: "custom", 
    label: "Custom", 
    description: "Define your own colors",
    colors: { primary: "#3b82f6", secondary: "#8b5cf6" }
  },
]

export function ButtonEditor({ initialData, onSave, onCancel }: ButtonEditorProps) {
  const [data, setData] = useState<ButtonData>(
    initialData || {
      text: "Click me",
      url: "",
      actionType: "url",
      variant: "solid",
      size: "md",
      showIcon: true,
      iconVariant: 0,
      iconPosition: "right",
      iconSize: "md",
      colorPalette: "blue",
      customColors: {
        primary: "#3b82f6",
        secondary: "#8b5cf6",
        text: "#ffffff",
        hoverPrimary: "#1d4ed8",
        hoverSecondary: "#7c3aed",
        hoverText: "#ffffff",
      },
      fontFamily: "sans",
      fontSize: "md",
    }
  )
  const settings = useEditorSettings("button")

  const handleSave = () => {
    onSave(data)
  }

  const handleCancel = () => {
    onCancel()
  }

  const getActionIcon = () => {
    const iconSizeClass = getIconSizeClass(data.size, data.iconSize)

    const iconsByType = {
      url: [
        <ExternalLink className={iconSizeClass} key="url-0" />,
        <Link2 className={iconSizeClass} key="url-1" />,
        <Link className={iconSizeClass} key="url-2" />,
      ],
      download: [
        <Download className={iconSizeClass} key="download-0" />,
        <ArrowDownToLine className={iconSizeClass} key="download-1" />,
        <FileDown className={iconSizeClass} key="download-2" />,
      ],
      copy: [
        <Copy className={iconSizeClass} key="copy-0" />,
        <ClipboardCopy className={iconSizeClass} key="copy-1" />,
        <CopyCheck className={iconSizeClass} key="copy-2" />,
      ],
      email: [
        <Mail className={iconSizeClass} key="email-0" />,
        <AtSign className={iconSizeClass} key="email-1" />,
        <Send className={iconSizeClass} key="email-2" />,
      ],
    }

    return iconsByType[data.actionType][data.iconVariant] || iconsByType[data.actionType][0]
  }

  const getButtonStyles = () => {
    const isVerticalIcon = data.showIcon && (data.iconPosition === "top" || data.iconPosition === "bottom")
    
    return `${BASE_BUTTON_STYLES} ${getSizeStyles(data.size, isVerticalIcon)} ${getVariantBaseStyles(data.variant, data.size)} ${getColorStyles(data.colorPalette, data.variant)} ${getLayoutStyles(data.iconPosition)} ${getFontFamilyClass(data.fontFamily)} ${getFontSizeClass(data.size, data.fontSize)}`
  }

  const getCustomStyle = () => {
    if (data.colorPalette === "custom" && data.customColors) {
      const { primary, secondary, text, hoverPrimary, hoverSecondary, hoverText } = data.customColors
      if (data.variant === "solid") {
        return {
          background: `linear-gradient(to right, ${primary}, ${secondary})`,
          color: text,
          "--hover-bg": `linear-gradient(to right, ${hoverPrimary}, ${hoverSecondary})`,
          "--hover-text": hoverText,
        } as React.CSSProperties
      } else if (data.variant === "outline") {
        return {
          borderColor: primary,
          color: text,
          "--hover-bg": hoverPrimary,
          "--hover-border": hoverPrimary,
          "--hover-text": hoverText,
        } as React.CSSProperties
      } else if (data.variant === "soft") {
        return {
          backgroundColor: `${primary}20`,
          color: text,
          "--hover-bg": `${hoverPrimary}30`,
          "--hover-text": hoverText,
        } as React.CSSProperties
      } else if (data.variant === "minimal") {
        return {
          color: text,
          "--hover-border": hoverPrimary,
          "--hover-text": hoverText,
        } as React.CSSProperties
      }
    }
    return {}
  }

  const getUrlPlaceholder = () => {
    switch (data.actionType) {
      case "url":
        return "https://example.com"
      case "download":
        return "https://example.com/file.pdf"
      case "copy":
        return "Text to be copied"
      case "email":
        return "email@example.com"
      default:
        return ""
    }
  }

  const getUrlLabel = () => {
    switch (data.actionType) {
      case "url":
        return "Page URL"
      case "download":
        return "File URL"
      case "copy":
        return "Text to Copy"
      case "email":
        return "Email Address"
      default:
        return "URL"
    }
  }

  return (
    <BlockEditorShell
      settings={settings}
      includeMonacoTheme={false}
      onClose={handleCancel}
      icon={<MousePointerClick className="h-5 w-5 text-blue-600 dark:text-blue-400" />}
      title="Button Editor"
      headerMeta={
        <>
          <div className="flex items-center gap-2 text-sm">
            <span className="text-gray-600 dark:text-gray-400">Action:</span>
            <div className="flex items-center gap-1 font-medium text-gray-800 dark:text-gray-200 capitalize bg-gray-100 dark:bg-gray-800 px-2 py-1 rounded">
              {actionTypes.find((a) => a.value === data.actionType)?.icon}
              <span>{actionTypes.find((a) => a.value === data.actionType)?.label}</span>
            </div>
          </div>
          <div className="flex items-center gap-2 text-sm">
            <span className="text-gray-600 dark:text-gray-400">Style:</span>
            <span className="font-medium text-gray-800 dark:text-gray-200 capitalize bg-gray-100 dark:bg-gray-800 px-2 py-1 rounded">
              {variants.find((v) => v.value === data.variant)?.label}
            </span>
          </div>
        </>
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
            Save Button
          </Button>
        </div>
      }
    >
        {/* Settings Bar */}
        <div className="flex items-center gap-4 p-4 border-b border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900">
          <div className="flex items-center gap-2">
            <Label className="text-sm font-medium text-gray-700 dark:text-gray-300">
              Action Type:
            </Label>
            <DropdownMenu>
              <DropdownMenuTrigger render={<Button variant="outline"
                  className="gap-2 bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-700" />}>
                {actionTypes.find((a) => a.value === data.actionType)?.icon}
                <span>{actionTypes.find((a) => a.value === data.actionType)?.label}</span>
              </DropdownMenuTrigger>
              <DropdownMenuContent className="bg-white dark:bg-gray-800 border-gray-200 dark:border-gray-700">
                {actionTypes.map((action) => (
                  <DropdownMenuItem
                    key={action.value}
                    onClick={() => setData((prev) => ({ ...prev, actionType: action.value }))}
                    className="cursor-pointer dark:hover:bg-gray-700 dark:focus:bg-gray-700"
                  >
                    <div className="flex items-center gap-2">
                      {action.icon}
                      <span>{action.label}</span>
                    </div>
                  </DropdownMenuItem>
                ))}
              </DropdownMenuContent>
            </DropdownMenu>
          </div>

          <div className="flex items-center gap-2">
            <Label className="text-sm font-medium text-gray-700 dark:text-gray-300">
              Style:
            </Label>
            <DropdownMenu>
              <DropdownMenuTrigger render={<Button variant="outline"
                  className="gap-2 bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-700" />}>
                <span className="capitalize">{variants.find((v) => v.value === data.variant)?.label}</span>
              </DropdownMenuTrigger>
              <DropdownMenuContent className="w-[400px] bg-white dark:bg-gray-800 border-gray-200 dark:border-gray-700 p-3">
                <div className="space-y-2">
                  {variants.map((variant) => {
                    const miniVariantStyles = {
                      solid: "bg-gradient-to-r from-blue-600 to-purple-600 text-white shadow-md",
                      outline: "border-2 border-blue-600 text-blue-600 dark:text-blue-400 dark:border-blue-400 bg-transparent",
                      soft: "bg-blue-100 text-blue-900 dark:bg-blue-900/30 dark:text-blue-100",
                      minimal: "text-blue-600 dark:text-blue-400 bg-transparent border-b-2 border-blue-600 dark:border-blue-400 rounded-none",
                    }
                    
                    return (
                      <button
                        key={variant.value}
                        onClick={() => setData((prev) => ({ ...prev, variant: variant.value }))}
                        className={`w-full text-left p-3 rounded transition-all hover:bg-gray-100 dark:hover:bg-gray-700 ${
                          data.variant === variant.value
                            ? "bg-blue-50 dark:bg-blue-950/30 ring-2 ring-blue-500 dark:ring-blue-400"
                            : "border border-gray-200 dark:border-gray-600"
                        }`}
                      >
                        <div className="flex items-center justify-between gap-3">
                          <div className="flex-1">
                            <div className="font-medium text-sm text-gray-900 dark:text-gray-100 mb-1">
                              {variant.label}
                            </div>
                            <div className="text-xs text-gray-500 dark:text-gray-400">
                              {variant.description}
                            </div>
                          </div>
                          <div className="shrink-0">
                            <div 
                              className={`px-4 py-2 text-xs font-medium rounded inline-flex items-center ${miniVariantStyles[variant.value]}`}
                            >
                              Preview
                            </div>
                          </div>
                        </div>
                      </button>
                    )
                  })}
                </div>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>

          <div className="flex items-center gap-2">
            <Label className="text-sm font-medium text-gray-700 dark:text-gray-300">
              Size:
            </Label>
            <DropdownMenu>
              <DropdownMenuTrigger render={<Button variant="outline"
                  className="gap-2 bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-700" />}>
                <span>{sizes.find((s) => s.value === data.size)?.label}</span>
              </DropdownMenuTrigger>
              <DropdownMenuContent className="bg-white dark:bg-gray-800 border-gray-200 dark:border-gray-700">
                {sizes.map((size) => (
                  <DropdownMenuItem
                    key={size.value}
                    onClick={() => setData((prev) => ({ ...prev, size: size.value }))}
                    className="cursor-pointer dark:hover:bg-gray-700 dark:focus:bg-gray-700"
                  >
                    {size.label}
                  </DropdownMenuItem>
                ))}
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>

        {/* Editor Content */}
        <div className="flex-1 flex min-h-0">
          {/* Left Panel - Editor */}
          <div className="w-1/2 border-r border-gray-200 dark:border-gray-800 flex flex-col bg-white dark:bg-gray-900">
            <div className="p-4 border-b border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900">
              <h3 className="font-medium flex items-center gap-2 text-gray-800 dark:text-gray-200">
                <MousePointerClick className="h-4 w-4 text-blue-600 dark:text-blue-400" />
                Button Settings
              </h3>
            </div>

            <div className="flex-1 p-6 overflow-auto bg-white dark:bg-gray-950">
              <div className="space-y-6">
                {/* Button Text */}
                <div className="space-y-2">
                  <Label htmlFor="text" className="text-sm font-medium text-gray-700 dark:text-gray-300">
                    Button Text *
                  </Label>
                  <Input
                    id="text"
                    value={data.text}
                    onChange={(e) => setData((prev) => ({ ...prev, text: e.target.value }))}
                    placeholder="Click me"
                    className="bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600 focus:border-blue-500 dark:focus:border-blue-400"
                  />
                </div>

                {/* URL/Target */}
                <div className="space-y-2">
                  <Label htmlFor="url" className="text-sm font-medium text-gray-700 dark:text-gray-300">
                    {getUrlLabel()}
                  </Label>
                  <Input
                    id="url"
                    value={data.url}
                    onChange={(e) => setData((prev) => ({ ...prev, url: e.target.value }))}
                    placeholder={getUrlPlaceholder()}
                    className="bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600 focus:border-blue-500 dark:focus:border-blue-400"
                  />
                </div>

                {/* Show Icon Toggle */}
                <div className="flex items-center space-x-3 p-4 bg-gray-50 dark:bg-gray-800/50 rounded-lg border border-gray-200 dark:border-gray-700">
                  <Switch
                    id="show-icon"
                    checked={data.showIcon}
                    onCheckedChange={(checked) => setData((prev) => ({ ...prev, showIcon: checked }))}
                  />
                  <Label htmlFor="show-icon" className="text-sm font-medium text-gray-700 dark:text-gray-300 cursor-pointer">
                    Show action icon
                  </Label>
                </div>

                {/* Icon Settings */}
                {data.showIcon && (
                  <div className="space-y-4 p-4 bg-blue-50 dark:bg-blue-950/20 rounded-lg border border-blue-200 dark:border-blue-800">
                    <h4 className="text-sm font-semibold text-blue-900 dark:text-blue-100">Icon Settings</h4>
                    
                    {/* Icon Variant */}
                    <div className="space-y-2">
                      <Label className="text-sm font-medium text-gray-700 dark:text-gray-300">
                        Icon Style
                      </Label>
                      <div className="grid grid-cols-3 gap-2">
                        {getIconVariantsByType(data.actionType).map((icon) => (
                          <button
                            key={icon.value}
                            onClick={() => setData((prev) => ({ ...prev, iconVariant: icon.value }))}
                            className={`p-3 rounded-lg border-2 transition-all flex flex-col items-center gap-2 ${
                              data.iconVariant === icon.value
                                ? "border-blue-500 bg-blue-100 dark:bg-blue-900/30"
                                : "border-gray-200 dark:border-gray-700 hover:border-blue-300 dark:hover:border-blue-700"
                            }`}
                          >
                            <div className="text-gray-700 dark:text-gray-300">{icon.icon}</div>
                            <span className="text-xs text-gray-600 dark:text-gray-400">{icon.label}</span>
                          </button>
                        ))}
                      </div>
                    </div>

                    {/* Icon Size */}
                    <div className="space-y-2">
                      <Label className="text-sm font-medium text-gray-700 dark:text-gray-300">
                        Icon Size (scales with button size)
                      </Label>
                      <div className="grid grid-cols-3 gap-2">
                        {iconSizes.map((size) => (
                          <button
                            key={size.value}
                            onClick={() => setData((prev) => ({ ...prev, iconSize: size.value }))}
                            className={`p-2 rounded-lg border-2 transition-all text-sm ${
                              data.iconSize === size.value
                                ? "border-blue-500 bg-blue-100 dark:bg-blue-900/30 font-medium"
                                : "border-gray-200 dark:border-gray-700 hover:border-blue-300 dark:hover:border-blue-700"
                            }`}
                          >
                            {size.label}
                          </button>
                        ))}
                      </div>
                    </div>

                    {/* Icon Position */}
                    <div className="space-y-2">
                      <Label className="text-sm font-medium text-gray-700 dark:text-gray-300">
                        Icon Position
                      </Label>
                      <div className="grid grid-cols-2 gap-2">
                        {iconPositions.map((position) => (
                          <button
                            key={position.value}
                            onClick={() => setData((prev) => ({ ...prev, iconPosition: position.value }))}
                            className={`p-3 rounded-lg border-2 transition-all flex items-center gap-2 justify-center ${
                              data.iconPosition === position.value
                                ? "border-blue-500 bg-blue-100 dark:bg-blue-900/30 font-medium"
                                : "border-gray-200 dark:border-gray-700 hover:border-blue-300 dark:hover:border-blue-700"
                            }`}
                          >
                            {position.icon}
                            <span className="text-sm">{position.label}</span>
                          </button>
                        ))}
                      </div>
                    </div>
                  </div>
                )}

                {/* Font Settings */}
                <div className="space-y-4 p-4 bg-green-50 dark:bg-green-950/20 rounded-lg border border-green-200 dark:border-green-800">
                  <h4 className="text-sm font-semibold text-green-900 dark:text-green-100">Font Settings</h4>
                  
                  {/* Font Family */}
                  <div className="space-y-2">
                    <Label className="text-sm font-medium text-gray-700 dark:text-gray-300">
                      Font Family
                    </Label>
                    <div className="grid grid-cols-3 gap-2">
                      {[
                        { value: "sans" as const, label: "Sans Serif", example: "Aa" },
                        { value: "display" as const, label: "Display", example: "Aa" },
                        { value: "roboto" as const, label: "Roboto", example: "Aa" },
                      ].map((font) => (
                        <button
                          key={font.value}
                          onClick={() => setData((prev) => ({ ...prev, fontFamily: font.value }))}
                          className={`p-3 rounded-lg border-2 transition-all flex flex-col items-center gap-1 ${
                            data.fontFamily === font.value
                              ? "border-green-500 bg-green-100 dark:bg-green-900/30 font-medium"
                              : "border-gray-200 dark:border-gray-700 hover:border-green-300 dark:hover:border-green-700"
                          }`}
                        >
                          <span className={`text-xl ${font.value === "sans" ? "font-sans" : font.value === "display" ? "font-bold tracking-tight" : "font-roboto"}`}>
                            {font.example}
                          </span>
                          <span className="text-xs text-gray-600 dark:text-gray-400">{font.label}</span>
                        </button>
                      ))}
                    </div>
                  </div>

                  {/* Font Size */}
                  <div className="space-y-2">
                    <Label className="text-sm font-medium text-gray-700 dark:text-gray-300">
                      Font Size (scales with button size)
                    </Label>
                    <div className="grid grid-cols-3 gap-2">
                      {[
                        { value: "sm" as const, label: "Small" },
                        { value: "md" as const, label: "Medium" },
                        { value: "lg" as const, label: "Large" },
                      ].map((size) => (
                        <button
                          key={size.value}
                          onClick={() => setData((prev) => ({ ...prev, fontSize: size.value }))}
                          className={`p-2 rounded-lg border-2 transition-all text-sm ${
                            data.fontSize === size.value
                              ? "border-green-500 bg-green-100 dark:bg-green-900/30 font-medium"
                              : "border-gray-200 dark:border-gray-700 hover:border-green-300 dark:hover:border-green-700"
                          }`}
                        >
                          {size.label}
                        </button>
                      ))}
                    </div>
                  </div>
                </div>

                {/* Color Palette */}
                <div className="space-y-4 p-4 bg-purple-50 dark:bg-purple-950/20 rounded-lg border border-purple-200 dark:border-purple-800">
                  <h4 className="text-sm font-semibold text-purple-900 dark:text-purple-100">Color Palette</h4>
                  
                  <div className="space-y-2">
                    <Label className="text-sm font-medium text-gray-700 dark:text-gray-300">
                      Choose Color Theme
                    </Label>
                    <div className="grid grid-cols-2 gap-3">
                      {colorPalettes.map((palette) => (
                        <button
                          key={palette.value}
                          onClick={() => setData((prev) => ({ 
                            ...prev, 
                            colorPalette: palette.value,
                            customColors: palette.value === "custom" ? {
                              primary: prev.customColors?.primary || "#3b82f6",
                              secondary: prev.customColors?.secondary || "#8b5cf6",
                              text: prev.customColors?.text || "#ffffff",
                              hoverPrimary: prev.customColors?.hoverPrimary || "#1d4ed8",
                              hoverSecondary: prev.customColors?.hoverSecondary || "#7c3aed",
                              hoverText: prev.customColors?.hoverText || "#ffffff",
                            } : prev.customColors
                          }))}
                          className={`p-3 rounded-lg border-2 transition-all text-left ${
                            data.colorPalette === palette.value
                              ? "border-purple-500 bg-purple-100 dark:bg-purple-900/30"
                              : "border-gray-200 dark:border-gray-700 hover:border-purple-300 dark:hover:border-purple-700"
                          }`}
                        >
                          <div className="flex items-center gap-2 mb-1">
                            <div className="flex gap-1">
                              <div 
                                className="w-4 h-4 rounded"
                                style={{ backgroundColor: palette.colors.primary }}
                              />
                              <div 
                                className="w-4 h-4 rounded"
                                style={{ backgroundColor: palette.colors.secondary }}
                              />
                            </div>
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
                  </div>

                  {/* Custom Colors */}
                  {data.colorPalette === "custom" && (
                    <div className="space-y-4 pt-3 border-t border-purple-200 dark:border-purple-800">
                      <div className="space-y-3">
                        <Label className="text-sm font-medium text-gray-700 dark:text-gray-300">
                          Normal State Colors
                        </Label>
                        <div className="grid grid-cols-3 gap-3">
                          <div className="space-y-1">
                            <Label htmlFor="primary-color" className="text-xs text-gray-600 dark:text-gray-400">
                              Primary
                            </Label>
                            <div className="flex items-center gap-2">
                              <Input
                                id="primary-color"
                                type="color"
                                value={data.customColors?.primary || "#3b82f6"}
                                onChange={(e) => setData((prev) => ({
                                  ...prev,
                                  customColors: {
                                    ...prev.customColors!,
                                    primary: e.target.value,
                                  }
                                }))}
                                className="h-10 w-full cursor-pointer"
                              />
                            </div>
                          </div>
                          <div className="space-y-1">
                            <Label htmlFor="secondary-color" className="text-xs text-gray-600 dark:text-gray-400">
                              Secondary
                            </Label>
                            <div className="flex items-center gap-2">
                              <Input
                                id="secondary-color"
                                type="color"
                                value={data.customColors?.secondary || "#8b5cf6"}
                                onChange={(e) => setData((prev) => ({
                                  ...prev,
                                  customColors: {
                                    ...prev.customColors!,
                                    secondary: e.target.value,
                                  }
                                }))}
                                className="h-10 w-full cursor-pointer"
                              />
                            </div>
                          </div>
                          <div className="space-y-1">
                            <Label htmlFor="text-color" className="text-xs text-gray-600 dark:text-gray-400">
                              Text
                            </Label>
                            <div className="flex items-center gap-2">
                              <Input
                                id="text-color"
                                type="color"
                                value={data.customColors?.text || "#ffffff"}
                                onChange={(e) => setData((prev) => ({
                                  ...prev,
                                  customColors: {
                                    ...prev.customColors!,
                                    text: e.target.value,
                                  }
                                }))}
                                className="h-10 w-full cursor-pointer"
                              />
                            </div>
                          </div>
                        </div>
                      </div>

                      <div className="space-y-3">
                        <Label className="text-sm font-medium text-gray-700 dark:text-gray-300">
                          Hover State Colors
                        </Label>
                        <div className="grid grid-cols-3 gap-3">
                          <div className="space-y-1">
                            <Label htmlFor="hover-primary-color" className="text-xs text-gray-600 dark:text-gray-400">
                              Primary
                            </Label>
                            <div className="flex items-center gap-2">
                              <Input
                                id="hover-primary-color"
                                type="color"
                                value={data.customColors?.hoverPrimary || "#1d4ed8"}
                                onChange={(e) => setData((prev) => ({
                                  ...prev,
                                  customColors: {
                                    ...prev.customColors!,
                                    hoverPrimary: e.target.value,
                                  }
                                }))}
                                className="h-10 w-full cursor-pointer"
                              />
                            </div>
                          </div>
                          <div className="space-y-1">
                            <Label htmlFor="hover-secondary-color" className="text-xs text-gray-600 dark:text-gray-400">
                              Secondary
                            </Label>
                            <div className="flex items-center gap-2">
                              <Input
                                id="hover-secondary-color"
                                type="color"
                                value={data.customColors?.hoverSecondary || "#7c3aed"}
                                onChange={(e) => setData((prev) => ({
                                  ...prev,
                                  customColors: {
                                    ...prev.customColors!,
                                    hoverSecondary: e.target.value,
                                  }
                                }))}
                                className="h-10 w-full cursor-pointer"
                              />
                            </div>
                          </div>
                          <div className="space-y-1">
                            <Label htmlFor="hover-text-color" className="text-xs text-gray-600 dark:text-gray-400">
                              Text
                            </Label>
                            <div className="flex items-center gap-2">
                              <Input
                                id="hover-text-color"
                                type="color"
                                value={data.customColors?.hoverText || "#ffffff"}
                                onChange={(e) => setData((prev) => ({
                                  ...prev,
                                  customColors: {
                                    ...prev.customColors!,
                                    hoverText: e.target.value,
                                  }
                                }))}
                                className="h-10 w-full cursor-pointer"
                              />
                            </div>
                          </div>
                        </div>
                      </div>
                    </div>
                  )}
                </div>

                {/* Info Section */}
                <div className="p-4 bg-blue-50 dark:bg-blue-950/30 border border-blue-200 dark:border-blue-800 rounded-lg">
                  <h4 className="text-sm font-medium text-blue-900 dark:text-blue-100 mb-2">
                    Action Behavior
                  </h4>
                  <p className="text-xs text-blue-700 dark:text-blue-300">
                    {data.actionType === "url" && "Opens the specified URL in a new tab"}
                    {data.actionType === "download" && "Downloads the file from the specified URL"}
                    {data.actionType === "copy" && "Copies the specified text to clipboard"}
                    {data.actionType === "email" && "Opens default email client with the specified address"}
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
              <style>{`
                .custom-button-hover:hover {
                  background: var(--hover-bg) !important;
                  color: var(--hover-text) !important;
                  border-color: var(--hover-border) !important;
                }
              `}</style>
              <div className="space-y-4 w-full max-w-md">
                <div className="text-center text-sm text-gray-600 dark:text-gray-400 mb-8">
                  Button appearance in your content
                </div>
                <div className="flex justify-center">
                  <button 
                    className={`${getButtonStyles()} ${data.colorPalette === "custom" ? "custom-button-hover" : ""}`}
                    style={getCustomStyle()}
                  >
                    {data.text}
                    {data.showIcon && (
                      <span className={getIconSpacingClass(data.iconPosition)}>
                        {getActionIcon()}
                      </span>
                    )}
                  </button>
                </div>
                
                {/* Visual indicators */}
                <div className="mt-8 p-4 bg-gray-50 dark:bg-gray-800/50 rounded-lg border border-gray-200 dark:border-gray-700">
                  <div className="space-y-2 text-xs">
                    <div className="flex justify-between">
                      <span className="text-gray-600 dark:text-gray-400">Text:</span>
                      <span className="font-medium text-gray-900 dark:text-gray-100">{data.text || "(empty)"}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-gray-600 dark:text-gray-400">Action:</span>
                      <span className="font-medium text-gray-900 dark:text-gray-100">{actionTypes.find((a) => a.value === data.actionType)?.label}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-gray-600 dark:text-gray-400">Style:</span>
                      <span className="font-medium text-gray-900 dark:text-gray-100">{variants.find((v) => v.value === data.variant)?.label}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-gray-600 dark:text-gray-400">Size:</span>
                      <span className="font-medium text-gray-900 dark:text-gray-100">{sizes.find((s) => s.value === data.size)?.label}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-gray-600 dark:text-gray-400">Icon:</span>
                      <span className="font-medium text-gray-900 dark:text-gray-100">
                        {data.showIcon ? `${iconPositions.find((p) => p.value === data.iconPosition)?.label} (${iconSizes.find((s) => s.value === data.iconSize)?.label})` : "Hidden"}
                      </span>
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
