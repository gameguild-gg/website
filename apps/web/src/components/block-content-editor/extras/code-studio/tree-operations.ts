import type { LayoutNode, LeafPanel, SplitNode, DisplayConfig, PanelType, EditorInstance } from "./types"

/**
 * Immutable-ish helpers for navigating and mutating a splitter tree.
 * Designed to be called from inside `useImmer` setLocalData drafts.
 */

let panelIdCounter = 0
function nextPanelId(type: PanelType): string {
  panelIdCounter += 1
  return `${type}-${Date.now()}-${panelIdCounter}`
}

let splitIdCounter = 0
function nextSplitId(): string {
  splitIdCounter += 1
  return `split-${Date.now()}-${splitIdCounter}`
}

// ---------------------------------------------------------------------------
// Traversal

export function isLeaf(node: LayoutNode): node is LeafPanel {
  return node.kind === "leaf"
}

export function isSplit(node: LayoutNode): node is SplitNode {
  return node.kind === "split"
}

/**
 * Flatten the tree to all leaf panels (left-to-right / top-to-bottom).
 */
export function getAllLeaves(node: LayoutNode): LeafPanel[] {
  if (isLeaf(node)) return [node]
  const out: LeafPanel[] = []
  for (const child of node.children) {
    out.push(...getAllLeaves(child))
  }
  return out
}

/**
 * Walk the tree looking for a leaf with the given id.
 */
export function findLeaf(node: LayoutNode, leafId: string): LeafPanel | undefined {
  if (isLeaf(node)) {
    return node.id === leafId ? node : undefined
  }
  for (const child of node.children) {
    const hit = findLeaf(child, leafId)
    if (hit) return hit
  }
  return undefined
}

/**
 * Locate the path from root to the target node id (leaf or split).
 * Returns the chain of indices to walk into `children` arrays.
 */
function findPath(node: LayoutNode, targetId: string, acc: number[] = []): number[] | undefined {
  if (node.id === targetId || (isLeaf(node) && node.id === targetId)) {
    return acc
  }
  if (isSplit(node)) {
    for (let i = 0; i < node.children.length; i += 1) {
      const child = node.children[i]
      if (!child) continue
      const sub = findPath(child, targetId, [...acc, i])
      if (sub) return sub
    }
  }
  return undefined
}

// ---------------------------------------------------------------------------
// Mutation helpers (work on Immer drafts — direct field assignment is fine)

/**
 * Apply new percent sizes to the split identified by `splitId`. No-op if not found.
 */
export function setSplitSizes(root: LayoutNode, splitId: string, sizes: number[]): void {
  if (isSplit(root) && root.id === splitId) {
    if (root.children.length === sizes.length) {
      root.sizes = sizes.map(s => Math.max(0, s))
    }
    return
  }
  if (isSplit(root)) {
    for (const child of root.children) {
      setSplitSizes(child, splitId, sizes)
    }
  }
}

/**
 * Remove the leaf with id `leafId` from the tree. If its parent split is left
 * with a single child, the split collapses (the parent is replaced by that
 * child in the grandparent). If the leaf is the root, returns a fallback
 * empty placeholder (no-op leaves should be avoided by callers).
 *
 * Returns the new root reference (callers should assign it back).
 */
export function removeLeaf(root: LayoutNode, leafId: string): LayoutNode | null {
  if (isLeaf(root)) {
    return root.id === leafId ? null : root
  }

  const newChildren: LayoutNode[] = []
  const keptSizes: number[] = []
  for (let i = 0; i < root.children.length; i += 1) {
    const child = root.children[i]
    if (!child) continue
    if (isLeaf(child)) {
      if (child.id === leafId) continue
      newChildren.push(child)
      keptSizes.push(root.sizes[i] ?? 0)
    } else {
      const updated = removeLeaf(child, leafId)
      if (updated == null) continue
      newChildren.push(updated)
      keptSizes.push(root.sizes[i] ?? 0)
    }
  }

  if (newChildren.length === 0) return null
  if (newChildren.length === 1) return newChildren[0]

  // Re-normalize sizes to sum to 100.
  const total = keptSizes.reduce((acc, s) => acc + s, 0)
  const normalized = total > 0
    ? keptSizes.map(s => (s / total) * 100)
    : keptSizes.map(() => 100 / keptSizes.length)

  return {
    kind: "split",
    id: root.id,
    direction: root.direction,
    sizes: normalized,
    children: newChildren,
  }
}

/**
 * Add a fresh panel by splitting the target leaf in two. The new leaf takes
 * `newSize` percent of the existing leaf's space.
 *
 * Returns the new root reference.
 */
