import { PencilSimple, Plus, Trash } from "@phosphor-icons/react"
import { useState } from "react"

import { Button } from "@/components/ui/button"
import { Textarea } from "@/components/ui/textarea"
import {
  formatDateTime,
  initials,
  userLabel,
} from "@/features/board/model/board-format"
import type { BoardCard, BoardCardComment } from "@/types/task"

type CardCommentsSectionProps = {
  addComment: () => Promise<void>
  canDeleteAnyComment: boolean
  card: BoardCard
  commentInput: string
  currentUserId: string | null
  isReadOnly: boolean
  isSaving: boolean
  onDeleteComment: (card: BoardCard, comment: BoardCardComment) => Promise<void>
  onEditComment: (
    card: BoardCard,
    comment: BoardCardComment,
    body: string
  ) => Promise<void>
  setCommentInput: (value: string) => void
}

export function CardCommentsSection({
  addComment,
  canDeleteAnyComment,
  card,
  commentInput,
  currentUserId,
  isReadOnly,
  isSaving,
  onDeleteComment,
  onEditComment,
  setCommentInput,
}: CardCommentsSectionProps) {
  const [editingId, setEditingId] = useState<string | null>(null)
  const [draft, setDraft] = useState("")

  function startEditing(comment: BoardCardComment) {
    setEditingId(comment.id)
    setDraft(comment.body)
  }

  async function saveEdit(comment: BoardCardComment) {
    await onEditComment(card, comment, draft)
    setEditingId(null)
  }

  return (
    <section className="grid gap-2">
      <h3 className="text-sm font-semibold">Comments</h3>
      <div className="grid gap-2">
        {card.comments.length > 0 ? (
          card.comments.map((comment) => {
            const isOwn = comment.author.userId === currentUserId
            const canEdit = !isReadOnly && isOwn
            const canDelete = !isReadOnly && (isOwn || canDeleteAnyComment)

            return (
              <div
                key={comment.id}
                className="group grid gap-1 rounded-md border border-zinc-950/10 bg-white p-3 text-sm dark:border-white/10 dark:bg-zinc-950"
              >
                <div className="flex items-center gap-2 text-xs text-muted-foreground">
                  <span className="grid size-6 place-items-center rounded-full bg-zinc-950 text-[10px] font-semibold text-white dark:bg-white dark:text-zinc-950">
                    {initials(comment.author.name ?? comment.author.email)}
                  </span>
                  <span className="font-medium text-zinc-800 dark:text-zinc-100">
                    {userLabel(comment.author)}
                  </span>
                  <span>{formatDateTime(comment.createdAt)}</span>
                  {comment.editedAt ? (
                    <span title={`Edited ${formatDateTime(comment.editedAt)}`}>
                      (edited)
                    </span>
                  ) : null}
                  {editingId !== comment.id && (canEdit || canDelete) ? (
                    <span className="ml-auto flex gap-1">
                      {canEdit ? (
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon-xs"
                          aria-label="Edit comment"
                          disabled={isSaving}
                          onClick={() => startEditing(comment)}
                        >
                          <PencilSimple />
                        </Button>
                      ) : null}
                      {canDelete ? (
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon-xs"
                          aria-label="Delete comment"
                          disabled={isSaving}
                          onClick={() => void onDeleteComment(card, comment)}
                        >
                          <Trash />
                        </Button>
                      ) : null}
                    </span>
                  ) : null}
                </div>
                {editingId === comment.id ? (
                  <div className="grid gap-2">
                    <Textarea
                      aria-label="Edit comment text"
                      value={draft}
                      onChange={(event) => setDraft(event.target.value)}
                      className="min-h-16 rounded-md bg-white text-sm dark:bg-white/[0.06]"
                    />
                    <div className="flex gap-2">
                      <Button
                        type="button"
                        className="h-7 rounded-md px-3 text-xs"
                        disabled={
                          !draft.trim() ||
                          draft.trim() === comment.body ||
                          isSaving
                        }
                        onClick={() => void saveEdit(comment)}
                      >
                        Save
                      </Button>
                      <Button
                        type="button"
                        variant="ghost"
                        className="h-7 rounded-md px-3 text-xs"
                        onClick={() => setEditingId(null)}
                      >
                        Cancel
                      </Button>
                    </div>
                  </div>
                ) : (
                  <p className="leading-6 whitespace-pre-wrap">
                    {comment.body}
                  </p>
                )}
              </div>
            )
          })
        ) : (
          <p className="rounded-md border border-dashed border-zinc-950/10 px-3 py-4 text-sm text-muted-foreground dark:border-white/10">
            No comments yet.
          </p>
        )}
      </div>
      {!isReadOnly ? (
        <div className="grid gap-2">
          <Textarea
            value={commentInput}
            onChange={(event) => setCommentInput(event.target.value)}
            placeholder="Write a comment"
            className="min-h-20 rounded-md bg-white text-sm dark:bg-white/[0.06]"
          />
          <Button
            type="button"
            className="h-8 justify-self-start rounded-md px-3 text-xs"
            disabled={!commentInput.trim() || isSaving}
            onClick={() => void addComment()}
          >
            <Plus className="size-3.5" />
            Comment
          </Button>
        </div>
      ) : null}
    </section>
  )
}
