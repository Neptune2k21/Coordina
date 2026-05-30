import {
  CheckCircle,
  CircleNotch,
  MagnifyingGlass,
  Plus,
} from "@phosphor-icons/react"
import { useCallback, useEffect, useMemo, useState } from "react"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { ApiError } from "@/features/auth/auth-api"
import { useAuth } from "@/features/auth/auth-context"
import type { Project } from "@/features/projects/project-types"
import {
  countBoardCards,
  filterBoard,
  findCard,
  getBoardLabels,
  moveCardInBoard,
  removeCardFromBoard,
  renameListInBoard,
} from "@/features/tasks/board-utils"
import { BoardListColumn } from "@/features/tasks/components/board-list-column"
import { BoardSkeleton } from "@/features/tasks/components/board-skeleton"
import { CardSidePanel } from "@/features/tasks/components/card-side-panel"
import { TemplateChooser } from "@/features/tasks/components/template-chooser"
import {
  createBoard,
  createCard,
  createList,
  deleteCard,
  getDefaultBoard,
  moveCard,
  updateCard,
  updateList,
} from "@/features/tasks/task-api"
import type {
  Board,
  BoardCard,
  BoardCardInput,
  BoardList,
  BoardTemplate,
} from "@/features/tasks/task-types"
import { listWorkspaceMembers } from "@/features/workspaces/workspace-api"
import type {
  Workspace,
  WorkspaceMember,
} from "@/features/workspaces/workspace-types"

type ProjectBoardProps = {
  project: Project
  workspace: Workspace
}

