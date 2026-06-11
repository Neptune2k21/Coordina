import {
  Check,
  CheckCircle,
  CircleNotch,
  ClockCountdown,
  FunnelSimple,
  GitBranch,
  Kanban,
  MagnifyingGlass,
  Plus,
  Target,
  type Icon,
  UsersThree,
  WarningCircle,
  X,
} from "@phosphor-icons/react"
import { useCallback, useEffect, useMemo, useState } from "react"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { Input } from "@/components/ui/input"
import { ApiError } from "@/lib/api"
import { useAuth } from "@/features/auth/auth-context"
import type { Project } from "@/types/project"
import { boardCardPriorities } from "@/features/tasks/board-constants"
import {
  type BoardFilters,
  type BoardFocus,
  type BoardMetrics,
  type WipAlert,
  countBoardCards,
  countUserCards,
  filterBoard,
  findCard,
  getBoardLabels,
  getBoardMetrics,
  getWipAlerts,
  isActiveWorkListTitle,
  moveCardInBoard,
  removeCardFromBoard,
  renameListInBoard,
} from "@/features/tasks/board-utils"
import { BoardListColumn } from "@/features/tasks/components/board-list-column"
import { BoardSkeleton } from "@/features/tasks/components/board-skeleton"
import { CardSidePanel } from "@/features/tasks/components/card-side-panel"
import { TemplateChooser } from "@/features/tasks/components/template-chooser"
import {
  addCardComment,
  addCardDependency,
  createBoard,
  createCard,
  createList,
  createSubtask,
  deleteCard,
  deleteCardDependency,
  deleteSubtask,
  getBoardGraph,
  getCardDependencyAnalysis,
  getDefaultBoard,
  moveCard,
  updateCard,
  updateList,
  updateSubtask,
} from "@/features/tasks/task-api"
import type {
  Board,
  BoardCard,
  BoardCardDependencyAnalysis,
  BoardCardInput,
  BoardCardSubtask,
  BoardGraph,
  BoardGraphPlanItem,
  BoardCardPriority,
  BoardList,
  BoardTemplate,
} from "@/types/task"
import { listWorkspaceMembers } from "@/features/workspaces/workspace-api"
import type { Workspace, WorkspaceMember } from "@/types/workspace"

type ProjectBoardProps = {
  project: Project
  workspace: Workspace
}

