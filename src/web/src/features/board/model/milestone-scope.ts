import type { BoardMilestoneCut, BoardMilestoneStatus } from "@/types/task"

/** A milestone likely late, or likely on time but not safely, may be saved by cutting. */
export function canProposeScope(status: BoardMilestoneStatus) {
  return status === "AT_RISK" || status === "LATE"
}

/** A chance from 0 to 1, as a whole percentage. */
export function describeChance(chance: number) {
  return `${Math.round(chance * 100)}%`
}

/** The cards that leave with a cut, or null when none do. */
export function describeAlsoRemoved(cut: BoardMilestoneCut) {
  const titles = cut.alsoRemoved.map((card) => card.title)

  if (titles.length === 0) {
    return null
  }

  if (titles.length <= 2) {
    return `Also removes ${titles.join(" and ")}`
  }

  return `Also removes ${titles.slice(0, 2).join(", ")} and ${titles.length - 2} more`
}
