"use client"

import type React from "react"
import type { SerializedTextNode } from "lexical"

interface PreviewTextProps {
  node: Pick<SerializedTextNode, "text"> & Partial<Pick<SerializedTextNode, "style" | "format" | "version">>
}

export function PreviewText({ node }: PreviewTextProps) {
  let textContent: React.ReactNode = node.text

  // Get inline styles from the node
  const inlineStyles: React.CSSProperties & Record<string, string> = {}
  if (node.style) {
    // Parse the style string and convert to React CSSProperties
    const styleString = node.style
    const styleRules = styleString.split(";").filter((rule: string) => rule.trim())

    styleRules.forEach((rule: string) => {
      const [property, value] = rule.split(":").map((s: string) => s.trim())
      if (property && value) {
        // Convert CSS property names to camelCase for React
        const camelCaseProperty = property.replace(/-([a-z])/g, (_match: string, letter: string) => letter.toUpperCase())
        ;(inlineStyles as Record<string, string>)[camelCaseProperty] = value
      }
    })
  }

  // Apply text formatting
  if (node.format) {
    if (node.format & 1) {
      // Bold
      textContent = (
        <strong key={`bold-${node.version || Math.random()}`} style={inlineStyles}>
          {textContent}
        </strong>
      )
    }
    if (node.format & 2) {
      // Italic
      textContent = (
        <em key={`italic-${node.version || Math.random()}`} style={inlineStyles}>
          {textContent}
        </em>
      )
    }
    if (node.format & 8) {
      // Underline (format flag 4)
      textContent = (
        <u key={`underline-${node.version || Math.random()}`} style={inlineStyles}>
          {textContent}
        </u>
      )
    }
    if (node.format & 4) {
      // Strikethrough (format flag 8)
      textContent = (
        <s key={`strikethrough-${node.version || Math.random()}`} style={inlineStyles}>
          {textContent}
        </s>
      )
    }
    if (node.format & 32) {
      // Subscript (format flag 32)
      textContent = (
        <sub key={`subscript-${node.version || Math.random()}`} style={inlineStyles}>
          {textContent}
        </sub>
      )
    }
    if (node.format & 64) {
      // Superscript (format flag 16)
      textContent = (
        <sup key={`superscript-${node.version || Math.random()}`} style={inlineStyles}>
          {textContent}
        </sup>
      )
    }
    if (node.format & 64) {
      // Code
      textContent = (
        <code
          key={`code-${node.version || Math.random()}`}
          style={inlineStyles}
          className="bg-muted px-1 py-0.5 rounded text-sm"
        >
          {textContent}
        </code>
      )
    }
    if (node.format & 128) {
      // Assuming format flag 128 for short quote
      textContent = (
        <q key={`quote-${node.version || Math.random()}`} style={inlineStyles}>
          {textContent}
        </q>
      )
    }
  } else if (Object.keys(inlineStyles).length > 0) {
    // Apply inline styles even if no formatting flags are set
    textContent = (
      <span key={`styled-${node.version || Math.random()}`} style={inlineStyles}>
        {textContent}
      </span>
    )
  }

  return <>{textContent}</>
}
