"use client"
import type { SerializedEditorState } from "lexical"
import type { ProjectData } from "../../lib/storage/editor/project-data"

// Import all preview components
import { PreviewQuiz } from "@/components/block-content-editor/plugins/preview-components/preview-quiz"
import { PreviewImage } from "@/components/block-content-editor/plugins/preview-components/preview-image"
import { PreviewGallery } from "@/components/block-content-editor/plugins/preview-components/preview-gallery"
import { PreviewMarkdown } from "@/components/block-content-editor/plugins/preview-components/preview-markdown"
import { PreviewHTML } from "@/components/block-content-editor/plugins/preview-components/preview-html"
import { PreviewVideo } from "@/components/block-content-editor/plugins/preview-components/preview-video"
import { PreviewAudio } from "@/components/block-content-editor/plugins/preview-components/preview-audio"
import { PreviewHeader } from "@/components/block-content-editor/plugins/preview-components/preview-header"
import { PreviewDivider } from "@/components/block-content-editor/plugins/preview-components/preview-divider"
import { PreviewSource } from "@/components/block-content-editor/plugins/preview-components/preview-source"
import { PreviewAdmonition } from "@/components/block-content-editor/plugins/preview-components/preview-admonition"
import { PreviewButton } from "@/components/block-content-editor/plugins/preview-components/preview-button"
import { PreviewText } from "@/components/block-content-editor/plugins/preview-components/preview-text"
import { PreviewMermaid } from "@/components/block-content-editor/plugins/preview-components/preview-mermaid"
import { PreviewParagraph } from "@/components/block-content-editor/plugins/preview-components/preview-paragraph"
import { PreviewQuote } from "@/components/block-content-editor/plugins/preview-components/preview-quote"
import { PreviewList } from "@/components/block-content-editor/plugins/preview-components/preview-list"
import { PreviewListItem } from "@/components/block-content-editor/plugins/preview-components/preview-list-item"
import { PreviewLink } from "@/components/block-content-editor/plugins/preview-components/preview-link"
import { PreviewHeading } from "@/components/block-content-editor/plugins/preview-components/preview-heading"
import { PreviewVegaLite } from "@/components/block-content-editor/plugins/preview-components/preview-vega-lite"
import { PreviewCodeStudio } from "@/components/block-content-editor/plugins/preview-components/preview-code-studio"
import { PreviewProject } from "@/components/block-content-editor/plugins/preview-components/preview-project"
import { PreviewRichText } from "@/components/block-content-editor/plugins/preview-components/preview-rich-text"

interface SerializedContentRendererProps {
  serializedState: SerializedEditorState
  className?: string
  projectId?: string
  storageAdapter?: {
    load: (id: string) => Promise<ProjectData | null>
  }
}

export function SerializedContentRenderer({
  serializedState,
  className = "prose prose-stone dark:prose-invert max-w-none",
  projectId,
  storageAdapter,
}: SerializedContentRendererProps) {
  let headingCounter = 0

  // Validate serializedState structure
  if (!serializedState || !serializedState.root || !serializedState.root.children) {
    console.error("Invalid serializedState:", serializedState)
    return (
      <div className="p-8 text-center border border-red-200 bg-red-50 dark:border-red-700 dark:bg-red-900/20">
        <p className="text-red-600 dark:text-red-400 font-medium">
          Unable to render content: Invalid data structure
        </p>
        <p className="text-sm text-red-500 dark:text-red-500 mt-2">
          The editor state is missing required properties (root.children)
        </p>
      </div>
    )
  }

  const renderNode = (node: any, index = 0, parentPath = "") => {
    // Create unique key using path and index
    const uniqueKey = `${parentPath}-${node.type}-${index}-${node.version || 0}`

    // Handle quiz nodes
    if (node.type === "quiz") {
      return <PreviewQuiz key={uniqueKey} node={node} />
    }

    // Handle image nodes
    if (node.type === "image") {
      return <PreviewImage key={uniqueKey} node={node} />
    }

    // Handle gallery nodes
    if (node.type === "gallery") {
      return <PreviewGallery key={uniqueKey} node={node} />
    }

    // Handle markdown nodes
    if (node.type === "markdown") {
      return <PreviewMarkdown key={uniqueKey} node={node} />
    }

    // Handle HTML nodes
    if (node.type === "html") {
      return <PreviewHTML key={uniqueKey} node={node} />
    }

    // Handle Rich Text nodes
    if (node.type === "rich-text") {
      return <PreviewRichText key={uniqueKey} node={node} />
    }

    // Handle video nodes
    if (node.type === "video") {
      return <PreviewVideo key={uniqueKey} node={node} />
    }

    // Handle audio nodes
    if (node.type === "audio") {
      return <PreviewAudio key={uniqueKey} node={node} />
    }

    // Handle header nodes
    if (node.type === "header") {
      return <PreviewHeader key={uniqueKey} node={node} />
    }

    // Handle divider nodes
    if (node.type === "divider") {
      return <PreviewDivider key={uniqueKey} node={node} />
    }

    // Handle button nodes
    if (node.type === "button") {
      return <PreviewButton key={uniqueKey} node={node} />
    }

    // Handle admonition nodes
    if (node.type === "admonition") {
      return <PreviewAdmonition key={uniqueKey} node={node} />
    }

    // Handle source nodes
    if (node.type === "source") {
      return <PreviewSource key={uniqueKey} node={node} />
    }

    // For text content - now using the new component
    if (node.type === "text") {
      return <PreviewText key={uniqueKey} node={node} />
    }

    // For Mermaid diagrams
    if (node.type === "mermaid") {
      return <PreviewMermaid key={uniqueKey} data={node.data} />
    }

    // For Vega-Lite charts
    if (node.type === "vega-lite") {
      return <PreviewVegaLite key={uniqueKey} node={node} />
    }

    // For CodeStudio nodes
    if (node.type === "code-studio") {
      return <PreviewCodeStudio key={uniqueKey} data={node.data} projectId={projectId} />
    }

    // For Project nodes
    if (node.type === "project") {
      return <PreviewProject key={uniqueKey} node={node} storageAdapter={storageAdapter} />
    }

    if (node.children) {
      const children = node.children.map((child: any, childIndex: number) => renderNode(child, childIndex, uniqueKey))

      switch (node.type) {
        case "paragraph":
          return (
            <PreviewParagraph key={uniqueKey} node={node}>
              {children}
            </PreviewParagraph>
          )
        case "quote":
          return (
            <PreviewQuote key={uniqueKey} node={node}>
              {children}
            </PreviewQuote>
          )
        case "list":
        case "custom-list":
          return (
            <PreviewList key={uniqueKey} node={node}>
              {children}
            </PreviewList>
          )
        case "listitem":
          return (
            <PreviewListItem key={uniqueKey} node={node}>
              {children}
            </PreviewListItem>
          )
        case "link":
          return (
            <PreviewLink key={uniqueKey} node={node}>
              {children}
            </PreviewLink>
          )
        case "heading": {
          const currentHeadingIndex = headingCounter++
          console.log("[v0] Rendering heading with index:", currentHeadingIndex)
          return (
            <PreviewHeading key={uniqueKey} node={node} index={currentHeadingIndex}>
              {children}
            </PreviewHeading>
          )
        }
        default:
          return <div key={uniqueKey}>{children}</div>
      }
    }

    return null
  }

  return (
    <div className={className}>
      {serializedState.root.children.map((node: any, index: number) => renderNode(node, index, "root"))}
    </div>
  )
}