export function ProjectBoard({ project, workspace }: ProjectBoardProps) {
  const { session, signOut } = useAuth()
  const [board, setBoard] = useState<Board | null>(null)
  const [boardGraph, setBoardGraph] = useState<BoardGraph | null>(null)
  const [dependencyAnalysis, setDependencyAnalysis] =
    useState<BoardCardDependencyAnalysis | null>(null)
  const [members, setMembers] = useState<WorkspaceMember[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [isCreatingBoard, setIsCreatingBoard] = useState(false)
  const [isMutating, setIsMutating] = useState(false)
  const [search, setSearch] = useState("")
  const [focus, setFocus] = useState<BoardFocus>("all")
  const [priorityFilter, setPriorityFilter] = useState<
    BoardCardPriority | "ALL"
  >("ALL")
  const [labelFilter, setLabelFilter] = useState<string | "ALL">("ALL")
  const [assigneeFilter, setAssigneeFilter] = useState<string | "ALL">("ALL")
  const [quickAddListId, setQuickAddListId] = useState<string | null>(null)
  const [quickTitle, setQuickTitle] = useState("")
  const [newListTitle, setNewListTitle] = useState("")
  const [isAddingList, setIsAddingList] = useState(false)
  const [draggingCardId, setDraggingCardId] = useState<string | null>(null)
  const [selectedCardId, setSelectedCardId] = useState<string | null>(null)
  const [isLoadingDependencyAnalysis, setIsLoadingDependencyAnalysis] =
    useState(false)
  const [notice, setNotice] = useState<string | null>(null)
  const [isPlanningPanelOpen, setIsPlanningPanelOpen] = useState(false)

  const isReadOnly = project.status === "COMPLETED"
  const sessionUserId = session?.user.id ?? null
  const boardFilters = useMemo<BoardFilters>(
    () => ({
      assigneeId: assigneeFilter,
      focus,
      label: labelFilter,
      priority: priorityFilter,
      sessionUserId,
    }),
    [assigneeFilter, focus, labelFilter, priorityFilter, sessionUserId]
  )
  const filteredBoard = useMemo(
    () => filterBoard(board, search, boardFilters),
    [board, boardFilters, search]
  )
  const selectedCard = useMemo(
    () =>
      board?.lists
        .flatMap((list) => list.cards)
        .find((card) => card.id === selectedCardId) ?? null,
    [board, selectedCardId]
  )
  const dependencyCandidates = useMemo(
    () =>
      board?.lists
        .flatMap((list) => list.cards)
        .filter((card) => card.id !== selectedCardId) ?? [],
    [board, selectedCardId]
  )
  const boardLabels = useMemo(() => getBoardLabels(board), [board])
  const metrics = useMemo(() => getBoardMetrics(board), [board])
  const wipAlerts = useMemo(() => getWipAlerts(board), [board])
  const cardCount = useMemo(() => countBoardCards(board), [board])
  const filteredCardCount = useMemo(
    () => countBoardCards(filteredBoard),
    [filteredBoard]
  )
  const myCardCount = useMemo(
    () => countUserCards(board, sessionUserId),
    [board, sessionUserId]
  )
  const activeFilterCount = [
    search.trim(),
    focus !== "all",
    priorityFilter !== "ALL",
    labelFilter !== "ALL",
    assigneeFilter !== "ALL",
  ].filter(Boolean).length
  const canDeleteCards =
    !isReadOnly &&
    (workspace.role === "OWNER" || project.projectOwnerId === session?.user.id)
  const canShowPlanningPanel = cardCount > 0
  const isPlanningPanelVisible = canShowPlanningPanel && isPlanningPanelOpen

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
      setBoardGraph(null)
    } catch (requestError) {
      handleRequestError(requestError, "Unable to load board.")
    } finally {
      setIsLoading(false)
    }
  }, [handleRequestError, project.id, session, workspace.id])

  useEffect(() => {
    queueMicrotask(() => {
      setBoard(null)
      setBoardGraph(null)
      setDependencyAnalysis(null)
      setSelectedCardId(null)
      setSearch("")
      setFocus("all")
      setPriorityFilter("ALL")
      setLabelFilter("ALL")
      setAssigneeFilter("ALL")
      void loadBoard()
    })
  }, [loadBoard])

  useEffect(() => {
    if (!session || !board) {
      queueMicrotask(() => setBoardGraph(null))
      return
    }

    let ignore = false
    const accessToken = session.accessToken
    const boardId = board.id

    async function loadGraph() {
      try {
        const graph = await getBoardGraph(
          accessToken,
          workspace.id,
          project.id,
          boardId
        )

        if (!ignore) {
          setBoardGraph(graph)
        }
      } catch {
        if (!ignore) {
          setBoardGraph(null)
        }
      }
    }

    void loadGraph()

    return () => {
      ignore = true
    }
  }, [board, project.id, session, workspace.id])

  useEffect(() => {
    if (!session || !board || !selectedCardId) {
      queueMicrotask(() => {
        setDependencyAnalysis(null)
        setIsLoadingDependencyAnalysis(false)
      })
      return
    }

    let ignore = false
    const accessToken = session.accessToken
    const boardId = board.id
    const cardId = selectedCardId
    queueMicrotask(() => {
      if (!ignore) {
        setIsLoadingDependencyAnalysis(true)
      }
    })

    async function loadAnalysis() {
      try {
        const analysis = await getCardDependencyAnalysis(
          accessToken,
          workspace.id,
          project.id,
          boardId,
          cardId
        )

        if (!ignore) {
          setDependencyAnalysis(analysis)
        }
      } catch {
        if (!ignore) {
          setDependencyAnalysis(null)
        }
      } finally {
        if (!ignore) {
          setIsLoadingDependencyAnalysis(false)
        }
      }
    }

    void loadAnalysis()

    return () => {
      ignore = true
    }
  }, [board, project.id, selectedCardId, session, workspace.id])

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

  function clearBoardFilters() {
    setSearch("")
    setFocus("all")
    setPriorityFilter("ALL")
    setLabelFilter("ALL")
    setAssigneeFilter("ALL")
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
      setIsAddingList(false)
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

  async function handleDuplicateCard(card: BoardCard) {
    if (!session || !board || isReadOnly) {
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
          card.listId,
          {
            assigneeIds: card.assignees.map((assignee) => assignee.userId),
            description: card.description ?? "",
            dueDate: card.dueDate,
            labels: card.labels,
            priority: card.priority ?? "",
            title: `Copy of ${card.title}`,
          }
        )
      )
      showNotice("Card duplicated")
    } catch (requestError) {
      handleRequestError(requestError, "Card could not be duplicated.")
    } finally {
      setIsMutating(false)
    }
  }

  async function handleAddComment(card: BoardCard, body: string) {
    if (!session || !board || isReadOnly) {
      return
    }

    setIsMutating(true)
    setError(null)

    try {
      setBoard(
        await addCardComment(
          session.accessToken,
          workspace.id,
          project.id,
          board.id,
          card.id,
          body
        )
      )
      showNotice("Comment added")
    } catch (requestError) {
      handleRequestError(requestError, "Comment could not be added.")
      throw requestError
    } finally {
      setIsMutating(false)
    }
  }

  async function handleCreateSubtask(card: BoardCard, title: string) {
    if (!session || !board || isReadOnly) {
      return
    }

    setIsMutating(true)
    setError(null)

    try {
      setBoard(
        await createSubtask(
          session.accessToken,
          workspace.id,
          project.id,
          board.id,
          card.id,
          title
        )
      )
      showNotice("Subtask added")
    } catch (requestError) {
      handleRequestError(requestError, "Subtask could not be added.")
      throw requestError
    } finally {
      setIsMutating(false)
    }
  }

  async function handleUpdateSubtask(
    card: BoardCard,
    subtask: BoardCardSubtask,
    input: { title?: string; isCompleted?: boolean }
  ) {
    if (!session || !board || isReadOnly) {
      return
    }

    setIsMutating(true)
    setError(null)

    try {
      setBoard(
        await updateSubtask(
          session.accessToken,
          workspace.id,
          project.id,
          board.id,
          card.id,
          subtask.id,
          input
        )
      )
      showNotice("Subtask updated")
    } catch (requestError) {
      handleRequestError(requestError, "Subtask could not be updated.")
      throw requestError
    } finally {
      setIsMutating(false)
    }
  }

  async function handleDeleteSubtask(
    card: BoardCard,
    subtask: BoardCardSubtask
  ) {
    if (!session || !board || isReadOnly) {
      return
    }

    setIsMutating(true)
    setError(null)

    try {
      await deleteSubtask(
        session.accessToken,
        workspace.id,
        project.id,
        board.id,
        card.id,
        subtask.id
      )
      await loadBoard()
      showNotice("Subtask deleted")
    } catch (requestError) {
      handleRequestError(requestError, "Subtask could not be deleted.")
    } finally {
      setIsMutating(false)
    }
  }

  async function handleAddDependency(card: BoardCard, dependsOnCardId: string) {
    if (!session || !board || isReadOnly) {
      return
    }

    setIsMutating(true)
    setError(null)

    try {
      setBoard(
        await addCardDependency(
          session.accessToken,
          workspace.id,
          project.id,
          board.id,
          card.id,
          dependsOnCardId
        )
      )
      showNotice("Dependency added")
    } catch (requestError) {
      handleRequestError(requestError, "Dependency could not be added.")
      throw requestError
    } finally {
      setIsMutating(false)
    }
  }

  async function handleDeleteDependency(
    card: BoardCard,
    dependsOnCardId: string
  ) {
    if (!session || !board || isReadOnly) {
      return
    }

    setIsMutating(true)
    setError(null)

    try {
      await deleteCardDependency(
        session.accessToken,
        workspace.id,
        project.id,
        board.id,
        card.id,
        dependsOnCardId
      )
      await loadBoard()
      showNotice("Dependency removed")
    } catch (requestError) {
      handleRequestError(requestError, "Dependency could not be removed.")
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
      <BoardSetupOnboarding
        isCreating={isCreatingBoard}
        projectName={project.name}
        onCreate={(template, customListTitles) =>
          void handleCreateBoard(template, customListTitles)
        }
      />
    )
  }

  return (
    <div className="min-h-480px flex h-[calc(100svh-5.75rem)] flex-col overflow-hidden rounded-md border border-zinc-950/10 bg-zinc-50 shadow-xs dark:border-white/10 dark:bg-zinc-950/70">
      <div className="grid shrink-0 gap-2 border-b border-zinc-950/10 bg-white px-2 py-2 dark:border-white/10 dark:bg-zinc-950">
        <div className="flex flex-col gap-2 xl:flex-row xl:items-center">
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
            {activeFilterCount > 0 ? (
              <Badge className="h-5 rounded-sm border-sky-500/20 bg-sky-500/10 px-1.5 text-[10px] text-sky-700 dark:text-sky-200">
                {filteredCardCount}/{cardCount}
              </Badge>
            ) : null}
          </div>
          <div className="flex min-w-0 flex-col gap-2 md:flex-row md:items-center">
            <label className="dark:bg-white/0.06 flex h-8 min-w-0 items-center gap-2 rounded-md border border-zinc-950/10 bg-zinc-50 px-2 text-xs md:w-64 dark:border-white/10">
              <MagnifyingGlass className="size-3.5 text-muted-foreground" />
              <Input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Search cards"
                className="h-6 border-0 bg-transparent px-0 text-xs shadow-none focus-visible:ring-0"
              />
            </label>
            <BoardFilterMenu
              activeFilterCount={activeFilterCount}
              assigneeFilter={assigneeFilter}
              labelFilter={labelFilter}
              labels={boardLabels}
              members={members}
              priorityFilter={priorityFilter}
              onAssigneeFilterChange={setAssigneeFilter}
              onClear={clearBoardFilters}
              onLabelFilterChange={setLabelFilter}
              onPriorityFilterChange={setPriorityFilter}
            />
            {canShowPlanningPanel ? (
              <Button
                type="button"
                variant={isPlanningPanelOpen ? "default" : "outline"}
                size="sm"
                className="h-8 rounded-md px-2 text-xs"
                aria-pressed={isPlanningPanelOpen}
                onClick={() => setIsPlanningPanelOpen((isOpen) => !isOpen)}
              >
                <Target
                  className="size-3.5"
                  weight={isPlanningPanelOpen ? "bold" : "regular"}
                />
                Insights
              </Button>
            ) : null}
          </div>
          <div className="flex items-center gap-2">
            <Button
              type="button"
              size="sm"
              className="h-8 rounded-md px-2 text-xs"
              disabled={isReadOnly}
              onClick={() => setIsAddingList(true)}
            >
              <Plus className="size-3.5" weight="bold" />
              List
            </Button>
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
        <BoardFocusTabs
          focus={focus}
          metrics={metrics}
          myCardCount={myCardCount}
          onFocusChange={setFocus}
        />
        {isPlanningPanelVisible ? (
          <BoardPlanningPanel
            graph={boardGraph}
            metrics={metrics}
            onSelectCard={setSelectedCardId}
            wipAlerts={wipAlerts}
          />
        ) : null}
        {activeFilterCount > 0 ? (
          <div className="flex flex-wrap items-center gap-2 rounded-md border border-sky-500/15 bg-sky-500/5 px-2 py-1.5 text-xs text-sky-800 dark:text-sky-100">
            <FunnelSimple className="size-3.5" weight="bold" />
            <span className="font-medium">{filteredCardCount} visible</span>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="h-6 rounded-md px-2 text-xs text-sky-800 hover:bg-sky-500/10 dark:text-sky-100"
              onClick={clearBoardFilters}
            >
              <X className="size-3.5" />
              Clear
            </Button>
          </div>
        ) : null}
      </div>

      {error ? (
        <div className="mx-2 mt-2 rounded-md border border-destructive/25 bg-white px-3 py-2 text-xs text-destructive dark:bg-zinc-950">
          {error}
        </div>
      ) : null}

      <div className="flex min-h-0 flex-1 gap-2 overflow-x-auto p-2">
        {filteredBoard?.lists.map((list) => {
          const sourceList = board.lists.find((item) => item.id === list.id)
          const totalCardCount = sourceList?.cards.length ?? list.cards.length
          const wipLimit = isActiveWorkListTitle(list.title) ? 5 : undefined

          return (
            <BoardListColumn
              key={list.id}
              draggingCardId={draggingCardId}
              isQuickAdding={quickAddListId === list.id}
              isReadOnly={isReadOnly}
              isSaving={isMutating}
              list={list}
              quickTitle={quickTitle}
              totalCardCount={totalCardCount}
              wipLimit={wipLimit}
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
          )
        })}
        {!isReadOnly && isAddingList ? (
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
                variant="ghost"
                size="icon"
                className="size-8 rounded-md"
                onClick={() => {
                  setIsAddingList(false)
                  setNewListTitle("")
                }}
              >
                <X className="size-3.5" />
              </Button>
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
        ) : !isReadOnly ? (
          <button
            type="button"
            className="dark:bg-white/0.04 dark:hover:bg-white/0.07 grid h-12 w-72 shrink-0 place-items-center rounded-md border border-dashed border-zinc-950/15 bg-white/70 text-xs font-semibold text-muted-foreground transition-colors hover:bg-white dark:border-white/15"
            onClick={() => setIsAddingList(true)}
          >
            <span className="inline-flex items-center gap-2">
              <Plus className="size-3.5" />
              Add another list
            </span>
          </button>
        ) : null}
      </div>

      <CardSidePanel
        canDelete={canDeleteCards}
        card={selectedCard}
        dependencyAnalysis={dependencyAnalysis}
        dependencyCandidates={dependencyCandidates}
        isLoadingDependencyAnalysis={isLoadingDependencyAnalysis}
        isReadOnly={isReadOnly}
        isSaving={isMutating}
        members={members}
        availableLabels={boardLabels}
        onAddComment={handleAddComment}
        onAddDependency={handleAddDependency}
        onClose={() => setSelectedCardId(null)}
        onCreateSubtask={handleCreateSubtask}
        onDelete={(card) => void handleDeleteCard(card)}
        onDeleteDependency={handleDeleteDependency}
        onDeleteSubtask={handleDeleteSubtask}
        onDuplicate={(card) => void handleDuplicateCard(card)}
        onSave={handleSaveCard}
        onUpdateSubtask={handleUpdateSubtask}
      />
    </div>
  )
}

