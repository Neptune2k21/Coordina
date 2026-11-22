import { useMemo } from "react"

import * as scopeApi from "@/features/board/api/milestone-scope-api"
import type { Board, BoardMilestoneScope } from "@/types/task"

export type MilestoneScopeActions = {
  propose: (milestoneId: string) => Promise<BoardMilestoneScope>
  evaluate: (
    milestoneId: string,
    cardIds: string[]
  ) => Promise<BoardMilestoneScope>
  cut: (milestoneId: string, cardIds: string[]) => Promise<void>
}

type UseMilestoneScopeOptions = {
  accessToken: string | null
  workspaceId: string
  projectId: string
  boardId: string | undefined
  setBoard: (board: Board) => void
}

/** The board's scope actions, or null until there is a board to ask about. */
export function useMilestoneScope({
  accessToken,
  workspaceId,
  projectId,
  boardId,
  setBoard,
}: UseMilestoneScopeOptions): MilestoneScopeActions | null {
  return useMemo(() => {
    if (!accessToken || !boardId) {
      return null
    }

    const target = (milestoneId: string) => ({
      accessToken,
      workspaceId,
      projectId,
      boardId,
      milestoneId,
    })

    return {
      propose: (milestoneId) =>
        scopeApi.proposeMilestoneScope(target(milestoneId)),
      evaluate: (milestoneId, cardIds) =>
        scopeApi.evaluateMilestoneScope(target(milestoneId), cardIds),
      // The new board reloads the milestones' forecasts.
      cut: async (milestoneId, cardIds) =>
        setBoard(
          await scopeApi.cutMilestoneScope(target(milestoneId), cardIds)
        ),
    }
  }, [accessToken, boardId, projectId, setBoard, workspaceId])
}
