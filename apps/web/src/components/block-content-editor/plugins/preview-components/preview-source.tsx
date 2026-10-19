"use client"

import type { SerializedSourceNode, SourceItem } from "../../nodes/source-node"
import { ExternalLink } from 'lucide-react'

export function PreviewSource({ node }: { node: SerializedSourceNode }) {
  if (!node?.data) {
    console.error("Invalid source node structure:", node)
    return null
  }

  const { sources, title, style } = node.data

  // Format a source according to the selected style
  const formatSource = (source: SourceItem): string => {
    switch (style) {
      case "apa":
        return formatAPA(source)
      case "mla":
        return formatMLA(source)
      case "chicago":
        return formatChicago(source)
      case "harvard":
        return formatHarvard(source)
      case "ieee":
        return formatIEEE(source)
      default:
        return formatAPA(source)
    }
  }

  // APA style: Author, A. A. (Year). Title of work. Publication. URL
  const formatAPA = (source: SourceItem): string => {
    let citation = ""

    // Author
    if (source.author) {
      citation += source.author
      if (!citation.endsWith(".")) {
        citation += "."
      }
      citation += " "
    }

    // Year
    if (source.year) {
      citation += `(${source.year}). `
    }

    // Title
    if (source.title) {
      citation += source.title
      if (!citation.endsWith(".")) {
        citation += "."
      }
      citation += " "
    }

    // Publication
    if (source.publication) {
      citation += source.publication
      if (!citation.endsWith(".")) {
        citation += "."
      }
      citation += " "
    }

    // URL
    if (source.url) {
      citation += source.url
    }

    return citation.trim()
  }

  // MLA style: Author. "Title." Publication, Year. URL
  const formatMLA = (source: SourceItem): string => {
    let citation = ""

    // Author
    if (source.author) {
      citation += source.author
      if (!citation.endsWith(".")) {
        citation += "."
      }
      citation += " "
    }

    // Title
    if (source.title) {
      citation += `"${source.title}"`
      if (!citation.endsWith(".")) {
        citation += "."
      }
      citation += " "
    }

    // Publication
    if (source.publication) {
      citation += source.publication
      if (source.year) {
        citation += `, ${source.year}`
      }
      if (!citation.endsWith(".")) {
        citation += "."
      }
      citation += " "
    } else if (source.year) {
      citation += source.year
      if (!citation.endsWith(".")) {
        citation += "."
      }
      citation += " "
    }

    // URL
    if (source.url) {
      citation += source.url
    }

    return citation.trim()
  }

  // Chicago style: Author. Year. "Title." Publication. URL
  const formatChicago = (source: SourceItem): string => {
    let citation = ""

    // Author
    if (source.author) {
      citation += source.author
      if (!citation.endsWith(".")) {
        citation += "."
      }
      citation += " "
    }

    // Year
    if (source.year) {
      citation += source.year
      if (!citation.endsWith(".")) {
        citation += "."
      }
      citation += " "
    }

    // Title
    if (source.title) {
      citation += `"${source.title}"`
      if (!citation.endsWith(".")) {
        citation += "."
      }
      citation += " "
    }

    // Publication
    if (source.publication) {
      citation += source.publication
      if (!citation.endsWith(".")) {
        citation += "."
      }
      citation += " "
    }

    // URL
    if (source.url) {
      citation += source.url
    }

    return citation.trim()
  }

  // Harvard style: Author (Year) Title, Publication. URL
  const formatHarvard = (source: SourceItem): string => {
    let citation = ""

    // Author
    if (source.author) {
      citation += source.author
      citation += " "
    }

    // Year
    if (source.year) {
      citation += `(${source.year}) `
    }

    // Title
    if (source.title) {
      citation += source.title
      if (!citation.endsWith(".")) {
        citation += ","
      }
      citation += " "
    }

    // Publication
    if (source.publication) {
      citation += source.publication
      if (!citation.endsWith(".")) {
        citation += "."
      }
      citation += " "
    }

    // URL
    if (source.url) {
      citation += source.url
    }

    return citation.trim()
  }

  // IEEE style: [1] A. Author, "Title," Publication, Year. URL
  const formatIEEE = (source: SourceItem): string => {
    let citation = ""

    // Author
    if (source.author) {
      citation += source.author
      if (!citation.endsWith(",")) {
        citation += ","
      }
      citation += " "
    }

    // Title
    if (source.title) {
      citation += `"${source.title}"`
      if (!citation.endsWith(",")) {
        citation += ","
      }
      citation += " "
    }

    // Publication
    if (source.publication) {
      citation += source.publication
      if (!citation.endsWith(",")) {
        citation += ","
      }
      citation += " "
    }

    // Year
    if (source.year) {
      citation += source.year
      if (!citation.endsWith(".")) {
        citation += "."
      }
      citation += " "
    }

    // URL
    if (source.url) {
      citation += source.url
    }

    return citation.trim()
  }

  return (
    <div className="my-8 rounded-lg p-4">
      <h3 className="text-xl font-bold mb-4">{title}</h3>
      {sources.length > 0 ? (
        <ol className="list-decimal list-inside space-y-2">
          {sources.map((source: SourceItem, index: number) => (
            <li key={source.id} className="pl-2">
              <div className="inline">{formatSource(source)}</div>
              {source.url && (
                <a
                  href={source.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="ml-2 inline-flex items-center text-primary hover:underline"
                >
                  <ExternalLink className="h-3 w-3 ml-1" />
                </a>
              )}
            </li>
          ))}
        </ol>
      ) : (
        <p className="text-muted-foreground">No sources added yet.</p>
      )}
    </div>
  )
}
