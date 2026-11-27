import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

import { MilestoneStaffingDialog } from "@/features/board/components/milestone-staffing-dialog"
import type { MilestoneStaffingActions } from "@/features/board/hooks/use-milestone-staffing"
import type { BoardMilestone, BoardMilestoneStaffing } from "@/types/task"
import type { WorkspaceMember } from "@/types/workspace"

const launch: BoardMilestone = {
  cardId: "launch",
  title: "Launch v1",
  targetDate: "2027-03-12",
  status: "LATE",
  forecastDate: "2027-03-18",
  commitDate: "2027-03-24",
  bestCaseDate: "2027-03-10",
  worstCaseDate: "2027-03-31",
  unlimitedTeamDate: "2027-03-09",
  onTime: 0.35,
  slipDays: 6,
  openCardCount: 5,
  unsizedCardCount: 0,
  drivers: [],
}

const ada = { userId: "ada", name: "Ada", email: "ada@example.com" }
const grace = { userId: "grace", name: "Grace", email: "grace@example.com" }
const linus = { userId: "linus", name: "Linus", email: "linus@example.com" }

const members: WorkspaceMember[] = [ada, grace, linus].map((person) => ({
  ...person,
  role: "MEMBER",
  joinedAt: "2026-01-01T00:00:00Z",
}))

const proposal: BoardMilestoneStaffing = {
  milestoneId: "launch",
  targetDate: "2027-03-12",
  forecastDateBefore: "2027-03-18",
  commitDateBefore: "2027-03-24",
  forecastDateAfter: "2027-03-15",
  commitDateAfter: "2027-03-20",
  othersDelayDays: 0,
  people: [ada, grace],
  changes: [
    {
      cardId: "payment",
      title: "Payment",
      from: [ada],
      to: grace,
      startBefore: "2027-03-11",
      startAfter: "2027-03-04",
      matchingLabels: [],
    },
    {
      cardId: "docs",
      title: "Docs",
      from: [],
      to: ada,
      startBefore: "2027-03-08",
      startAfter: "2027-03-08",
      matchingLabels: ["writing"],
    },
  ],
}

function actions(overrides: Partial<MilestoneStaffingActions> = {}) {
  return {
    propose: vi.fn(() => Promise.resolve(proposal)),
    evaluate: vi.fn(() =>
      Promise.resolve({
        ...proposal,
        commitDateAfter: "2027-03-23",
        changes: [proposal.changes[1]],
      })
    ),
    apply: vi.fn(() => Promise.resolve()),
    ...overrides,
  }
}

function renderDialog(
  staffing: MilestoneStaffingActions,
  options: { isReadOnly?: boolean; onClose?: () => void } = {}
) {
  render(
    <MilestoneStaffingDialog
      milestone={launch}
      members={members}
      actions={staffing}
      isReadOnly={options.isReadOnly ?? false}
      onClose={options.onClose ?? vi.fn()}
    />
  )
}

describe("milestone staffing dialog", () => {
  afterEach(() => cleanup())

  it("shows the gain and each change with its reason", async () => {
    renderDialog(actions())

    expect(await screen.findByText("Ships 4 days sooner")).toBeTruthy()
    expect(
      screen.getByText("Give Payment to Grace instead of Ada")
    ).toBeTruthy()
    expect(screen.getByText(/has done #writing cards/)).toBeTruthy()
    expect(screen.getByRole("button", { name: "Apply 2 changes" })).toBeTruthy()
  })

  it("plans with the board's team until someone else is added", async () => {
    const staffing = actions()
    renderDialog(staffing)

    await screen.findByText("Ships 4 days sooner")
    expect(
      screen.getByRole("button", { name: "Grace" }).getAttribute("aria-pressed")
    ).toBe("true")
    expect(
      screen.getByRole("button", { name: "Linus" }).getAttribute("aria-pressed")
    ).toBe("false")

    fireEvent.click(screen.getByRole("button", { name: "Linus" }))
    fireEvent.click(screen.getByLabelText(/Also move cards/))

    await waitFor(() =>
      expect(staffing.propose).toHaveBeenLastCalledWith("launch", {
        people: ["ada", "grace", "linus"],
        allowReassignment: true,
      })
    )
  })

  it("measures the changes left chosen", async () => {
    const staffing = actions()
    renderDialog(staffing)

    fireEvent.click(await screen.findByLabelText(/Give Payment/))

    expect(staffing.evaluate).toHaveBeenCalledWith("launch", [
      { cardId: "docs", userId: "ada" },
    ])
    expect(await screen.findByText("Ships 1 day sooner")).toBeTruthy()
    expect(screen.getByRole("button", { name: "Apply 1 change" })).toBeTruthy()
  })

  it("applies the chosen changes and closes", async () => {
    const staffing = actions()
    const onClose = vi.fn()
    renderDialog(staffing, { onClose })

    fireEvent.click(
      await screen.findByRole("button", { name: "Apply 2 changes" })
    )

    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(staffing.apply).toHaveBeenCalledWith("launch", [
      { cardId: "payment", userId: "grace" },
      { cardId: "docs", userId: "ada" },
    ])
  })

  it("only shows the proposal on a read-only board", async () => {
    renderDialog(actions(), { isReadOnly: true })

    expect(await screen.findByText("Ships 4 days sooner")).toBeTruthy()
    expect(screen.queryByRole("button", { name: /Apply/ })).toBeNull()
  })

  it("says when no better plan exists", async () => {
    renderDialog(
      actions({
        propose: vi.fn(() => Promise.resolve({ ...proposal, changes: [] })),
      })
    )

    expect(await screen.findByText(/No better plan found/)).toBeTruthy()
  })
})
