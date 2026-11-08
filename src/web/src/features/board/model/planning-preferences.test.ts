import { describe, expect, it } from "vitest"

import {
  defaultPlanningPreferences,
  formatMinutes,
  planningQuery,
} from "@/features/board/model/planning-preferences"

describe("planningQuery", () => {
  it("omits default preferences", () => {
    expect(planningQuery(defaultPlanningPreferences)).toBe("")
  })

  it("serializes focus time and context", () => {
    expect(
      planningQuery({
        ...defaultPlanningPreferences,
        availableMinutes: 60,
        context: null,
      })
    ).toBe("?availableMinutes=60")
    expect(
      planningQuery({
        ...defaultPlanningPreferences,
        availableMinutes: 0,
        context: "front end",
      })
    ).toBe("?context=front+end")
    expect(
      planningQuery({
        ...defaultPlanningPreferences,
        availableMinutes: 30,
        context: "backend",
      })
    ).toBe("?availableMinutes=30&context=backend")
  })

  it("sends the assignment options only when they are turned off", () => {
    expect(
      planningQuery({
        ...defaultPlanningPreferences,
        preferAssigned: false,
        includeUnassigned: false,
      })
    ).toBe("?preferAssigned=false&includeUnassigned=false")
  })
})

describe("formatMinutes", () => {
  it("formats minutes, hours, and mixed durations", () => {
    expect(formatMinutes(15)).toBe("15m")
    expect(formatMinutes(60)).toBe("1h")
    expect(formatMinutes(90)).toBe("1h 30m")
    expect(formatMinutes(480)).toBe("8h")
  })
})
