import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

import { CardCommentsSection } from "@/features/board/components/card-comments-section"
import type { BoardCard, BoardCardComment } from "@/types/task"

function comment(
  id: string,
  authorId: string,
  body: string,
  editedAt: string | null = null
): BoardCardComment {
  return {
    id,
    cardId: "card",
    author: { userId: authorId, name: authorId, email: null },
    body,
    createdAt: "2026-11-10T09:00:00Z",
    editedAt,
  }
}

function renderSection(
  comments: BoardCardComment[],
  options: { canDeleteAnyComment?: boolean; isReadOnly?: boolean } = {}
) {
  const onEditComment = vi.fn(() => Promise.resolve())
  const onDeleteComment = vi.fn(() => Promise.resolve())

  render(
    <CardCommentsSection
      addComment={() => Promise.resolve()}
      canDeleteAnyComment={options.canDeleteAnyComment ?? false}
      card={{ id: "card", comments } as unknown as BoardCard}
      commentInput=""
      currentUserId="me"
      isReadOnly={options.isReadOnly ?? false}
      isSaving={false}
      onDeleteComment={onDeleteComment}
      onEditComment={onEditComment}
      setCommentInput={() => undefined}
    />
  )

  return { onDeleteComment, onEditComment }
}

describe("CardCommentsSection", () => {
  afterEach(() => cleanup())

  it("offers edit and delete on the user's own comments only", () => {
    renderSection([
      comment("mine", "me", "Mine"),
      comment("theirs", "ada", "Theirs"),
    ])

    expect(
      screen.getAllByRole("button", { name: "Edit comment" })
    ).toHaveLength(1)
    expect(
      screen.getAllByRole("button", { name: "Delete comment" })
    ).toHaveLength(1)
  })

  it("lets a workspace owner delete anyone's comment but edit only their own", () => {
    renderSection(
      [comment("mine", "me", "Mine"), comment("theirs", "ada", "Theirs")],
      {
        canDeleteAnyComment: true,
      }
    )

    expect(
      screen.getAllByRole("button", { name: "Edit comment" })
    ).toHaveLength(1)
    expect(
      screen.getAllByRole("button", { name: "Delete comment" })
    ).toHaveLength(2)
  })

  it("edits a comment in place", async () => {
    const own = comment("mine", "me", "Shipping friday")
    const { onEditComment } = renderSection([own])

    fireEvent.click(screen.getByRole("button", { name: "Edit comment" }))
    const save = screen.getByRole("button", { name: "Save" })
    expect((save as HTMLButtonElement).disabled).toBe(true)

    fireEvent.change(screen.getByLabelText("Edit comment text"), {
      target: { value: "Shipping monday" },
    })
    fireEvent.click(save)

    await waitFor(() =>
      expect(onEditComment).toHaveBeenCalledWith(
        expect.objectContaining({ id: "card" }),
        own,
        "Shipping monday"
      )
    )
    await waitFor(() =>
      expect(screen.queryByLabelText("Edit comment text")).toBeNull()
    )
  })

  it("deletes a comment", () => {
    const own = comment("mine", "me", "Wrong card")
    const { onDeleteComment } = renderSection([own])

    fireEvent.click(screen.getByRole("button", { name: "Delete comment" }))

    expect(onDeleteComment).toHaveBeenCalledWith(
      expect.objectContaining({ id: "card" }),
      own
    )
  })

  it("marks an edited comment", () => {
    renderSection([comment("mine", "me", "Fixed", "2026-11-10T10:00:00Z")])

    expect(screen.getByText("(edited)")).toBeTruthy()
  })

  it("offers no action on a read-only board", () => {
    renderSection([comment("mine", "me", "Mine")], {
      canDeleteAnyComment: true,
      isReadOnly: true,
    })

    expect(screen.queryByRole("button", { name: /comment/ })).toBeNull()
  })
})
