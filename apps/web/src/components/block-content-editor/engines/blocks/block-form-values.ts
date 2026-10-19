import type { HeaderData } from "../../nodes/header-node"
import type { SourceData, SourceItem } from "../../nodes/source-node"

export const HEADER_LEVELS = [1, 2, 3, 4, 5, 6] as const satisfies readonly HeaderData["level"][]

const HEADER_STYLES = ["default", "underlined", "bordered", "gradient", "accent"] as const satisfies readonly HeaderData["style"][]
const SOURCE_STYLES = ["apa", "mla", "chicago", "harvard", "ieee", "vancouver", "ama", "turabian", "abnt"] as const satisfies readonly NonNullable<SourceData["style"]>[]

export type SourceTextField = keyof Pick<SourceItem, "author" | "title" | "url" | "year">

export function parseHeaderLevel(value: string): HeaderData["level"] | undefined {
  const level = Number(value)
  return HEADER_LEVELS.find((candidate) => candidate === level)
}

export function isHeaderStyle(value: string): value is HeaderData["style"] {
  return HEADER_STYLES.some((style) => style === value)
}

export function isSourceStyle(value: string): value is NonNullable<SourceData["style"]> {
  return SOURCE_STYLES.some((style) => style === value)
}
