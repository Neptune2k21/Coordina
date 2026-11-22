import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

import { MilestoneScopeDialog } from "@/features/board/components/milestone-scope-dialog"
import type { MilestoneScopeActions } from "@/features/board/hooks/use-milestone-scope"
import type { BoardMilestone, BoardMilestoneScope } from "@/types/task"

const launch: BoardMilestone = {
  cardId: "launch",
  title: "Launch v1",
  targetDate: "2027-03-12",
  status: "LATE",
  forecastDate: "2027-03-18",
  commitDate: "2027-03-24",
  slipDays: 6,
  openCardCount: 5,
  unsizedCardCount: 0,
  drivers: [],
}

const proposal: BoardMilestoneScope = {
  milestoneId: "launch",
  targetDate: "2027-03-12",
  onTimeBefore: 0.4,
  onTimeAfter: 0.86,
  forecastDate: "2027-03-08",
  commitDate: "2027-03-11",
  cuts: [
    {
      cardId: "pdf",
      title: "Export PDF",
      alsoRemoved: [{ cardId: "templates", title: "PDF templates" }],
    },
    { cardId: "dark", title: "Dark mode", alsoRemoved: [] },
  ],
}

function actions(overrides: Partial<MilestoneScopeActions> = {}) {
  return {
    propose: vi.fn(() => Promise.resolve(proposal)),
    evaluate: vi.fn(() =>
      Promise.resolve({
        ...proposal,
        onTimeAfter: 0.62,
        cuts: [proposal.cuts[1]],
      })
    ),
    cut: vi.fn(() => Promise.resolve()),
    ...overrides,
  }
}

describe("milestone scope dialog", () => {
  afterEach(() => cleanup())

  it("shows the proposal and what each cut takes with it", async () => {
    render(
      <MilestoneScopeDialog
        milestone={launch}
        actions={actions()}
        isReadOnly={false}
        onClose={vi.fn()}
      />
    )

    expect(await screen.findByText("Export PDF")).toBeTruthy()
    expect(screen.getByText("Also removes PDF templates")).toBeTruthy()
    expect(screen.getByText("40%")).toBeTruthy()
    expect(screen.getByText("86%")).toBeTruthy()
    expect(
      screen.getByRole("button", { name: "Take out 2 cards" })
    ).toBeTruthy()
  })

  it("measures the cards left chosen", async () => {
    const scope = actions()
    render(
      <MilestoneScopeDialog
        milestone={launch}
        actions={scope}
        isReadOnly={false}
        onClose={vi.fn()}
      />
    )

    fireEvent.click(await screen.findByLabelText(/Export PDF/))

    expect(scope.evaluate).toHaveBeenCalledWith("launch", ["dark"])
    expect(await screen.findByText("62%")).toBeTruthy()
    expect(screen.getByRole("button", { name: "Take out 1 card" })).toBeTruthy()
  })

  it("takes the chosen cards out and closes", async () => {
    const scope = actions()
    const onClose = vi.fn()
    render(
      <MilestoneScopeDialog
        milestone={launch}
        actions={scope}
        isReadOnly={false}
        onClose={onClose}
      />
    )

    fireEvent.click(
      await screen.findByRole("button", { name: "Take out 2 cards" })
    )

    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(scope.cut).toHaveBeenCalledWith("launch", ["pdf", "dark"])
  })

  it("only shows the proposal on a read-only board", async () => {
    render(
      <MilestoneScopeDialog
        milestone={launch}
        actions={actions()}
        isReadOnly
        onClose={vi.fn()}
      />
    )

    expect(await screen.findByText("Export PDF")).toBeTruthy()
    expect(screen.queryByRole("button", { name: /Take out/ })).toBeNull()
  })

  it("says when nothing is worth cutting", async () => {
    render(
      <MilestoneScopeDialog
        milestone={launch}
        actions={actions({
          propose: vi.fn(() => Promise.resolve({ ...proposal, cuts: [] })),
        })}
        isReadOnly={false}
        onClose={vi.fn()}
      />
    )

    expect(await screen.findByText(/Nothing is worth cutting/)).toBeTruthy()
  })
})
