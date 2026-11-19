import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

import { CardSidePanel } from "@/features/board/components/card-side-panel"
import { makeCard } from "@/features/board/model/board-test-fixtures"
import type { BoardCard, BoardCardInput } from "@/types/task"

function renderPanel(card: BoardCard) {
  const onSave = vi.fn<
    (card: BoardCard, input: BoardCardInput) => Promise<void>
  >(() => Promise.resolve())
  const noop = () => Promise.resolve()

  render(
    <CardSidePanel
      availableLabels={[]}
      canDelete={false}
      canDeleteAnyComment={false}
      card={card}
      currentUserId="me"
      dependencyAnalysis={null}
      dependencyCandidates={[]}
      isLoadingDependencyAnalysis={false}
      isReadOnly={false}
      isSaving={false}
      members={[]}
      onAddComment={noop}
      onAddDependency={noop}
      onClose={() => undefined}
      onCreateSubtask={noop}
      onDelete={() => undefined}
      onDeleteComment={noop}
      onDeleteDependency={noop}
      onDeleteSubtask={noop}
      onDuplicate={() => undefined}
      onEditComment={noop}
      onSave={onSave}
      onUpdateSubtask={noop}
    />
  )

  return onSave
}

// The form loads the card's values in a microtask after the first render.
async function sizeIsChecked(size: string) {
  await waitFor(() =>
    expect(
      screen.getByRole("radio", { name: size }).getAttribute("aria-checked")
    ).toBe("true")
  )
}

describe("card size field", () => {
  afterEach(() => cleanup())

  it("shows the card's size and what it means", async () => {
    renderPanel(makeCard({ size: "M" }))

    await sizeIsChecked("M")
    expect(screen.getByText("About a day")).toBeTruthy()
  })

  it("saves a new size", async () => {
    const onSave = renderPanel(makeCard({ size: "M" }))
    await sizeIsChecked("M")

    fireEvent.click(screen.getByRole("radio", { name: "L" }))
    expect(screen.getByText("A few days")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Save" }))

    await waitFor(() =>
      expect(onSave).toHaveBeenCalledWith(
        expect.objectContaining({ id: "card" }),
        expect.objectContaining({ size: "L" })
      )
    )
  })

  it("clears the size when the selected one is chosen again", async () => {
    const onSave = renderPanel(makeCard({ size: "S" }))
    await sizeIsChecked("S")

    fireEvent.click(screen.getByRole("radio", { name: "S" }))
    expect(screen.getByText("Not sized yet")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Save" }))

    await waitFor(() =>
      expect(onSave).toHaveBeenCalledWith(
        expect.objectContaining({ id: "card" }),
        expect.objectContaining({ size: null })
      )
    )
  })

  it("tells that an XXL card should be split", async () => {
    renderPanel(makeCard({ size: "XXL" }))

    expect(await screen.findByText("More than a week: split it")).toBeTruthy()
  })
})

describe("card milestone field", () => {
  afterEach(() => cleanup())

  it("unmarks a milestone", async () => {
    const onSave = renderPanel(makeCard({ milestoneDate: "2099-03-12" }))

    fireEvent.click(await screen.findByRole("button", { name: "Unmark" }))
    expect(
      screen.getByRole("button", { name: "Mark as milestone" })
    ).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Save" }))

    await waitFor(() =>
      expect(onSave).toHaveBeenCalledWith(
        expect.objectContaining({ id: "card" }),
        expect.objectContaining({ milestoneDate: null })
      )
    )
  })

  it("keeps a milestone whose date has passed when saving other changes", async () => {
    const onSave = renderPanel(
      makeCard({ title: "Demo day", milestoneDate: "2020-01-15" })
    )
    await screen.findByRole("button", { name: "Unmark" })

    fireEvent.click(screen.getByRole("radio", { name: "S" }))
    fireEvent.click(screen.getByRole("button", { name: "Save" }))

    await waitFor(() =>
      expect(onSave).toHaveBeenCalledWith(
        expect.objectContaining({ id: "card" }),
        expect.objectContaining({ milestoneDate: "2020-01-15", size: "S" })
      )
    )
  })
})