function BoardFocusTabs({
  focus,
  metrics,
  myCardCount,
  onFocusChange,
}: {
  focus: BoardFocus
  metrics: BoardMetrics
  myCardCount: number
  onFocusChange: (focus: BoardFocus) => void
}) {
  const riskCount = metrics.risk
  const items: Array<{
    id: BoardFocus
    icon: Icon
    label: string
    value: number
  }> = [
    { id: "all", icon: Kanban, label: "All", value: metrics.total },
    { id: "mine", icon: UsersThree, label: "Mine", value: myCardCount },
    { id: "risk", icon: WarningCircle, label: "At risk", value: riskCount },
    {
      id: "due-soon",
      icon: ClockCountdown,
      label: "Due",
      value: metrics.dueSoon + metrics.overdue,
    },
    {
      id: "unassigned",
      icon: Target,
      label: "Unassigned",
      value: metrics.unassigned,
    },
  ]

  return (
    <div className="flex gap-1 overflow-x-auto">
      {items.map((item) => {
        const Icon = item.icon

        return (
          <Button
            key={item.id}
            type="button"
            variant={focus === item.id ? "default" : "outline"}
            size="sm"
            className="h-8 shrink-0 rounded-md px-2 text-xs"
            aria-pressed={focus === item.id}
            onClick={() => onFocusChange(item.id)}
          >
            <Icon
              className="size-3.5"
              weight={focus === item.id ? "bold" : "regular"}
            />
            {item.label}
            <span className="rounded-sm bg-white/18 px-1 text-[10px] dark:bg-zinc-950/20">
              {item.value}
            </span>
          </Button>
        )
      })}
    </div>
  )
}

