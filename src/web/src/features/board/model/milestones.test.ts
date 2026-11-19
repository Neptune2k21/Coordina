import { describe, expect, it } from "vitest"

import {
  describeSlip,
  describeUnsized,
  orderMilestones,
} from "@/features/board/model/milestones"
import type { BoardMilestone } from "@/types/task"

function milestone(overrides: Partial<BoardMilestone> = {}): BoardMilestone {
  return {
    cardId: "launch",
    title: "Launch v1",
    targetDate: "2027-03-12",
    status: "LATE",
    forecastDate: "2027-03-18",
    commitDate: "2027-03-24",
    slipDays: 6,
    openCardCount: 12,
    unsizedCardCount: 0,
    drivers: [],
    ...overrides,
  }
}

describe("describeSlip", () => {
  it("says how far the forecast lands from the target", () => {
    expect(describeSlip(6)).toBe("6 days late")
    expect(describeSlip(1)).toBe("1 day late")
    expect(describeSlip(0)).toBe("On the target date")
    expect(describeSlip(-3)).toBe("3 days early")
    expect(describeSlip(null)).toBeNull()
  })
})

describe("describeUnsized", () => {
  it("warns only when cards have no size", () => {
    expect(describeUnsized(milestone())).toBeNull()
    expect(describeUnsized(milestone({ unsizedCardCount: 1 }))).toBe(
      "1 card has no size, low confidence"
    )
    expect(describeUnsized(milestone({ unsizedCardCount: 7 }))).toBe(
      "7 cards have no size, low confidence"
    )
  })
})

describe("orderMilestones", () => {
  it("puts completed milestones after the open ones", () => {
    const shipped = milestone({ cardId: "beta", status: "COMPLETED" })
    const late = milestone({ cardId: "launch" })
    const onTrack = milestone({ cardId: "v2", status: "ON_TRACK" })

    expect(
      orderMilestones([shipped, late, onTrack]).map((item) => item.cardId)
    ).toEqual(["launch", "v2", "beta"])
  })
})
