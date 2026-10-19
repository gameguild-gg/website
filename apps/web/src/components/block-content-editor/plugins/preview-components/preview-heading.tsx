"use client"

import React from "react"
import type { SerializedHeadingNode } from "@lexical/rich-text"

interface PreviewHeadingNode {
  format?: SerializedHeadingNode["format"]
  tag?: SerializedHeadingNode["tag"] | 1 | 2 | 3 | 4 | 5 | 6
  style?: string
}

interface PreviewHeadingProps {
  node: PreviewHeadingNode
  children: React.ReactNode
  index?: number
}

export function PreviewHeading({ node, children, index = 0 }: PreviewHeadingProps) {
  const headingClasses = ["font-bold my-4"]
  if (node.format === "left") headingClasses.push("text-left")
  else if (node.format === "center") headingClasses.push("text-center")
  else if (node.format === "right") headingClasses.push("text-right")
  else if (node.format === "justify") headingClasses.push("text-justify")

  // Get inline styles from the node if they exist
  const headingStyles: React.CSSProperties & Record<string, string> = {}
  if (node.style) {
    const styleString = node.style
    const styleRules = styleString.split(";").filter((rule: string) => rule.trim())

    styleRules.forEach((rule) => {
      const [property, value] = rule.split(":").map((s) => s.trim())
      if (property && value) {
        const camelCaseProperty = property.replace(/-([a-z])/g, (match: string, letter: string) => letter.toUpperCase())
        headingStyles[camelCaseProperty] = value
      }
    })
  }

  const extractTextFromChildren = (children: React.ReactNode): string => {
    if (typeof children === "string") return children
    if (typeof children === "number") return children.toString()
    if (Array.isArray(children)) {
      return children.map(extractTextFromChildren).join("")
    }
    if (React.isValidElement(children)) {
      // Type guard to safely access props.children
      const element = children as React.ReactElement<{ children?: React.ReactNode }>
      if (element.props && "children" in element.props) {
        return extractTextFromChildren(element.props.children)
      }
    }
    return ""
  }

  const headingText = extractTextFromChildren(children)
  const headingId = `heading-${index}`

  console.log("[v0] PreviewHeading - Generated heading ID:", headingId)
  console.log("[v0] PreviewHeading - Index received:", index)
  console.log("[v0] PreviewHeading - Heading text:", headingText)

  const commonProps = {
    className: headingClasses.join(" "),
    style: headingStyles,
    "data-heading-id": headingId,
    children,
  }

  switch (node.tag) {
    case "h1":
    case 1:
      return (
        <h1 className={`text-4xl ${headingClasses.join(" ")}`} style={headingStyles} data-heading-id={headingId}>
          {children}
        </h1>
      )
    case "h2":
    case 2:
      return (
        <h2 className={`text-3xl ${headingClasses.join(" ")}`} style={headingStyles} data-heading-id={headingId}>
          {children}
        </h2>
      )
    case "h3":
    case 3:
      return (
        <h3 className={`text-2xl ${headingClasses.join(" ")}`} style={headingStyles} data-heading-id={headingId}>
          {children}
        </h3>
      )
    case "h4":
    case 4:
      return (
        <h4 className={`text-xl ${headingClasses.join(" ")}`} style={headingStyles} data-heading-id={headingId}>
          {children}
        </h4>
      )
    case "h5":
    case 5:
      return (
        <h5 className={`text-lg ${headingClasses.join(" ")}`} style={headingStyles} data-heading-id={headingId}>
          {children}
        </h5>
      )
    case "h6":
    case 6:
      return (
        <h6 className={`text-base ${headingClasses.join(" ")}`} style={headingStyles} data-heading-id={headingId}>
          {children}
        </h6>
      )
    default:
      return (
        <h2 className={`text-3xl ${headingClasses.join(" ")}`} style={headingStyles} data-heading-id={headingId}>
          {children}
        </h2>
      )
  }
}
