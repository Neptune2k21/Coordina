import { describe, expect, it } from "vitest"

import {
  canOptimizeStaffing,
  chosenChanges,
  describeChange,
  describeGain,
  describeReason,
} from "@/features/board/model/milestone-staffing"
import type {
  BoardMilestoneStaffChange,
  BoardMilestoneStaffing,
} from "@/types/task"

const ada = { userId: "ada", name: "Ada", email: "ada@example.com" }
const grace = { userId: "grace", name: "Grace", email: "grace@example.com" }

const payment: BoardMilestoneStaffChange = {
  cardId: "payment",
  title: "Payment",
  from: [ada],
  to: grace,
  startBefore: "2027-03-09",
  startAfter: "2027-03-04",
  matchingLabels: [],
}

const staffing: BoardMilestoneStaffing = {
  milestoneId: "launch",
  targetDate: "2027-03-12",
  forecastDateBefore: "2027-03-11",
  commitDateBefore: "2027-03-14",
  forecastDateAfter: "2027-03-08",
  commitDateAfter: "2027-03-10",
  othersDelayDays: 0,
  people: [ada, grace],
  changes: [
    payment,
    { ...payment, cardId: "docs", title: "Docs", from: [], to: ada },
  ],
}

describe("milestone staffing", () => {
  it("offers a plan for every milestone with work left", () => {
    expect(canOptimizeStaffing("LATE")).toBe(true)
    expect(canOptimizeStaffing("ON_TRACK")).toBe(true)
    expect(canOptimizeStaffing("COMPLETED")).toBe(false)
  })

  it("counts the days the 85% date moves", () => {
    expect(describeGain(staffing)).toBe("Ships 4 days sooner")
    expect(describeGain({ ...staffing, commitDateAfter: "2027-03-13" })).toBe(
      "Ships 1 day sooner"
    )
    expect(describeGain({ ...staffing, commitDateAfter: "2027-03-14" })).toBe(
      "Ships on the same date"
    )
  })

  it("names who a card goes to and who it leaves", () => {
    expect(describeChange(payment)).toBe("Give Payment to Grace instead of Ada")
    expect(describeChange({ ...payment, from: [] })).toBe(
      "Give Payment to Grace"
    )
  })

  it("explains when the card starts and what its person has done", () => {
    expect(describeReason(payment)).toMatch(
      /^Grace can start it .+ instead of /
    )
    expect(
      describeReason({
        ...payment,
        startAfter: payment.startBefore,
        matchingLabels: ["backend"],
      })
    ).toBe("Frees Ada for other work; has done #backend cards")
  })

  it("sends the chosen changes as card and person", () => {
    expect(chosenChanges(staffing, ["docs"])).toEqual([
      { cardId: "docs", userId: "ada" },
    ])
  })
})
