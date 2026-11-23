import type { MilestoneScopeTarget } from "@/features/board/api/milestone-scope-api"
import { apiRequest } from "@/lib/api"
import type { Board, BoardMilestoneStaffing } from "@/types/task"

/** Who the proposal may give cards to, and whether assigned cards may move. */
export type StaffingRequest = {
  people?: string[]
  allowReassignment: boolean
}

/** A card to give to one person, who becomes its only assignee. */
export type StaffChange = {
  cardId: string
  userId: string
}

function milestoneRequest<T>(
  target: MilestoneScopeTarget,
  action: "staffing-proposal" | "staffing-evaluation" | "staffing",
  body: object
) {
  const { accessToken, workspaceId, projectId, boardId, milestoneId } = target

  return apiRequest<T>(
    `/workspaces/${workspaceId}/projects/${projectId}/boards/${boardId}/milestones/${milestoneId}/${action}`,
    {
      accessToken,
      method: "POST",
      body: JSON.stringify(body),
      errorMessage: "Milestone plan request failed.",
    }
  )
}

/** Who should take which card so the milestone ships soonest; changes nothing. */
export function proposeMilestoneStaffing(
  target: MilestoneScopeTarget,
  request: StaffingRequest
) {
  return milestoneRequest<BoardMilestoneStaffing>(
    target,
    "staffing-proposal",
    request
  )
}

/** What giving the chosen cards to the chosen people would buy; changes nothing. */
export function evaluateMilestoneStaffing(
  target: MilestoneScopeTarget,
  changes: StaffChange[]
) {
  return milestoneRequest<BoardMilestoneStaffing>(
    target,
    "staffing-evaluation",
    { changes }
  )
}

/** Gives each chosen card to its person and returns the board. */
export function applyMilestoneStaffing(
  target: MilestoneScopeTarget,
  changes: StaffChange[]
) {
  return milestoneRequest<Board>(target, "staffing", { changes })
}
