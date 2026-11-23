import { useMemo } from "react"

import * as staffingApi from "@/features/board/api/milestone-staffing-api"
import type {
  StaffChange,
  StaffingRequest,
} from "@/features/board/api/milestone-staffing-api"
import type { Board, BoardMilestoneStaffing } from "@/types/task"

export type MilestoneStaffingActions = {
  propose: (
    milestoneId: string,
    request: StaffingRequest
  ) => Promise<BoardMilestoneStaffing>
  evaluate: (
    milestoneId: string,
    changes: StaffChange[]
  ) => Promise<BoardMilestoneStaffing>
  apply: (milestoneId: string, changes: StaffChange[]) => Promise<void>
}

type UseMilestoneStaffingOptions = {
  accessToken: string | null
  workspaceId: string
  projectId: string
  boardId: string | undefined
  setBoard: (board: Board) => void
}

/** The board's staffing actions, or null until there is a board to ask about. */
export function useMilestoneStaffing({
  accessToken,
  workspaceId,
  projectId,
  boardId,
  setBoard,
}: UseMilestoneStaffingOptions): MilestoneStaffingActions | null {
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
      propose: (milestoneId, request) =>
        staffingApi.proposeMilestoneStaffing(target(milestoneId), request),
      evaluate: (milestoneId, changes) =>
        staffingApi.evaluateMilestoneStaffing(target(milestoneId), changes),
      // The new board reloads the milestones' forecasts.
      apply: async (milestoneId, changes) =>
        setBoard(
          await staffingApi.applyMilestoneStaffing(target(milestoneId), changes)
        ),
    }
  }, [accessToken, boardId, projectId, setBoard, workspaceId])
}