export function splitLeafWithPanel(
  root: LayoutNode,
  targetLeafId: string,
  newPanelType: PanelType,
  direction: "horizontal" | "vertical",
  position: "before" | "after" = "after",
  newSize = 30,
): LayoutNode {
  const newLeaf: LeafPanel =
    newPanelType === "full-editor" || newPanelType === "focus-editor"
      ? { kind: "leaf", id: nextPanelId(newPanelType), type: newPanelType, editorInstance: "multiple" }
      : { kind: "leaf", id: nextPanelId(newPanelType), type: newPanelType }

  function visit(node: LayoutNode): LayoutNode {
    if (isLeaf(node)) {
      if (node.id !== targetLeafId) return node
      const children = position === "after" ? [node, newLeaf] : [newLeaf, node]
      const sizes = position === "after" ? [100 - newSize, newSize] : [newSize, 100 - newSize]
      return {
        kind: "split",
        id: nextSplitId(),
        direction,
        sizes,
        children,
      }
    }
    return { ...node, children: node.children.map(visit) }
  }

  return visit(root)
}

/**
 * Add a panel by splitting the entire root. Used when the user just wants to
 * append a panel without picking a specific leaf to host the split.
 */
export function addPanelAtRoot(
  root: LayoutNode,
  panelType: PanelType,
  direction: "horizontal" | "vertical" = "horizontal",
  newSize = 30,
): LayoutNode {
  const newLeaf: LeafPanel =
    panelType === "full-editor" || panelType === "focus-editor"
      ? { kind: "leaf", id: nextPanelId(panelType), type: panelType, editorInstance: "multiple" }
      : { kind: "leaf", id: nextPanelId(panelType), type: panelType }

  return {
    kind: "split",
    id: nextSplitId(),
    direction,
    sizes: [100 - newSize, newSize],
    children: [root, newLeaf],
  }
}

/**
 * Toggle a leaf's editor-instance flag between "multiple" and "unique".
 * Only meaningful for "full-editor" / "focus-editor" leaves.
 */
export function toggleLeafEditorInstance(root: LayoutNode, leafId: string): void {
  const leaf = findLeaf(root, leafId)
  if (!leaf) return
  if (leaf.type !== "full-editor" && leaf.type !== "focus-editor") return
  const next: EditorInstance = leaf.editorInstance === "unique" ? "multiple" : "unique"
  leaf.editorInstance = next
}

/**
 * Re-dock an existing leaf next to a target leaf.
 *
 * Used by the drag-and-drop quadrant docking: the user grabs a leaf and drops
 * it on one of the four edges of another leaf. The source leaf is first
 * removed (parents collapse when single-child), then re-inserted as a fresh
 * split sibling of the target.
 *
 * Returns the new root reference. If `sourceLeafId === targetLeafId` or either
 * id is missing, the tree is returned unchanged.
 */
export function moveLeafRelativeTo(
  root: LayoutNode,
  sourceLeafId: string,
  targetLeafId: string,
  position: "top" | "right" | "bottom" | "left",
  newSize = 30,
): LayoutNode {
  if (sourceLeafId === targetLeafId) return root
  const source = findLeaf(root, sourceLeafId)
  if (!source) return root
  // Snapshot the source leaf so we can re-insert it after removal collapses
  // any parents.
  const sourceSnapshot: LeafPanel = {
    kind: "leaf",
    id: source.id,
    type: source.type,
    ...(source.editorInstance ? { editorInstance: source.editorInstance } : {}),
  }

  const removed = removeLeaf(root, sourceLeafId)
  if (removed == null) return root

  // Target may have been collapsed during removal; re-find on the new tree.
  const target = findLeaf(removed, targetLeafId)
  if (!target) return root

  const direction: "horizontal" | "vertical" =
    position === "left" || position === "right" ? "horizontal" : "vertical"
  const placeAfter = position === "right" || position === "bottom"

  function visit(node: LayoutNode): LayoutNode {
    if (isLeaf(node)) {
      if (node.id !== targetLeafId) return node
      const children = placeAfter ? [node, sourceSnapshot] : [sourceSnapshot, node]
      const sizes = placeAfter ? [100 - newSize, newSize] : [newSize, 100 - newSize]
      return {
        kind: "split",
        id: nextSplitId(),
        direction,
        sizes,
        children,
      }
    }
    return { ...node, children: node.children.map(visit) }
  }

  return visit(removed)
}

// ---------------------------------------------------------------------------
// Display-level helpers

export function displayHasPanelType(display: DisplayConfig, type: PanelType): boolean {
  return getAllLeaves(display.root).some(p => p.type === type)
}

export function displayHasUniqueEditor(display: DisplayConfig): boolean {
  return getAllLeaves(display.root).some(
    p => (p.type === "full-editor" || p.type === "focus-editor") && p.editorInstance === "unique",
  )
}

export function findUniqueEditorLeaf(display: DisplayConfig): LeafPanel | undefined {
  return getAllLeaves(display.root).find(
    p => (p.type === "full-editor" || p.type === "focus-editor") && p.editorInstance === "unique",
  )
}
