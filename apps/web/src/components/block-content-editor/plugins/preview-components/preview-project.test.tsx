import { cleanup, render, screen } from "@testing-library/react"
import type { SerializedEditorState } from "lexical"
import { afterEach, describe, expect, it, vi } from "vitest"

import type { ProjectData } from "../../lib/storage/editor/project-data"
import { PreviewProject } from "./preview-project"

vi.mock("../../extras/preview/serialized-content-renderer", () => ({
  SerializedContentRenderer: ({ serializedState }: { serializedState: SerializedEditorState }) => (
    <div data-testid="referenced-project-content">{serializedState.root.children.length}</div>
  ),
}))

afterEach(cleanup)

describe("PreviewProject storage metadata", () => {
  it("shows the stored metadata size when loading a referenced project", async () => {
    const editorState: SerializedEditorState = {
      root: {
        type: "root",
        version: 1,
        children: [],
        direction: null,
        format: "",
        indent: 0,
      },
    }
    const storedProject: ProjectData = {
      id: "referenced-project",
      name: "Referenced project",
      data: JSON.stringify(editorState),
      tags: [],
      storageType: "local",
      metadata: {
        size: 1536,
        hash: "stored-content-hash",
        createdAt: "2026-10-20T00:00:00.000Z",
        updatedAt: "2026-10-20T00:00:00.000Z",
      },
    }
    const load = vi.fn<(id: string) => Promise<ProjectData | null>>().mockResolvedValue(storedProject)

    render(
      <PreviewProject
        node={{
          data: {
            projectId: storedProject.id,
            projectName: storedProject.name,
            editorState: null,
            isLocalCopy: false,
            isReference: true,
          },
        }}
        storageAdapter={{ load }}
      />,
    )

    expect(await screen.findByText("1.5MB")).toBeVisible()
    expect(load).toHaveBeenCalledWith("referenced-project")
    expect(screen.getByTestId("referenced-project-content")).toHaveTextContent("0")
  })
})