export function ProjectBoard({ project, workspace }: ProjectBoardProps) {
  const { session, signOut } = useAuth()
  const [board, setBoard] = useState<Board | null>(null)
  const [members, setMembers] = useState<WorkspaceMember[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [isCreatingBoard, setIsCreatingBoard] = useState(false)
  const [isMutating, setIsMutating] = useState(false)
  const [search, setSearch] = useState("")
  const [quickAddListId, setQuickAddListId] = useState<string | null>(null)
  const [quickTitle, setQuickTitle] = useState("")
  const [newListTitle, setNewListTitle] = useState("")
  const [draggingCardId, setDraggingCardId] = useState<string | null>(null)
  const [selectedCardId, setSelectedCardId] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const isReadOnly = project.status === "COMPLETED"
  const filteredBoard = useMemo(
    () => filterBoard(board, search),
    [board, search]
  )
  const selectedCard = useMemo(
    () =>
      board?.lists
        .flatMap((list) => list.cards)
        .find((card) => card.id === selectedCardId) ?? null,
    [board, selectedCardId]
  )
  const boardLabels = useMemo(() => getBoardLabels(board), [board])
  const cardCount = useMemo(() => countBoardCards(board), [board])
  const canDeleteCards =
    !isReadOnly &&
    (workspace.role === "OWNER" || project.projectOwnerId === session?.user.id)

  const handleRequestError = useCallback(
    (requestError: unknown, fallback: string) => {
      if (requestError instanceof ApiError) {
        if (requestError.status === 404) {
          setError(null)
          setBoard(null)
          return
        }

        setError(requestError.message)

        if (requestError.status === 401) {
          signOut()
        }
        return
      }

      setError(fallback)
    },
    [signOut]
  )

  const loadBoard = useCallback(async () => {
    if (!session) {
      setBoard(null)
      return
    }

    setIsLoading(true)
    setError(null)

    try {
      const [loadedBoard, loadedMembers] = await Promise.all([
        getDefaultBoard(session.accessToken, workspace.id, project.id).catch(
          (requestError) => {
            if (
              requestError instanceof ApiError &&
              requestError.status === 404
            ) {
              return null
            }

            throw requestError
          }
        ),
        listWorkspaceMembers(session.accessToken, workspace.id),
      ])
      setBoard(loadedBoard)
      setMembers(loadedMembers)
    } catch (requestError) {
      handleRequestError(requestError, "Unable to load board.")
    } finally {
      setIsLoading(false)
    }
  }, [handleRequestError, project.id, session, workspace.id])

  useEffect(() => {
    queueMicrotask(() => {
      setBoard(null)
      setSelectedCardId(null)
      void loadBoard()
    })
  }, [loadBoard])

  useEffect(() => {
    if (!notice) {
      return
    }

    const timeout = window.setTimeout(() => setNotice(null), 1800)
    return () => window.clearTimeout(timeout)
  }, [notice])

  function showNotice(message: string) {
    setNotice(message)
  }

  async function handleCreateBoard(
    template: BoardTemplate,
    customListTitles?: string[]
  ) {
    if (!session || isReadOnly) {
      return
    }

    setIsCreatingBoard(true)
    setError(null)

    try {
      setBoard(
        await createBoard(session.accessToken, workspace.id, project.id, {
          name: `${project.name} Board`,
          template,
          customListTitles,
        })
      )
      showNotice("Board created")
    } catch (requestError) {
      handleRequestError(requestError, "Board could not be created.")
    } finally {
      setIsCreatingBoard(false)
    }
  }

  async function handleCreateList() {
    if (!session || !board || !newListTitle.trim() || isReadOnly) {
      return
    }

    setIsMutating(true)
    setError(null)

    try {
      setBoard(
        await createList(
          session.accessToken,
          workspace.id,
          project.id,
          board.id,
          newListTitle
        )
      )
      setNewListTitle("")
      showNotice("List created")
    } catch (requestError) {
      handleRequestError(requestError, "List could not be created.")
    } finally {
      setIsMutating(false)
    }
  }

  async function handleRenameList(list: BoardList, title: string) {
    if (
      !session ||
      !board ||
      !title.trim() ||
      title === list.title ||
      isReadOnly
    ) {
      return
    }

    setBoard(renameListInBoard(board, list.id, title))

    try {
      setBoard(
        await updateList(
          session.accessToken,
          workspace.id,
          project.id,
          board.id,
          list.id,
          title
        )
      )
    } catch (requestError) {
      setBoard(board)
      handleRequestError(requestError, "List could not be renamed.")
    }
  }

  async function handleQuickAdd(list: BoardList) {
    if (!session || !board || !quickTitle.trim() || isReadOnly) {
      return
    }

    setIsMutating(true)
    setError(null)

    try {
      setBoard(
        await createCard(
          session.accessToken,
          workspace.id,
          project.id,
          board.id,
          list.id,
          { title: quickTitle }
        )
      )
      setQuickTitle("")
      setQuickAddListId(null)
      showNotice("Card created")
    } catch (requestError) {
      handleRequestError(requestError, "Card could not be created.")
    } finally {
      setIsMutating(false)
    }
  }

  async function handleMoveCard(cardId: string, listId: string) {
    if (!session || !board || isReadOnly) {
      return
    }

    const card = findCard(board, cardId)

    if (!card || card.listId === listId) {
      return
    }

    const previousBoard = board
    setBoard(moveCardInBoard(board, cardId, listId))

    try {
      setBoard(
        await moveCard(
          session.accessToken,
          workspace.id,
          project.id,
          board.id,
          cardId,
          listId
        )
      )
      showNotice("Card moved")
    } catch (requestError) {
      setBoard(previousBoard)
      handleRequestError(requestError, "Card could not be moved.")
    } finally {
      setDraggingCardId(null)
    }
  }

  async function handleSaveCard(card: BoardCard, input: BoardCardInput) {
    if (!session || !board || isReadOnly) {
      return
    }

    setIsMutating(true)
    setError(null)

    try {
      setBoard(
        await updateCard(
          session.accessToken,
          workspace.id,
          project.id,
          board.id,
          card.id,
          input
        )
      )
      showNotice("Card saved")
    } catch (requestError) {
      handleRequestError(requestError, "Card could not be updated.")
      throw requestError
    } finally {
      setIsMutating(false)
    }
  }

  async function handleDeleteCard(card: BoardCard) {
    if (!session || !board || !canDeleteCards || isReadOnly) {
      return
    }

    setIsMutating(true)
    setError(null)

    try {
      await deleteCard(
        session.accessToken,
        workspace.id,
        project.id,
        board.id,
        card.id
      )
      setBoard(removeCardFromBoard(board, card.id))
      setSelectedCardId(null)
      showNotice("Card deleted")
    } catch (requestError) {
      handleRequestError(requestError, "Card could not be deleted.")
    } finally {
      setIsMutating(false)
    }
  }

  if (isLoading) {
    return <BoardSkeleton />
  }

  if (!board) {
    return isReadOnly ? (
      <BoardStateMessage
        title="Board unavailable"
        description="Completed projects are read only."
      />
    ) : (
      <TemplateChooser
        isCreating={isCreatingBoard}
        onCreate={(template, customListTitles) =>
          void handleCreateBoard(template, customListTitles)
        }
      />
    )
  }

  return (
    <div className="flex h-[calc(100svh-5.75rem)] min-h-[480px] flex-col overflow-hidden rounded-md border border-zinc-950/10 bg-zinc-50 shadow-xs dark:border-white/10 dark:bg-zinc-950/70">
      <div className="flex shrink-0 flex-col gap-2 border-b border-zinc-950/10 bg-white px-2 py-2 md:h-11 md:flex-row md:items-center dark:border-white/10 dark:bg-zinc-950">
        <div className="flex min-w-0 flex-1 items-center gap-2">
          <p className="truncate text-sm font-semibold">{project.name}</p>
          <Badge className="h-5 rounded-sm px-1.5 font-mono text-[10px]">
            {board.template}
          </Badge>
          {isReadOnly ? (
            <Badge className="h-5 rounded-sm bg-zinc-950/5 px-1.5 text-[10px] text-muted-foreground dark:bg-white/10">
              READ ONLY
            </Badge>
          ) : null}
        </div>
        <label className="flex h-8 min-w-0 items-center gap-2 rounded-md border border-zinc-950/10 bg-zinc-50 px-2 text-xs md:w-64 dark:border-white/10 dark:bg-white/[0.06]">
          <MagnifyingGlass className="size-3.5 text-muted-foreground" />
          <Input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search cards"
            className="h-6 border-0 bg-transparent px-0 text-xs shadow-none focus-visible:ring-0"
          />
        </label>
        <div className="flex items-center gap-2">
          <Button
            type="button"
            size="sm"
            className="h-8 rounded-md px-2 text-xs"
            disabled={isReadOnly || board.lists.length === 0}
            onClick={() => setQuickAddListId(board.lists[0]?.id ?? null)}
          >
            <Plus className="size-3.5" weight="bold" />
            Card
          </Button>
          <div className="hidden h-8 items-center gap-1.5 rounded-md border border-zinc-950/10 bg-zinc-50 px-2 text-[11px] font-medium text-muted-foreground lg:flex dark:border-white/10 dark:bg-white/[0.06]">
            {cardCount} cards
          </div>
          {notice ? (
            <div className="hidden h-8 items-center gap-1.5 rounded-md border border-teal-500/20 bg-teal-500/10 px-2 text-[11px] font-medium text-teal-700 sm:flex dark:text-teal-200">
              <CheckCircle className="size-3.5" weight="fill" />
              {notice}
            </div>
          ) : null}
          {isMutating ? (
            <CircleNotch className="size-4 animate-spin text-muted-foreground" />
          ) : null}
        </div>
      </div>

      {error ? (
        <div className="mx-2 mt-2 rounded-md border border-destructive/25 bg-white px-3 py-2 text-xs text-destructive dark:bg-zinc-950">
          {error}
        </div>
      ) : null}

      <div className="flex min-h-0 flex-1 gap-2 overflow-x-auto p-2">
        {filteredBoard?.lists.map((list) => (
          <BoardListColumn
            key={list.id}
            draggingCardId={draggingCardId}
            isQuickAdding={quickAddListId === list.id}
            isReadOnly={isReadOnly}
            isSaving={isMutating}
            list={list}
            quickTitle={quickTitle}
            onCardClick={(card) => setSelectedCardId(card.id)}
            onDragCard={(cardId) => setDraggingCardId(cardId)}
            onDropCard={(cardId) => void handleMoveCard(cardId, list.id)}
            onQuickAdd={() => void handleQuickAdd(list)}
            onQuickTitleChange={setQuickTitle}
            onRename={(title) => void handleRenameList(list, title)}
            onToggleQuickAdd={() => {
              setQuickAddListId(quickAddListId === list.id ? null : list.id)
              setQuickTitle("")
            }}
          />
        ))}
        {!isReadOnly ? (
          <div className="w-72 shrink-0 rounded-md border border-zinc-950/10 bg-white p-2 shadow-xs dark:border-white/10 dark:bg-zinc-950">
            <div className="flex gap-2">
              <Input
                value={newListTitle}
                onChange={(event) => setNewListTitle(event.target.value)}
                placeholder="New list"
                className="h-8 rounded-md text-xs"
                onKeyDown={(event) => {
                  if (event.key === "Enter") {
                    void handleCreateList()
                  }
                }}
              />
              <Button
                type="button"
                size="sm"
                className="h-8 rounded-md px-2"
                disabled={!newListTitle.trim() || isMutating}
                onClick={() => void handleCreateList()}
              >
                <Plus className="size-3.5" />
              </Button>
            </div>
          </div>
        ) : null}
      </div>

      <CardSidePanel
        canDelete={canDeleteCards}
        card={selectedCard}
        isReadOnly={isReadOnly}
        isSaving={isMutating}
        members={members}
        availableLabels={boardLabels}
        onClose={() => setSelectedCardId(null)}
        onDelete={(card) => void handleDeleteCard(card)}
        onSave={handleSaveCard}
      />
    </div>
  )
}

function BoardStateMessage({
  description,
  title,
}: {
  description: string
  title: string
}) {
  return (
    <div className="grid min-h-64 place-items-center rounded-md border border-dashed border-zinc-950/15 bg-white px-4 py-12 text-center dark:border-white/15 dark:bg-white/[0.035]">
      <div className="max-w-sm">
        <h2 className="text-sm font-semibold">{title}</h2>
        <p className="mt-2 text-sm leading-6 text-muted-foreground">
          {description}
        </p>
      </div>
    </div>
  )
}
