import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

import type * as BoardFormat from "@/features/board/model/board-format"
import { BoardAddListForm } from "@/features/board/components/board-add-list-form"
import { BoardListColumn } from "@/features/board/components/board-list-column"
import { makeCard, makeList } from "@/features/board/model/board-test-fixtures"
import type { BoardCard } from "@/types/task"

const renderedCards = vi.hoisted(() => ({ count: 0 }))

vi.mock("@/features/board/model/board-format", async (importOriginal) => {
  const original = await importOriginal<typeof BoardFormat>()

  return {
    ...original,
    // Every card calls this once per render, which makes renders countable.
    priorityBar: (...args: Parameters<typeof original.priorityBar>) => {
      renderedCards.count += 1

      return original.priorityBar(...args)
    },
  }
})

afterEach(() => {
  cleanup()
  renderedCards.count = 0
})

describe("BoardAddListForm", () => {
  it("keeps typing to itself until the list is submitted", () => {
    const onSubmit = vi.fn()
    render(
      <BoardAddListForm
        isSaving={false}
        onCancel={vi.fn()}
        onSubmit={onSubmit}
      />
    )

    const input = screen.getByPlaceholderText<HTMLInputElement>("New list")

    fireEvent.change(input, { target: { value: "Shipped" } })
    fireEvent.change(screen.getByLabelText("Status"), {
      target: { value: "DONE" },
    })

    expect(input.value).toBe("Shipped")
    expect(onSubmit).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole("button", { name: "Add list" }))

    expect(onSubmit).toHaveBeenCalledWith("Shipped", "DONE")
  })

  it("cannot submit an empty title with the button", () => {
    render(
      <BoardAddListForm
        isSaving={false}
        onCancel={vi.fn()}
        onSubmit={vi.fn()}
      />
    )

    expect(
      screen.getByRole<HTMLButtonElement>("button", { name: "Add list" })
        .disabled
    ).toBe(true)
  })
})

describe("BoardListColumn", () => {
  const list = makeList("Todo", "TODO", [
    makeCard({ id: "a", title: "Card A" }),
    makeCard({ id: "b", title: "Card B" }),
  ])
  const onCardClick = vi.fn<(card: BoardCard) => void>()
  const onDragCard = vi.fn<(cardId: string) => void>()

  function column(isQuickAdding: boolean, onQuickAdd = vi.fn()) {
    return (
      <BoardListColumn
        draggingCardId={null}
        isQuickAdding={isQuickAdding}
        isReadOnly={false}
        isSaving={false}
        list={list}
        totalCardCount={list.cards.length}
        onCardClick={onCardClick}
        onDragCard={onDragCard}
        onDropCard={vi.fn()}
        onQuickAdd={onQuickAdd}
        onRename={vi.fn()}
        onToggleQuickAdd={vi.fn()}
      />
    )
  }

  it("does not re-render cards while typing a quick add title", () => {
    const onQuickAdd = vi.fn()
    render(column(true, onQuickAdd))
    expect(renderedCards.count).toBe(2)

    const input = screen.getByPlaceholderText("Card title")

    fireEvent.change(input, { target: { value: "New card" } })

    expect(renderedCards.count).toBe(2)

    fireEvent.keyDown(input, { key: "Enter" })

    expect(onQuickAdd).toHaveBeenCalledWith("New card")
  })

  it("does not re-render unchanged cards when the column re-renders", () => {
    const { rerender } = render(column(false))
    expect(renderedCards.count).toBe(2)

    rerender(column(true))

    expect(renderedCards.count).toBe(2)
  })

  it("passes the clicked card to the shared handler", () => {
    render(column(false))

    fireEvent.click(screen.getByText("Card B"))

    expect(onCardClick).toHaveBeenCalledWith(
      expect.objectContaining({ id: "b" })
    )
  })
})
