import { describe, expect, it } from "vitest"

import {
  defaultPlanningPreferences,
  planningQuery,
} from "@/features/board/model/planning-preferences"

describe("planningQuery", () => {
  it("omits default preferences", () => {
    expect(planningQuery(defaultPlanningPreferences)).toBe("")
  })

  it("serializes the focus slot and context", () => {
    expect(
      planningQuery({
        ...defaultPlanningPreferences,
        maxSize: "M",
        context: null,
      })
    ).toBe("?maxSize=M")
    expect(
      planningQuery({
        ...defaultPlanningPreferences,
        maxSize: null,
        context: "front end",
      })
    ).toBe("?context=front+end")
    expect(
      planningQuery({
        ...defaultPlanningPreferences,
        maxSize: "S",
        context: "backend",
      })
    ).toBe("?maxSize=S&context=backend")
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
