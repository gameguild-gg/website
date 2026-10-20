"use client"

import type React from "react"
import type { SerializedParagraphNode } from "lexical"

interface PreviewParagraphProps {
  node: SerializedParagraphNode
  children: React.ReactNode
}

export function PreviewParagraph({ node, children }: PreviewParagraphProps) {
  const paragraphClasses = ["my-4"]
  if (node.format === "left") paragraphClasses.push("text-left")
  else if (node.format === "center") paragraphClasses.push("text-center")
  else if (node.format === "right") paragraphClasses.push("text-right")
  else if (node.format === "justify") paragraphClasses.push("text-justify")

  // Check if paragraph contains block-level nodes (gallery, code-studio, etc.)
  // These cannot be nested inside <p> tags
  const hasBlockLevelChildren = node.children?.some((child) =>
    child.type === "gallery" || 
    child.type === "code-studio" ||
    child.type === "list" ||
    child.type === "custom-list"
  )

  // Use div for paragraphs with block-level children to avoid hydration errors
  if (hasBlockLevelChildren) {
    return <div className={paragraphClasses.join(" ")}>{children}</div>
  }

  return <p className={paragraphClasses.join(" ")}>{children}</p>
}
