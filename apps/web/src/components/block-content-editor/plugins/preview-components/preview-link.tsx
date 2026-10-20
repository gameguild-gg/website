"use client"

import type React from "react"
import type { SerializedLinkNode } from "@lexical/link"

interface PreviewLinkProps {
  node: SerializedLinkNode
  children: React.ReactNode
}

export function PreviewLink({ node, children }: PreviewLinkProps) {
  return (
    <a href={node.url} className="text-primary hover:underline" target="_blank" rel="noopener noreferrer">
      {children}
    </a>
  )
}
