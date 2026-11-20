import type { BoardMilestone, BoardMilestoneStatus } from "@/types/task"

type BoardMilestoneDriver = BoardMilestone["drivers"][number]

export const milestoneStatusLabels: Record<BoardMilestoneStatus, string> = {
  ON_TRACK: "On track",
  AT_RISK: "At risk",
  LATE: "Late",
  OVERDUE: "Overdue",
  COMPLETED: "Completed",
}

export function milestoneStatusClass(status: BoardMilestoneStatus) {
  switch (status) {
    case "ON_TRACK":
      return "bg-teal-500/10 text-teal-700 dark:text-teal-200"
    case "AT_RISK":
      return "bg-amber-500/10 text-amber-700 dark:text-amber-200"
    case "LATE":
    case "OVERDUE":
      return "bg-rose-500/10 text-rose-700 dark:text-rose-200"
    case "COMPLETED":
      return "bg-zinc-950/5 text-muted-foreground dark:bg-white/10"
  }
}

/** How far the likely date lands from the target, or null without a forecast. */
export function describeSlip(slipDays: number | null) {
  if (slipDays === null) {
    return null
  }

  if (slipDays === 0) {
    return "On the target date"
  }

  const days = Math.abs(slipDays)
  const unit = days === 1 ? "day" : "days"

  return slipDays > 0 ? `${days} ${unit} late` : `${days} ${unit} early`
}

/** Unsized cards are planned as M, so the forecast says how much it guessed. */
export function describeUnsized(milestone: BoardMilestone) {
  const count = milestone.unsizedCardCount

  if (count === 0) {
    return null
  }

  return count === 1
    ? "1 card has no size, low confidence"
    : `${count} cards have no size, low confidence`
}

/** Open milestones first, soonest target first; completed ones after them. */
export function orderMilestones(milestones: BoardMilestone[]) {
  return [
    ...milestones.filter((milestone) => milestone.status !== "COMPLETED"),
    ...milestones.filter((milestone) => milestone.status === "COMPLETED"),
  ]
}

/** The square of the sensitivity is the share of the date's spread the card's own uncertainty explains. */
export function describeDriver(driver: BoardMilestoneDriver) {
  const sets = Math.round(driver.criticality * 100)
  const explains = Math.round(Math.max(0, driver.sensitivity) ** 2 * 100)

  return `Sets the date in ${sets}% of runs; its size explains ${explains}% of the spread`
}
