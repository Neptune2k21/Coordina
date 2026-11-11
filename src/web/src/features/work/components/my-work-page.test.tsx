import {
  cleanup,
  fireEvent,
  render,
  screen,
  within,
} from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

import { MyWorkPage } from "@/features/work/components/my-work-page"
import * as workApi from "@/features/work/work-api"
import { ApiError } from "@/lib/api"
import type { MyWork, MyWorkItem } from "@/types/work"

vi.mock("@/features/work/work-api")
vi.mock("@/features/projects", () => ({ ProjectIconMark: () => null }))
vi.mock("@/features/auth", () => ({
  useAuth: () => ({ session: { accessToken: "token" }, signOut: vi.fn() }),
}))
vi.mock("@/features/workspaces", () => ({
  useWorkspaces: () => ({
    activeWorkspace: { name: "Team" },
    activeWorkspaceId: "workspace",
  }),
}))

function item(title: string, overrides: Partial<MyWorkItem> = {}): MyWorkItem {
  return {
    project: {
      id: "project",
      name: "Launch",
      key: "LCH",
      icon: null,
      color: null,
    },
    boardId: "board",
    dueDate: null,
    isUnassigned: false,
    plan: {
      card: {
        id: `card-${title}`,
        listId: "list",
        title,
        isCompleted: false,
        dependencyCount: 0,
        dependentCount: 0,
        dependencyDepth: 0,
        dependentDepth: 0,
        transitiveDependentCount: 0,
        isCriticalPath: false,
      },
      score: 10,
      isActionable: true,
      blockerCount: 0,
      size: null,
      fitsMaxSize: true,
      isAssignedToMe: true,
      isContextMatch: false,
      isInProgress: false,
      isShortestAlternative: false,
      reasons: ["Ready now"],
    },
    ...overrides,
  }
}

function work(overrides: Partial<MyWork> = {}): MyWork {
  return {
    workspaceId: "workspace",
    now: [],
    next: [],
    nextTotal: 0,
    blocked: [],
    ...overrides,
  }
}

describe("MyWorkPage", () => {
  afterEach(() => {
    cleanup()
    vi.resetAllMocks()
  })

  it("shows each section with its cards and why they rank there", async () => {
    vi.mocked(workApi.getMyWork).mockResolvedValue(
      work({
        now: [item("Doing it")],
        next: [item("Ship API"), item("Anyone", { isUnassigned: true })],
        nextTotal: 5,
        blocked: [item("Waiting")],
      })
    )
    render(<MyWorkPage onOpenCard={vi.fn()} />)

    const next = await screen.findByRole("region", { name: "Next" })
    expect(within(next).getByText("Ship API")).toBeInTheDocument()
    expect(within(next).getByText("Unassigned")).toBeInTheDocument()
    expect(
      within(next).getByText("3 more ready across your boards.")
    ).toBeInTheDocument()
    expect(
      within(screen.getByRole("region", { name: "Now" })).getByText("Doing it")
    ).toBeInTheDocument()
    expect(
      within(screen.getByRole("region", { name: "Blocked" })).getByText(
        "Waiting"
      )
    ).toBeInTheDocument()
    expect(within(next).getAllByText("Ready now")).toHaveLength(2)
    expect(workApi.getMyWork).toHaveBeenCalledWith("token", "workspace")
  })

  it("opens the card a user picks", async () => {
    vi.mocked(workApi.getMyWork).mockResolvedValue(
      work({ next: [item("Ship API")], nextTotal: 1 })
    )
    const onOpenCard = vi.fn()
    render(<MyWorkPage onOpenCard={onOpenCard} />)

    fireEvent.click(await screen.findByRole("button", { name: /Ship API/ }))

    expect(onOpenCard).toHaveBeenCalledWith("project", "card-Ship API")
  })

  it("explains an empty section", async () => {
    vi.mocked(workApi.getMyWork).mockResolvedValue(work())
    render(<MyWorkPage onOpenCard={vi.fn()} />)

    expect(await screen.findByText(/Nothing in progress/)).toBeInTheDocument()
    expect(screen.getByText(/Nothing ready for you/)).toBeInTheDocument()
  })

  it("shows why the work could not be loaded", async () => {
    vi.mocked(workApi.getMyWork).mockRejectedValue(
      new ApiError("Workspace not found.", 404)
    )
    render(<MyWorkPage onOpenCard={vi.fn()} />)

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Workspace not found."
    )
  })
})
