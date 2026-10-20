import { $createListItemNode, ListItemNode, ListNode } from "@lexical/list"
import { $createTextNode, $getNodeByKey, $getRoot, createEditor, type LexicalEditor } from "lexical"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"

import { $createCustomListNode, $isCustomListNode, CustomListNode } from "./custom-list-node"

let editor: LexicalEditor | undefined
let root: HTMLDivElement | undefined
let existingStyles: Set<HTMLStyleElement>

beforeEach(() => {
  existingStyles = new Set(document.head.querySelectorAll("style"))
})

afterEach(() => {
  editor?.setRootElement(null)
  editor = undefined
  root?.remove()
  root = undefined
  for (const style of document.head.querySelectorAll("style")) {
    if (!existingStyles.has(style)) style.remove()
  }
  vi.useRealTimers()
})

describe("CustomListNode marker color", () => {
  for (const { listType, listStyleType, tagName } of [
    { listType: "number", listStyleType: "decimal", tagName: "OL" },
    { listType: "bullet", listStyleType: "disc", tagName: "UL" },
  ] as const) {
    it(`reconciles ${listType} markers without deferred editor access`, () => {
      vi.useFakeTimers()
      const activeEditor = createEditor({
        namespace: `custom-list-color-${listType}`,
        nodes: [ListNode, ListItemNode, CustomListNode],
        onError(error) {
          throw error
        },
      })
      editor = activeEditor
      root = document.createElement("div")
      root.className = "lexical-editor"
      document.body.append(root)
      activeEditor.setRootElement(root)

      let listKey = ""
      activeEditor.update(() => {
        const list = $createCustomListNode(listType, 1, listStyleType, "#111111")
        list.append($createListItemNode().append($createTextNode("List item")))
        $getRoot().append(list)
        listKey = list.getKey()
      }, { discrete: true })

      const listElement = activeEditor.getElementByKey(listKey)
      if (!listElement) throw new Error("The custom list was not mounted")
      expect(listElement.tagName).toBe(tagName)
      const initialInstance = listElement.getAttribute("data-list-instance")
      expect(document.getElementById(`instance-${initialInstance}`)?.textContent).toContain("#111111")

      activeEditor.update(() => {
        const list = $getNodeByKey(listKey)
        if (!$isCustomListNode(list)) throw new Error("The mounted custom list is missing")
        list.setMarkerColor("#f97316")
      }, { discrete: true })

      expect(activeEditor.getElementByKey(listKey)).toBe(listElement)
      expect(listElement.textContent).toBe("List item")
      const updatedInstance = listElement.getAttribute("data-list-instance")
      const markerStyle = document.getElementById(`instance-${updatedInstance}`)?.textContent
      expect(markerStyle).toContain("#f97316")
      expect(markerStyle).not.toContain("#111111")
      activeEditor.getEditorState().read(() => {
        const list = $getNodeByKey(listKey)
        if (!$isCustomListNode(list)) throw new Error("The updated custom list is missing")
        expect(list.getMarkerColor()).toBe("#f97316")
        expect(list.exportJSON().markerColor).toBe("#f97316")
      })

      activeEditor.setRootElement(null)
      expect(() => vi.runOnlyPendingTimers()).not.toThrow()
    })
  }
})
