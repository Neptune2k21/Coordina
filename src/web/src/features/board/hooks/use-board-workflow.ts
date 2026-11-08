import { useCallback, useEffect, useMemo, useState } from "react"

import * as boardApi from "@/features/board/api/board-api"
import { ApiError } from "@/lib/api"
import type { Board, BoardList, BoardTaskStatus } from "@/types/task"

type BoardWorkflowScope = {
  accessToken: string
  workspaceId: string
  projectId: string
}

/**
 * The board's lists for the Settings tab: loads the board, then renames,
 * reorders, changes the status of, adds and deletes lists. Every change
 * returns the whole board, which replaces the one shown.
 */
export function useBoardWorkflow({
  accessToken,
  workspaceId,
  projectId,
}: BoardWorkflowScope) {
  const [board, setBoard] = useState<Board | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let isCurrent = true

    queueMicrotask(() => {
      setIsLoading(true)
      setError(null)
    })
    boardApi
      .getDefaultBoard(accessToken, workspaceId, projectId)
      .then((loaded) => isCurrent && setBoard(loaded))
      .catch((requestError: unknown) => {
        if (!isCurrent) {
          return
        }

        setBoard(null)

        // A project without a board has nothing to configure yet.
        if (!(
          requestError instanceof ApiError && requestError.status === 404
        )) {
          setError(messageOf(requestError, "Board could not be loaded."))
        }
      })
      .finally(() => isCurrent && setIsLoading(false))

    return () => {
      isCurrent = false
    }
  }, [accessToken, projectId, workspaceId])

  const lists = useMemo(
    () =>
      board ? [...board.lists].sort((a, b) => a.position - b.position) : [],
    [board]
  )

  const run = useCallback(
    async (change: (boardId: string) => Promise<Board>, fallback: string) => {
      if (!board) {
        return false
      }

      setIsSaving(true)
      setError(null)

      try {
        setBoard(await change(board.id))
        return true
      } catch (requestError) {
        setError(messageOf(requestError, fallback))
        return false
      } finally {
        setIsSaving(false)
      }
    },
    [board]
  )

  const update = useCallback(
    (list: BoardList, changes: boardApi.BoardListChanges) =>
      run(
        (boardId) =>
          boardApi.updateList(
            accessToken,
            workspaceId,
            projectId,
            boardId,
            list.id,
            changes
          ),
        "List could not be updated."
      ),
    [accessToken, projectId, run, workspaceId]
  )

  return {
    board,
    lists,
    isLoading,
    isSaving,
    error,
    rename: (list: BoardList, title: string) =>
      title.trim() && title.trim() !== list.title
        ? update(list, { title: title.trim() })
        : Promise.resolve(false),
    changeStatus: (list: BoardList, taskStatus: BoardTaskStatus) =>
      update(list, { taskStatus }),
    move: (list: BoardList, offset: -1 | 1) =>
      update(list, { position: lists.indexOf(list) + offset }),
    add: (title: string, taskStatus: BoardTaskStatus) =>
      run(
        (boardId) =>
          boardApi.createList(
            accessToken,
            workspaceId,
            projectId,
            boardId,
            title.trim(),
            taskStatus
          ),
        "List could not be added."
      ),
    remove: (list: BoardList, moveCardsTo: string | null) =>
      run(
        (boardId) =>
          boardApi.deleteList(
            accessToken,
            workspaceId,
            projectId,
            boardId,
            list.id,
            moveCardsTo
          ),
        "List could not be deleted."
      ),
  }
}

// A validation problem names the field that is wrong; show that first.
function messageOf(requestError: unknown, fallback: string) {
  if (!(requestError instanceof ApiError)) {
    return fallback
  }

  return (
    Object.values(requestError.errors ?? {})[0]?.[0] ?? requestError.message
  )
}
