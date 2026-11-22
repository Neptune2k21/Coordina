import { describe, expect, it } from "vitest"

import {
  canProposeScope,
  describeAlsoRemoved,
  describeChance,
} from "@/features/board/model/milestone-scope"
import type { BoardMilestoneCut } from "@/types/task"

const cut = (...titles: string[]): BoardMilestoneCut => ({
  cardId: "pdf",
  title: "Export PDF",
  alsoRemoved: titles.map((title) => ({ cardId: title, title })),
})

describe("milestone scope", () => {
  it("offers a cut to milestones at risk or late", () => {
    expect(canProposeScope("AT_RISK")).toBe(true)
    expect(canProposeScope("LATE")).toBe(true)
    expect(canProposeScope("ON_TRACK")).toBe(false)
    expect(canProposeScope("OVERDUE")).toBe(false)
    expect(canProposeScope("COMPLETED")).toBe(false)
  })

  it("rounds chances to whole percentages", () => {
    expect(describeChance(0)).toBe("0%")
    expect(describeChance(0.8525)).toBe("85%")
    expect(describeChance(1)).toBe("100%")
  })

  it("names the cards that leave with a cut", () => {
    expect(describeAlsoRemoved(cut())).toBeNull()
    expect(describeAlsoRemoved(cut("Templates"))).toBe("Also removes Templates")
    expect(describeAlsoRemoved(cut("Templates", "Fonts"))).toBe(
      "Also removes Templates and Fonts"
    )
    expect(describeAlsoRemoved(cut("Templates", "Fonts", "Preview"))).toBe(
      "Also removes Templates, Fonts and 1 more"
    )
  })
})
