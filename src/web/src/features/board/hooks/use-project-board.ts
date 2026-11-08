import { useCallback, useEffect, useMemo, useState } from "react"

import { useAuth } from "@/features/auth"
import { getPlanningPreferences } from "@/features/board/api/board-api"
import { useBoardFilters } from "@/features/board/hooks/use-board-filters"
import { useBoardMutations } from "@/features/board/hooks/use-board-mutations"
import {
  useBoardResource,
  useCardDependencyAnalysis,
} from "@/features/board/hooks/use-board-resource"
import {
  countBoardCards,
  countUserCards,
  getBoardLabels,
  getBoardMetrics,
  getWipAlerts,
} from "@/features/board/model/board-selectors"
import {
  defaultPlanningPreferences,
  type PlanningPreferences,
} from "@/features/board/model/planning-preferences"
import { ApiError } from "@/lib/api"
import type { Project } from "@/types/project"
import type {
  BoardCard,
  BoardCardInput,
  BoardCardSubtask,
  BoardCreationInput,
  BoardList,
  BoardTaskStatus,
} from "@/types/task"
import type { Workspace } from "@/types/workspace"

export function useProjectBoard(project: Project, workspace: Workspace) {
  const { session, signOut } = useAuth()
  const accessToken = session?.accessToken ?? null
  const sessionUserId = session?.user.id ?? null
  const isReadOnly = project.status === "COMPLETED"

  const [error, setError] = useState<string | null>(null)
  const [quickAddListId, setQuickAddListId] = useState<string | null>(null)
  const [isAddingList, setIsAddingList] = useState(false)
  const [draggingCardId, setDraggingCardId] = useState<string | null>(null)
  const [selectedCardId, setSelectedCardId] = useState<string | null>(null)
  const [isPlanningPanelOpen, setIsPlanningPanelOpen] = useState(false)
  const [planningPreferences, setPlanningPreferences] =
    useState<PlanningPreferences>(defaultPlanningPreferences)

  const reportError = useCallback(
    (requestError: unknown, fallback: string) => {
      if (!(requestError instanceof ApiError)) {
        setError(fallback)
        return
      }

      setError(requestError.message)

      if (requestError.status === 401) {
        signOut()
      }
    },
    [signOut]
  )
  const clearError = useCallback(() => setError(null), [])

  const scope = {
    accessToken,
    workspaceId: workspace.id,
    projectId: project.id,
  }
  const resource = useBoardResource({
    ...scope,
    onError: reportError,
    planning: planningPreferences,
  })
  const { board } = resource
  const mutations = useBoardMutations({
    ...scope,
    board,
    setBoard: resource.setBoard,
    reload: resource.reload,
    isReadOnly,
    onError: reportError,
    clearError,
  })
  const dependency = useCardDependencyAnalysis({
    ...scope,
    board,
    cardId: selectedCardId,
  })
  const filters = useBoardFilters(board, sessionUserId)
  const { clearFilters } = filters

  useEffect(() => {
    queueMicrotask(() => {
      setError(null)
      setSelectedCardId(null)
      setPlanningPreferences(defaultPlanningPreferences)
      clearFilters()
    })
  }, [clearFilters, project.id, workspace.id])

  // The board starts from the preferences saved in Settings; changes made in
  // the planning panel only last for this visit.
  const boardId = board?.id
  useEffect(() => {
    if (!accessToken || !boardId) {
      return
    }

    let isCurrent = true

    getPlanningPreferences(accessToken, workspace.id, project.id, boardId)
      .then((saved) => {
        if (isCurrent) {
          setPlanningPreferences(saved)
        }
      })
      // Without them the board keeps working with the defaults.
      .catch(() => undefined)

    return () => {
      isCurrent = false
    }
  }, [accessToken, boardId, project.id, workspace.id])

  // A link from My work opens its card: /app/projects/{id}?card={cardId}.
  useEffect(() => {
    const linkedCardId = new URLSearchParams(window.location.search).get("card")

    if (!board || !linkedCardId) {
      return
    }

    if (
      board.lists.some((list) =>
        list.cards.some((card) => card.id === linkedCardId)
      )
    ) {
      queueMicrotask(() => setSelectedCardId(linkedCardId))
    }

    window.history.replaceState({}, "", window.location.pathname)
  }, [board])

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
  const myCardCount = useMemo(
    () => countUserCards(board, sessionUserId),
    [board, sessionUserId]
  )
  const canDeleteCards =
    !isReadOnly &&
    (workspace.role === "OWNER" || project.projectOwnerId === sessionUserId)
  const canShowPlanningPanel = cardCount > 0
  const handleSelectCard = useCallback((card: BoardCard) => {
    setSelectedCardId(card.id)
  }, [])
  const isPlanningPanelVisible = canShowPlanningPanel && isPlanningPanelOpen

  async function handleCreateBoard(input: BoardCreationInput) {
    await mutations.createBoard(`${project.name} Board`, input)
  }

  async function handleCreateList(title: string, taskStatus: BoardTaskStatus) {
    if (await mutations.createList(title, taskStatus)) {
      setIsAddingList(false)
    }
  }

  async function handleRenameList(list: BoardList, title: string) {
    await mutations.renameList(list, title)
  }

  async function handleQuickAdd(list: BoardList, title: string) {
    if (await mutations.quickAddCard(list.id, title)) {
      setQuickAddListId(null)
    }
  }

  async function handleMoveCard(cardId: string, listId: string) {
    try {
      await mutations.moveCard(cardId, listId)
    } finally {
      setDraggingCardId(null)
    }
  }

  async function handleSaveCard(card: BoardCard, input: BoardCardInput) {
    await mutations.saveCard(card, input)
  }

  async function handleDeleteCard(card: BoardCard) {
    if (canDeleteCards && (await mutations.deleteCard(card))) {
      setSelectedCardId(null)
    }
  }

  async function handleDuplicateCard(card: BoardCard) {
    await mutations.duplicateCard(card)
  }

  async function handleUpdateSubtask(
    card: BoardCard,
    subtask: BoardCardSubtask,
    input: { title?: string; isCompleted?: boolean }
  ) {
    await mutations.updateSubtask(card, subtask, input)
  }

  return {
    board,
    boardGraph: resource.boardGraph,
    boardLabels,
    canDeleteCards,
    canShowPlanningPanel,
    cardCount,
    dependencyAnalysis: dependency.analysis,
    dependencyCandidates,
    draggingCardId,
    error,
    filters,
    handleAddComment: mutations.addComment,
    handleAddDependency: mutations.addDependency,
    handleCreateBoard,
    handleCreateList,
    handleCreateSubtask: mutations.createSubtask,
    handleDeleteCard,
    handleDeleteDependency: mutations.deleteDependency,
    handleDeleteSubtask: mutations.deleteSubtask,
    handleDuplicateCard,
    handleMoveCard,
    handleQuickAdd,
    handleRenameList,
    handleSaveCard,
    handleSelectCard,
    handleUpdateSubtask,
    isAddingList,
    isCreatingBoard: mutations.isCreatingBoard,
    isLoading: resource.isLoading,
    isLoadingDependencyAnalysis: dependency.isLoading,
    isMutating: mutations.isMutating,
    isPlanningPanelOpen,
    isPlanningPanelVisible,
    isReadOnly,
    members: resource.members,
    metrics,
    myCardCount,
    notice: mutations.notice,
    planningPreferences,
    quickAddListId,
    selectedCard,
    setDraggingCardId,
    setIsAddingList,
    setIsPlanningPanelOpen,
    setPlanningPreferences,
    setQuickAddListId,
    setSelectedCardId,
    wipAlerts,
  }
}
