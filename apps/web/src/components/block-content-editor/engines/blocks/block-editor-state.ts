import type { Block } from "../../lib/storage/editor/block-structure"

export function getCurrentEditingBlock(block: Block | null, editedBlock: Block | null): Block | null {
  if (block && editedBlock && block.id === editedBlock.id && block.type === editedBlock.type) {
    return editedBlock
  }
  return block
}

export function isPresentationFormData(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
}
