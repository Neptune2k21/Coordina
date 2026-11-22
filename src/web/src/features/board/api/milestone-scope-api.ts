import { apiRequest } from "@/lib/api"
import type { Board, BoardMilestoneScope } from "@/types/task"

export type MilestoneScopeTarget = {
  accessToken: string
  workspaceId: string
  projectId: string
  boardId: string
  milestoneId: string
}

function milestoneRequest<T>(
  target: MilestoneScopeTarget,
  action: "scope-proposal" | "scope-evaluation" | "scope-cut",
  body: object
) {
  const { accessToken, workspaceId, projectId, boardId, milestoneId } = target

  return apiRequest<T>(
    `/workspaces/${workspaceId}/projects/${projectId}/boards/${boardId}/milestones/${milestoneId}/${action}`,
    {
      accessToken,
      method: "POST",
      body: JSON.stringify(body),
      errorMessage: "Milestone scope request failed.",
    }
  )
}

/** Cards to take out so the milestone ships by its target; changes nothing. */
export function proposeMilestoneScope(
  target: MilestoneScopeTarget,
  keep: string[] = []
) {
  return milestoneRequest<BoardMilestoneScope>(target, "scope-proposal", {
    keep,
  })
}

/** What taking the chosen cards out would buy; changes nothing. */
export function evaluateMilestoneScope(
  target: MilestoneScopeTarget,
  cardIds: string[]
) {
  return milestoneRequest<BoardMilestoneScope>(target, "scope-evaluation", {
    cardIds,
  })
}

/** Takes the chosen cards out of the milestone and returns the board. */
export function cutMilestoneScope(
  target: MilestoneScopeTarget,
  cardIds: string[]
) {
  return milestoneRequest<Board>(target, "scope-cut", { cardIds })
}
