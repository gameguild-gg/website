import type { SerializedBlockNode } from "./base/serialized-block-node"

export type GalleryLayout = "1" | "2" | "3" | "4"

export type ImageDisplayMode = "crop" | "adaptive"

export interface GalleryImage {
  id: string
  src: string
  alt: string
  caption?: string
  displayMode?: ImageDisplayMode
  span?: "1x1" | "1x2" | "2x1" | "2x2"
  aspectRatio?: number
  gridPosition?: { rowStart: number; colStart: number; rowSpan: number; colSpan: number }
}

export interface GalleryData {
  images: GalleryImage[]
  layout: GalleryLayout
  caption?: string
  captionStyle?: {
    fontSize?: "xs" | "sm" | "base" | "lg"
    fontFamily?: "sans" | "serif" | "mono"
    fontWeight?: "normal" | "medium" | "bold"
    fontStyle?: "normal" | "italic"
  }
  isNew?: boolean
  defaultDisplayMode?: ImageDisplayMode
}

export type SerializedGalleryNode = SerializedBlockNode<"gallery", GalleryData>
