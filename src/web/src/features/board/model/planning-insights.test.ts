import { describe, expect, it } from "vitest"

import { makePlanItem } from "@/features/board/model/board-test-fixtures"
import {
  getPlanTags,
  pickPlanFocus,
} from "@/features/board/model/planning-insights"
import { defaultPlanningPreferences } from "@/features/board/model/planning-preferences"

describe("pickPlanFocus", () => {
  it("skips blocked items and queues the next two ready ones", () => {
    const focus = pickPlanFocus(
      [
        makePlanItem({ id: "blocked", isActionable: false }),
        makePlanItem({ id: "a" }),
        makePlanItem({ id: "b" }),
        makePlanItem({ id: "c" }),
        makePlanItem({ id: "d" }),
      ],
      null
    )

    expect(focus.primary?.card.id).toBe("a")
    expect(focus.queued.map((item) => item.card.id)).toEqual(["b", "c"])
    expect(focus.isNoFitAlternative).toBe(false)
  })

  it("only proposes items that fit the focus block", () => {
    const focus = pickPlanFocus(
      [
        makePlanItem({ id: "long", fitsMaxSize: false }),
        makePlanItem({ id: "quick", fitsMaxSize: true }),
      ],
      "S"
    )

    expect(focus.primary?.card.id).toBe("quick")
    expect(focus.queued).toEqual([])
  })

  it("falls back to the shortest ready alternative", () => {
    const focus = pickPlanFocus(
      [
        makePlanItem({ id: "long", fitsMaxSize: false }),
        makePlanItem({
          id: "shorter",
          fitsMaxSize: false,
          isShortestAlternative: true,
        }),
      ],
      "S"
    )

    expect(focus.primary?.card.id).toBe("shorter")
    expect(focus.isNoFitAlternative).toBe(true)
  })

  it("returns nothing when no ready item fits and no estimate exists", () => {
    const focus = pickPlanFocus(
      [makePlanItem({ id: "unknown", fitsMaxSize: false })],
      "S"
    )

    expect(focus.primary).toBeNull()
  })
})

describe("getPlanTags", () => {
  it("builds tags from structured fields", () => {
    const labels = getPlanTags(
      makePlanItem({
        size: "XS",
        fitsMaxSize: true,
        isAssignedToMe: true,
        isContextMatch: true,
        isInProgress: true,
      }),
      {
        ...defaultPlanningPreferences,
        maxSize: "S",
        context: "backend",
      }
    ).map((tag) => tag.label)

    expect(labels).toEqual([
      "Ready to start",
      "Fits your focus slot",
      "Assigned to you",
      "In progress",
      "#backend",
    ])
  })

  it("flags an unknown size only when a focus slot is set", () => {
    const item = makePlanItem({ fitsMaxSize: false })

    expect(
      getPlanTags(item, {
        ...defaultPlanningPreferences,
        maxSize: "S",
        context: null,
      }).map((tag) => tag.label)
    ).toContain("Size unknown")
    expect(
      getPlanTags(item, {
        ...defaultPlanningPreferences,
        maxSize: null,
        context: null,
      }).map((tag) => tag.label)
    ).not.toContain("Size unknown")
  })
})
