import "@testing-library/jest-dom/vitest"

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { FormProvider, useForm } from "react-hook-form"
import {
  createEssayEntry,
  safeParseQuizEntry,
  type EssayEntry,
} from "@game-guild/quiz"
import { EssayEditor } from "./editor"

vi.mock("./lexical-adapter", () => ({
  EssayLexicalEditor: () => null,
}))

afterEach(cleanup)

function EssayEditorForm({ onSubmit }: { onSubmit: (entry: EssayEntry) => void }) {
  const form = useForm<EssayEntry>({
    defaultValues: createEssayEntry("Explain your reasoning."),
  })

  return (
    <FormProvider {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <EssayEditor />
        <button type="submit">Save</button>
      </form>
    </FormProvider>
  )
}

describe("EssayEditor", () => {
  it("keeps empty optional word limits undefined", async () => {
    const onSubmit = vi.fn()
    render(<EssayEditorForm onSubmit={onSubmit} />)

    fireEvent.click(screen.getByRole("button", { name: "Save" }))

    await waitFor(() => expect(onSubmit).toHaveBeenCalledOnce())
    const entry = onSubmit.mock.calls[0]![0]
    expect(entry.minWordCount).toBeUndefined()
    expect(entry.maxWordCount).toBeUndefined()
    expect(safeParseQuizEntry(entry).success).toBe(true)
  })

  it("converts provided word limits to numbers", async () => {
    const onSubmit = vi.fn()
    render(<EssayEditorForm onSubmit={onSubmit} />)
    const [minimum, maximum] = screen.getAllByRole("spinbutton")

    fireEvent.change(minimum!, { target: { value: "100" } })
    fireEvent.change(maximum!, { target: { value: "500" } })
    fireEvent.click(screen.getByRole("button", { name: "Save" }))

    await waitFor(() => expect(onSubmit).toHaveBeenCalledOnce())
    expect(onSubmit.mock.calls[0]![0]).toEqual(expect.objectContaining({
      minWordCount: 100,
      maxWordCount: 500,
    }))
  })
})
