"use client"

import type { SerializedButtonNode } from "../../nodes/button-node"
import { Copy, Download, ExternalLink, Mail,
  Link, Link2,
  ArrowDownToLine, FileDown,
  ClipboardCopy, CopyCheck,
  AtSign, Send
} from "lucide-react"
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

export function PreviewButton({ node }: { node: SerializedButtonNode }) {
  if (!node?.data) {
    console.error("Invalid button node structure:", node)
    return null
  }

  const { text, url, actionType, variant, size, showIcon, iconVariant, iconPosition, iconSize, colorPalette, customColors, fontFamily, fontSize } = node.data

  const getActionIcon = () => {
    const iconSizeClass = getIconSizeClass(size, iconSize || "md")

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

    return iconsByType[actionType][iconVariant || 0] || iconsByType[actionType][0]
  }

  const getButtonStyles = () => {
    const isVerticalIcon = showIcon && (iconPosition === "top" || iconPosition === "bottom")
    const palette = colorPalette || "blue"
    const font = fontFamily || "sans"
    const textSize = fontSize || "md"
    return `${BASE_BUTTON_STYLES} cursor-pointer ${getSizeStyles(size, isVerticalIcon)} ${getVariantBaseStyles(variant, size)} ${getColorStyles(palette, variant)} ${getLayoutStyles(iconPosition || "right")} ${getFontFamilyClass(font)} ${getFontSizeClass(size, textSize)}`
  }

  const getCustomStyle = () => {
    if (colorPalette === "custom" && customColors) {
      const { primary, secondary, text, hoverPrimary, hoverSecondary, hoverText } = customColors
      if (variant === "solid") {
        return {
          background: `linear-gradient(to right, ${primary}, ${secondary})`,
          color: text,
          "--hover-bg": `linear-gradient(to right, ${hoverPrimary}, ${hoverSecondary})`,
          "--hover-text": hoverText,
        } as React.CSSProperties
      } else if (variant === "outline") {
        return {
          borderColor: primary,
          color: text,
          "--hover-bg": hoverPrimary,
          "--hover-border": hoverPrimary,
          "--hover-text": hoverText,
        } as React.CSSProperties
      } else if (variant === "soft") {
        return {
          backgroundColor: `${primary}20`,
          color: text,
          "--hover-bg": `${hoverPrimary}30`,
          "--hover-text": hoverText,
        } as React.CSSProperties
      } else if (variant === "minimal") {
        return {
          color: text,
          "--hover-border": hoverPrimary,
          "--hover-text": hoverText,
        } as React.CSSProperties
      }
    }
    return {}
  }

  const handleButtonAction = () => {
    switch (actionType) {
      case "url":
        window.open(url, "_blank")
        break
      case "download": {
        const link = document.createElement("a")
        link.href = url
        link.download = ""
        document.body.appendChild(link)
        link.click()
        document.body.removeChild(link)
        break
      }
      case "copy":
        navigator.clipboard.writeText(url)
        break
      case "email":
        window.location.href = `mailto:${url}`
        break
    }
  }

  const iconSpacingClass = getIconSpacingClass(iconPosition || "right")

  return (
    <div className="my-4 flex justify-center">
      <style>{`
        .custom-button-hover:hover {
          background: var(--hover-bg) !important;
          color: var(--hover-text) !important;
          border-color: var(--hover-border) !important;
        }
      `}</style>
      <button 
        className={`${getButtonStyles()} ${colorPalette === "custom" ? "custom-button-hover" : ""}`}
        style={getCustomStyle()}
        onClick={handleButtonAction}
      >
        {text}
        {showIcon && <span className={iconSpacingClass}>{getActionIcon()}</span>}
      </button>
    </div>
  )
}
