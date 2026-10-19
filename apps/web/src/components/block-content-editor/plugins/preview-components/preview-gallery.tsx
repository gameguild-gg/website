"use client"

import { ImageIcon } from 'lucide-react'
import type { SerializedGalleryNode } from "../../nodes/gallery-node"
import { AssetImage } from "../../extras/media/asset-image"

export function PreviewGallery({ node }: { node: SerializedGalleryNode }) {
  if (!node?.data) {
    console.error("Invalid gallery node structure:", node)
    return null
  }

  const { layout, caption, images } = node.data
  const captionStyle = node.data.captionStyle

  // Get caption style classes
  const getCaptionStyleClasses = () => {
    const classes = ["text-center text-muted-foreground"]

    // Font size
    if (captionStyle?.fontSize === "xs") classes.push("text-xs")
    else if (captionStyle?.fontSize === "sm") classes.push("text-sm")
    else if (captionStyle?.fontSize === "base") classes.push("text-base")
    else if (captionStyle?.fontSize === "lg") classes.push("text-lg")
    else classes.push("text-sm") // Default

    // Font family
    if (captionStyle?.fontFamily === "serif") classes.push("font-serif")
    else if (captionStyle?.fontFamily === "mono") classes.push("font-mono")
    else classes.push("font-sans") // Default

    // Font weight
    if (captionStyle?.fontWeight === "medium") classes.push("font-medium")
    else if (captionStyle?.fontWeight === "normal") classes.push("font-normal")
    else if (captionStyle?.fontWeight === "bold") classes.push("font-bold")
    else classes.push("font-normal") // Default

    return classes.join(" ")
  }

  if (images.length === 0) {
    return (
      <div className="my-4 flex items-center justify-center h-40 bg-muted/20 rounded-lg border border-dashed">
        <div className="text-center">
          <ImageIcon className="h-8 w-8 mx-auto text-muted-foreground" />
          <p className="mt-2 text-sm text-muted-foreground">No images in gallery</p>
        </div>
      </div>
    )
  }

  // Get grid template columns based on layout
  const getGridTemplateColumns = () => {
    return `repeat(${layout}, 1fr)`
  }

  return (
    <div className="my-4">
      <div
        className="grid gap-2"
        style={{
          gridTemplateColumns: getGridTemplateColumns(),
          gridAutoRows: "auto",
        }}
      >
        {images.map((image) => {
          // Check if this is a placeholder (empty src means it's a placeholder)
          const isPlaceholder = !image.src || image.src.trim() === ''

          // Calculate grid area style
          const gridAreaStyle = {
            gridRow: image.span === "2x1" || image.span === "2x2" ? "span 2" : undefined,
            gridColumn: image.span === "1x2" || image.span === "2x2" ? "span 2" : undefined,
          }

          return (
            <div key={image.id} className="space-y-1" style={gridAreaStyle}>
              <div
                className="relative overflow-hidden rounded-md"
                style={{ aspectRatio: image.displayMode === "crop" ? "1/1" : "16/9" }}
              >
                {isPlaceholder || !image.src ? (
                  // Render empty space for placeholders - maintains aspect ratio from parent
                  <div className="h-full w-full" />
                ) : (
                  // Render actual image
                  <AssetImage
                    src={image.src}
                    alt={image.alt}
                    className={
                      image.displayMode === "crop" ? "h-full w-full object-cover" : "h-auto w-full object-contain"
                    }
                  />
                )}
              </div>
              {image.caption && <div className={getCaptionStyleClasses()}>{image.caption}</div>}
            </div>
          )
        })}
      </div>
      {caption && <div className={`mt-2 ${getCaptionStyleClasses()}`}>{caption}</div>}
    </div>
  )
}