function BoardFilterMenu({
  activeFilterCount,
  assigneeFilter,
  labelFilter,
  labels,
  members,
  priorityFilter,
  onAssigneeFilterChange,
  onClear,
  onLabelFilterChange,
  onPriorityFilterChange,
}: {
  activeFilterCount: number
  assigneeFilter: string | "ALL"
  labelFilter: string | "ALL"
  labels: string[]
  members: WorkspaceMember[]
  priorityFilter: BoardCardPriority | "ALL"
  onAssigneeFilterChange: (value: string | "ALL") => void
  onClear: () => void
  onLabelFilterChange: (value: string | "ALL") => void
  onPriorityFilterChange: (value: BoardCardPriority | "ALL") => void
}) {
  const priorityOptions: BoardCardPriority[] = ["HIGH", "MEDIUM", "LOW"]

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          type="button"
          variant={activeFilterCount > 0 ? "default" : "outline"}
          size="sm"
          className="h-8 rounded-md px-2 text-xs"
        >
          <FunnelSimple className="size-3.5" weight="bold" />
          Filters
          {activeFilterCount > 0 ? (
            <span className="rounded-sm bg-white/18 px-1 text-[10px]">
              {activeFilterCount}
            </span>
          ) : null}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent
        align="end"
        className="max-h-96 w-72 overflow-y-auto rounded-md"
      >
        <DropdownMenuLabel>Priority</DropdownMenuLabel>
        <FilterMenuItem
          active={priorityFilter === "ALL"}
          onSelect={() => onPriorityFilterChange("ALL")}
        >
          Any priority
        </FilterMenuItem>
        {priorityOptions.map((priority) => (
          <FilterMenuItem
            key={priority}
            active={priorityFilter === priority}
            onSelect={() => onPriorityFilterChange(priority)}
          >
            {priorityLabel(priority)}
          </FilterMenuItem>
        ))}
        <DropdownMenuSeparator />
        <DropdownMenuLabel>Labels</DropdownMenuLabel>
        <FilterMenuItem
          active={labelFilter === "ALL"}
          onSelect={() => onLabelFilterChange("ALL")}
        >
          Any label
        </FilterMenuItem>
        {labels.length > 0 ? (
          labels.map((label) => (
            <FilterMenuItem
              key={label}
              active={labelFilter === label}
              onSelect={() => onLabelFilterChange(label)}
            >
              {label}
            </FilterMenuItem>
          ))
        ) : (
          <DropdownMenuItem disabled>No labels</DropdownMenuItem>
        )}
        <DropdownMenuSeparator />
        <DropdownMenuLabel>Assignee</DropdownMenuLabel>
        <FilterMenuItem
          active={assigneeFilter === "ALL"}
          onSelect={() => onAssigneeFilterChange("ALL")}
        >
          Any assignee
        </FilterMenuItem>
        {members.map((member) => (
          <FilterMenuItem
            key={member.userId}
            active={assigneeFilter === member.userId}
            onSelect={() => onAssigneeFilterChange(member.userId)}
          >
            {member.name ?? member.email ?? "Workspace member"}
          </FilterMenuItem>
        ))}
        {activeFilterCount > 0 ? (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={onClear}>
              <X className="size-4" />
              Clear filters
            </DropdownMenuItem>
          </>
        ) : null}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

function FilterMenuItem({
  active,
  children,
  onSelect,
}: {
  active: boolean
  children: React.ReactNode
  onSelect: () => void
}) {
  return (
    <DropdownMenuItem onSelect={onSelect}>
      {active ? (
        <Check className="size-4" weight="bold" />
      ) : (
        <span className="size-4" />
      )}
      <span className="min-w-0 flex-1 truncate">{children}</span>
    </DropdownMenuItem>
  )
}

function BoardPlanningPanel({
  graph,
  metrics,
  onSelectCard,
  wipAlerts,
}: {
  graph: BoardGraph | null
  metrics: BoardMetrics
  onSelectCard: (cardId: string) => void
  wipAlerts: WipAlert[]
}) {
  const planItems = graph?.planItems ?? []
  const actionableItems = planItems
    .filter((item) => item.isActionable)
    .slice(0, 3)
  const primaryItem = actionableItems[0] ?? null
  const queuedItems = actionableItems.slice(1, 3)
  const blockedPlanCount = planItems.filter((item) => !item.isActionable).length
  const criticalPath = graph?.criticalPath ?? []
  const criticalPathStart = criticalPath[0] ?? null
  const healthStats = [
    {
      icon: CheckCircle,
      label: "Done",
      tone: "teal",
      value: `${metrics.completionRate}%`,
    },
    {
      icon: ClockCountdown,
      label: metrics.overdue > 0 ? "Overdue" : "Due soon",
      tone: metrics.overdue > 0 ? "rose" : "amber",
      value:
        metrics.overdue > 0 ? String(metrics.overdue) : String(metrics.dueSoon),
    },
    {
      icon: WarningCircle,
      label: "Blocked",
      tone: metrics.blocked > 0 ? "rose" : "zinc",
      value: String(metrics.blocked),
    },
    {
      icon: UsersThree,
      label: "No owner",
      tone: metrics.unassigned > 0 ? "sky" : "zinc",
      value: String(metrics.unassigned),
    },
  ] satisfies HealthStatProps[]

  return (
    <div className="grid gap-3 rounded-md border border-zinc-950/10 bg-white p-3 text-xs shadow-xs xl:grid-cols-[minmax(0,1.35fr)_minmax(280px,0.65fr)] dark:border-white/10 dark:bg-white/[0.035]">
      <section className="min-w-0">
        <div className="mb-2 flex items-center justify-between gap-2">
          <div className="min-w-0">
            <p className="text-sm font-semibold">Suggested next step</p>
            <p className="mt-0.5 text-[11px] text-muted-foreground">
              Picked from dates, blockers, priority, and impact.
            </p>
          </div>
          {graph?.isAcyclic === false ? (
            <PlanTag tone="rose">Dependency cycle</PlanTag>
          ) : (
            <PlanTag tone="teal">Up to date</PlanTag>
          )}
        </div>
        {primaryItem ? (
          <div className="grid gap-2">
            <button
              type="button"
              className="group flex min-w-0 items-start gap-3 rounded-md border border-teal-500/20 bg-teal-500/[0.07] p-3 text-left transition-colors hover:bg-teal-500/10 dark:border-teal-300/20"
              onClick={() => onSelectCard(primaryItem.card.id)}
            >
              <span className="grid size-9 shrink-0 place-items-center rounded-md bg-teal-600 text-white shadow-xs dark:bg-teal-300 dark:text-teal-950">
                <Target className="size-4" weight="bold" />
              </span>
              <span className="min-w-0 flex-1">
                <span className="block truncate text-sm font-semibold">
                  {primaryItem.card.title}
                </span>
                <span className="mt-1 block text-[11px] leading-5 text-muted-foreground">
                  {getPlanExplanation(primaryItem)}
                </span>
                <span className="mt-2 flex flex-wrap gap-1.5">
                  {getPlanTags(primaryItem).map((tag) => (
                    <PlanTag key={tag.label} tone={tag.tone}>
                      {tag.label}
                    </PlanTag>
                  ))}
                </span>
              </span>
            </button>
            {queuedItems.length > 0 ? (
              <div className="grid gap-1.5 md:grid-cols-2">
                {queuedItems.map((item, index) => (
                  <PlanItemButton
                    key={item.card.id}
                    item={item}
                    onSelectCard={onSelectCard}
                    rank={index + 2}
                  />
                ))}
              </div>
            ) : null}
          </div>
        ) : (
          <div className="rounded-md border border-dashed border-zinc-950/10 bg-zinc-50 px-3 py-4 text-muted-foreground dark:border-white/10 dark:bg-zinc-950">
            No suggested next step yet. Add dates, priorities, or dependencies
            to help the planner rank work.
          </div>
        )}
      </section>

      <section className="min-w-0 rounded-md border border-zinc-950/[0.06] bg-zinc-50 p-2.5 dark:border-white/10 dark:bg-zinc-950/40">
        <div className="mb-2 flex items-center justify-between gap-2">
          <p className="font-semibold">Board health</p>
          {wipAlerts.length > 0 ? (
            <PlanTag tone="amber">{wipAlerts.length} WIP alert</PlanTag>
          ) : (
            <PlanTag tone="teal">Flow stable</PlanTag>
          )}
        </div>
        <div className="mb-3">
          <div className="mb-1 flex items-center justify-between text-[11px] text-muted-foreground">
            <span>{metrics.completed} completed</span>
            <span>{metrics.active} active</span>
          </div>
          <div className="h-1.5 overflow-hidden rounded-full bg-zinc-950/10 dark:bg-white/10">
            <div
              className="h-full rounded-full bg-teal-600 dark:bg-teal-300"
              style={{ width: `${metrics.completionRate}%` }}
            />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-1.5">
          {healthStats.map((stat) => (
            <HealthStat key={stat.label} {...stat} />
          ))}
        </div>
        {criticalPathStart && criticalPath.length > 1 ? (
          <button
            type="button"
            className="mt-2 flex w-full min-w-0 items-center gap-2 rounded-md border border-sky-500/15 bg-sky-500/[0.07] px-2.5 py-2 text-left transition-colors hover:bg-sky-500/10"
            onClick={() => onSelectCard(criticalPathStart.id)}
          >
            <GitBranch className="size-4 shrink-0 text-sky-700 dark:text-sky-200" />
            <span className="min-w-0 flex-1">
              <span className="block truncate font-medium">
                Main path starts here
              </span>
              <span className="block truncate text-[11px] text-muted-foreground">
                {criticalPathStart.title} · {criticalPath.length} cards
              </span>
            </span>
          </button>
        ) : null}
        {blockedPlanCount > 0 ? (
          <p className="mt-2 text-[11px] leading-5 text-muted-foreground">
            {blockedPlanCount} planned item{blockedPlanCount === 1 ? "" : "s"}{" "}
            waiting on another task.
          </p>
        ) : null}
      </section>
    </div>
  )
}

function PlanItemButton({
  item,
  onSelectCard,
  rank,
}: {
  item: BoardGraphPlanItem
  onSelectCard: (cardId: string) => void
  rank: number
}) {
  return (
    <button
      type="button"
      className="flex h-12 min-w-0 items-center gap-2 rounded-md border border-zinc-950/[0.08] bg-zinc-50 px-2.5 text-left transition-colors hover:bg-zinc-100 dark:border-white/10 dark:bg-zinc-950/70 dark:hover:bg-white/[0.06]"
      onClick={() => onSelectCard(item.card.id)}
    >
      <span className="grid size-6 shrink-0 place-items-center rounded-md bg-zinc-950/5 font-mono text-[11px] font-semibold text-muted-foreground dark:bg-white/10">
        {rank}
      </span>
      <span className="min-w-0 flex-1">
        <span className="block truncate font-medium">{item.card.title}</span>
        <span className="block truncate text-[11px] text-muted-foreground">
          {getShortPlanReason(item)}
        </span>
      </span>
    </button>
  )
}

type PlanTone = "amber" | "rose" | "sky" | "teal" | "zinc"

function PlanTag({
  children,
  tone,
}: {
  children: React.ReactNode
  tone: PlanTone
}) {
  return (
    <span
      className={`inline-flex h-5 items-center rounded-sm px-1.5 text-[11px] font-medium ${getToneClass(
        tone
      )}`}
    >
      {children}
    </span>
  )
}

type HealthStatProps = {
  icon: Icon
  label: string
  tone: PlanTone
  value: string
}

function HealthStat({
  icon: IconComponent,
  label,
  tone,
  value,
}: HealthStatProps) {
  return (
    <div className="flex min-w-0 items-center gap-2 rounded-md border border-zinc-950/[0.06] bg-white px-2 py-2 dark:border-white/10 dark:bg-zinc-950">
      <span
        className={`grid size-6 shrink-0 place-items-center rounded-md ${getToneClass(
          tone
        )}`}
      >
        <IconComponent className="size-3.5" weight="bold" />
      </span>
      <span className="min-w-0">
        <span className="block text-sm leading-none font-semibold">
          {value}
        </span>
        <span className="mt-0.5 block truncate text-[11px] text-muted-foreground">
          {label}
        </span>
      </span>
    </div>
  )
}

function getPlanExplanation(item: BoardGraphPlanItem) {
  const unlockReason = getUnlockReason(item)
  const dueReason = getDueReason(item)
  const isCritical = isCriticalPlanItem(item)

  if (dueReason?.startsWith("Overdue")) {
    return "Do this first: it is overdue and ready to move without waiting on another task."
  }

  if (isCritical && unlockReason) {
    return `Do this first: it keeps the main path moving and ${formatUnlockReason(
      unlockReason
    )}.`
  }

  if (unlockReason) {
    return `Do this first: it is ready and ${formatUnlockReason(unlockReason)}.`
  }

  if (isCritical) {
    return "Do this first: it is the cleanest ready task on the main path."
  }

  if (dueReason) {
    return `Do this first: it is ready and ${dueReason.toLowerCase()}.`
  }

  return "Do this first: it is ready and has the strongest impact right now."
}

function getPlanTags(item: BoardGraphPlanItem) {
  const tags: { label: string; tone: PlanTone }[] = [
    { label: "Ready to start", tone: "teal" },
  ]
  const dueReason = getDueReason(item)
  const unlockReason = getUnlockReason(item)

  if (dueReason) {
    tags.push({
      label: dueReason,
      tone: dueReason.startsWith("Overdue") ? "rose" : "amber",
    })
  }

  if (isCriticalPlanItem(item)) {
    tags.push({ label: "Main path", tone: "sky" })
  }

  if (unlockReason) {
    tags.push({ label: toUnlockTag(unlockReason), tone: "teal" })
  }

  if (item.reasons.includes("High priority")) {
    tags.push({ label: "High priority", tone: "rose" })
  }

  return tags.slice(0, 4)
}

function getShortPlanReason(item: BoardGraphPlanItem) {
  const unlockReason = getUnlockReason(item)
  const dueReason = getDueReason(item)

  if (unlockReason) {
    return toUnlockTag(unlockReason)
  }

  if (isCriticalPlanItem(item)) {
    return "Keeps the main path moving"
  }

  if (dueReason) {
    return dueReason
  }

  if (item.reasons.includes("High priority")) {
    return "High priority"
  }

  return "Ready to start"
}

function getUnlockReason(item: BoardGraphPlanItem) {
  return item.reasons.find((reason) => reason.startsWith("Unlocks"))
}

function getDueReason(item: BoardGraphPlanItem) {
  return item.reasons.find(
    (reason) => reason.startsWith("Due") || reason.startsWith("Overdue")
  )
}

function isCriticalPlanItem(item: BoardGraphPlanItem) {
  return item.card.isCriticalPath || item.reasons.includes("Critical path")
}

function toUnlockTag(reason: string) {
  const match = reason.match(/^Unlocks (\d+)/)

  if (!match) {
    return reason
  }

  const count = Number(match[1])

  return `Unblocks ${count} task${count === 1 ? "" : "s"}`
}

function formatUnlockReason(reason: string) {
  const tag = toUnlockTag(reason)

  return tag.charAt(0).toLowerCase() + tag.slice(1)
}

function getToneClass(tone: PlanTone) {
  if (tone === "rose") {
    return "bg-rose-500/10 text-rose-700 dark:text-rose-200"
  }

  if (tone === "amber") {
    return "bg-amber-500/10 text-amber-700 dark:text-amber-200"
  }

  if (tone === "teal") {
    return "bg-teal-500/10 text-teal-700 dark:text-teal-200"
  }

  if (tone === "sky") {
    return "bg-sky-500/10 text-sky-700 dark:text-sky-200"
  }

  return "bg-zinc-950/5 text-muted-foreground dark:bg-white/10"
}

function priorityLabel(priority: BoardCardPriority) {
  return (
    boardCardPriorities.find((item) => item.value === priority)?.label ??
    priority
  )
}

function BoardSetupOnboarding({
  isCreating,
  onCreate,
  projectName,
}: {
  isCreating: boolean
  onCreate: (template: BoardTemplate, customListTitles?: string[]) => void
  projectName: string
}) {
  return (
    <div className="grid gap-3 rounded-md border border-zinc-950/10 bg-zinc-50 p-3 shadow-xs dark:border-white/10 dark:bg-zinc-950/70">
      <div className="dark:bg-white/0.045 rounded-md border border-zinc-950/10 bg-white p-4 dark:border-white/10">
        <div className="flex items-start gap-3">
          <span className="grid size-10 shrink-0 place-items-center rounded-md bg-zinc-950 text-white dark:bg-white dark:text-zinc-950">
            <Kanban className="size-5" weight="bold" />
          </span>
          <div className="min-w-0">
            <p className="text-sm font-semibold">Set up {projectName}</p>
            <p className="mt-1 max-w-2xl text-sm leading-6 text-muted-foreground">
              Pick a starting workflow for this project board. The project
              already exists; this step only creates its first board structure.
            </p>
          </div>
        </div>
      </div>
      <TemplateChooser isCreating={isCreating} onCreate={onCreate} />
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
