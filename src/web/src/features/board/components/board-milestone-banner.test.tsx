import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

import { BoardMilestoneBanner } from "@/features/board/components/board-milestone-banner"
import type { BoardMilestone } from "@/types/task"

const late: BoardMilestone = {
  cardId: "launch",
  title: "Launch v1",
  targetDate: "2027-03-12",
  status: "LATE",
  forecastDate: "2027-03-18",
  commitDate: "2027-03-24",
  slipDays: 6,
  openCardCount: 12,
  unsizedCardCount: 7,
  drivers: [
    {
      cardId: "payment",
      title: "Payment API",
      criticality: 0.9,
      sensitivity: 0.8,
    },
    {
      cardId: "onboarding",
      title: "Onboarding",
      criticality: 0.4,
      sensitivity: 0.3,
    },
  ],
}

const shipped: BoardMilestone = {
  ...late,
  cardId: "beta",
  title: "Beta",
  status: "COMPLETED",
  forecastDate: null,
  commitDate: null,
  slipDays: null,
  unsizedCardCount: 0,
  drivers: [],
}

describe("milestone banner", () => {
  afterEach(() => cleanup())

  it("shows the forecast, the slip, the drivers and missing sizes", () => {
    render(<BoardMilestoneBanner milestones={[late]} onSelectCard={vi.fn()} />)

    expect(screen.getByText("Launch v1")).toBeTruthy()
    expect(screen.getByText("Late")).toBeTruthy()
    expect(screen.getByText("6 days late")).toBeTruthy()
    expect(screen.getByText(/85%:/)).toBeTruthy()
    expect(
      screen.getByText("7 cards have no size, low confidence")
    ).toBeTruthy()
  })

  it("opens a driver's card", () => {
    const onSelectCard = vi.fn()
    render(
      <BoardMilestoneBanner milestones={[late]} onSelectCard={onSelectCard} />
    )

    fireEvent.click(screen.getByRole("button", { name: "Payment API" }))

    expect(onSelectCard).toHaveBeenCalledWith("payment")
  })

  it("shows a completed milestone without a forecast", () => {
    render(
      <BoardMilestoneBanner milestones={[shipped]} onSelectCard={vi.fn()} />
    )

    expect(screen.getByText("Completed")).toBeTruthy()
    expect(screen.queryByText(/Forecast/)).toBeNull()
  })

  it("renders nothing without milestones", () => {
    const { container } = render(
      <BoardMilestoneBanner milestones={[]} onSelectCard={vi.fn()} />
    )

    expect(container.childElementCount).toBe(0)
  })

  it("folds milestones beyond the first three", () => {
    const many = ["a", "b", "c", "d"].map((id) => ({
      ...late,
      cardId: id,
      title: `Milestone ${id}`,
    }))
    render(<BoardMilestoneBanner milestones={many} onSelectCard={vi.fn()} />)

    expect(screen.queryByText("Milestone d")).toBeNull()
    fireEvent.click(
      screen.getByRole("button", { name: "Show all 4 milestones" })
    )
    expect(screen.getByText("Milestone d")).toBeTruthy()
  })
})
