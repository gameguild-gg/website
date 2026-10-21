"use client"

import type { SerializedDividerNode } from "../../nodes/divider-node"
import {
  getThicknessStyles,
  getSpacingStyles,
  getColorStyles,
  getStyleClasses,
  getPaletteColor,
} from "@/components/block-content-editor/extras/divider/divider-styles"

export function PreviewDivider({ node }: { node: SerializedDividerNode }) {
  if (!node?.data) {
    console.error("Invalid divider node structure:", node)
    return null
  }

  const { style, thickness, spacing, colorPalette, customColor } = node.data

  const spacingClass = getSpacingStyles(spacing)
  const thicknessClass = getThicknessStyles(thickness, style)
  const colorClass = getColorStyles(colorPalette, style)
  const styleClass = getStyleClasses(style)
  const paletteColor = getPaletteColor(colorPalette, customColor)

  const customStyle = colorPalette === "custom" && customColor ? {
    borderColor: customColor,
    backgroundColor: customColor,
  } : {}

  const renderDivider = () => {
    switch (style) {
      case "gradient":
        return (
          <div className={`${spacingClass} ${thicknessClass} ${colorClass}`} style={customStyle} aria-hidden="true" />
        )
      case "double": {
        // Duas linhas perpendiculares (paralelas horizontais)
        const doubleThickness = thickness === "thin" ? "1px" : thickness === "medium" ? "2px" : "3px"
        const doubleGap = thickness === "thin" ? "2px" : thickness === "medium" ? "3px" : "4px"
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
    <div className="my-4">
      {renderDivider()}
    </div>
  )
}
