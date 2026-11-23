import { shortDate, userLabel } from "@/features/board/model/board-format"
import { daysUntilDateKey } from "@/features/board/model/dates"
import type {
  BoardMilestoneStaffChange,
  BoardMilestoneStaffing,
  BoardMilestoneStatus,
} from "@/types/task"

/** Any milestone with work left can be planned again. */
export function canOptimizeStaffing(status: BoardMilestoneStatus) {
  return status !== "COMPLETED"
}

function days(count: number) {
  return `${count} ${count === 1 ? "day" : "days"}`
}

/** How much sooner the plan ships at 85%, the date the milestone commits to. */
export function describeGain(staffing: BoardMilestoneStaffing) {
  const gain = daysUntilDateKey(
    staffing.commitDateBefore,
    staffing.commitDateAfter
  )

  if (gain > 0) {
    return `Ships ${days(gain)} sooner`
  }

  return gain < 0 ? `Ships ${days(-gain)} later` : "Ships on the same date"
}

/** Who the card goes to, and from whom. */
export function describeChange(change: BoardMilestoneStaffChange) {
  const from = change.from.map(userLabel)
  const to = userLabel(change.to)

  return from.length === 0
    ? `Give ${change.title} to ${to}`
    : `Give ${change.title} to ${to} instead of ${from.join(" and ")}`
}

/** Why the change helps: when the card starts, and what its new person has done. */
export function describeReason(change: BoardMilestoneStaffChange) {
  const to = userLabel(change.to)
  const reasons = [
    daysUntilDateKey(change.startBefore, change.startAfter) > 0
      ? `${to} can start it ${shortDate(change.startAfter)} instead of ${shortDate(change.startBefore)}`
      : change.from.length > 0
        ? `Frees ${change.from.map(userLabel).join(" and ")} for other work`
        : `${to} can start it ${shortDate(change.startAfter)}`,
  ]

  if (change.matchingLabels.length > 0) {
    reasons.push(
      `has done ${change.matchingLabels.map((label) => `#${label}`).join(", ")} cards`
    )
  }

  return reasons.join("; ")
}

/** The changes chosen, as the API takes them. */
export function chosenChanges(
  staffing: BoardMilestoneStaffing,
  chosen: string[]
) {
  return staffing.changes
    .filter((change) => chosen.includes(change.cardId))
    .map((change) => ({ cardId: change.cardId, userId: change.to.userId }))
}
