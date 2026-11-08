import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"

import * as boardApi from "@/features/board/api/board-api"
import { BoardSettings } from "@/features/board/components/board-settings"
import {
  makeBoard,
  makeCard,
  makeList,
} from "@/features/board/model/board-test-fixtures"
import { defaultPlanningPreferences } from "@/features/board/model/planning-preferences"
import { ApiError } from "@/lib/api"
import type { Board, BoardList } from "@/types/task"
import type { Project } from "@/types/project"

vi.mock("@/features/board/api/board-api")
vi.mock("@/features/auth", () => ({
  useAuth: () => ({ session: { accessToken: "token" } }),
}))

const project = { id: "project", status: "ACTIVE" } as Project

function boardOf(...lists: BoardList[]): Board {
  return makeBoard(lists.map((list, position) => ({ ...list, position })))
}

const initialBoard = boardOf(
  makeList("To do", "TODO"),
  makeList("Doing", "IN_PROGRESS", [makeCard({ id: "c1", title: "Build" })]),
  makeList("Done", "DONE")
)

function renderSettings(status: Project["status"] = "ACTIVE") {
  render(
    <BoardSettings project={{ ...project, status }} workspaceId="workspace" />
  )
  return screen.findByRole("list", { name: "Board lists" })
}

describe("BoardSettings", () => {
  beforeEach(() => {
    vi.mocked(boardApi.getDefaultBoard).mockResolvedValue(initialBoard)
    vi.mocked(boardApi.getPlanningPreferences).mockResolvedValue(
      defaultPlanningPreferences
    )
  })

  afterEach(() => {
    cleanup()
    vi.resetAllMocks()
  })

  it("lists the board's lists in order with their status", async () => {
    const lists = await renderSettings()

    expect(
      within(lists)
        .getAllByRole("textbox")
        .map((input) => (input as HTMLInputElement).value)
    ).toEqual(["To do", "Doing", "Done"])
    expect(screen.getByLabelText("Status of Doing")).toHaveValue("IN_PROGRESS")
  })

  it("changes a list's status", async () => {
    vi.mocked(boardApi.updateList).mockResolvedValue(
      boardOf(
        makeList("To do", "TODO"),
        makeList("Doing", "DONE"),
        makeList("Done", "DONE")
      )
    )
    await renderSettings()

    fireEvent.change(screen.getByLabelText("Status of Doing"), {
      target: { value: "DONE" },
    })

    await waitFor(() =>
      expect(screen.getByLabelText("Status of Doing")).toHaveValue("DONE")
    )
    expect(boardApi.updateList).toHaveBeenCalledWith(
      "token",
      "workspace",
      "project",
      "board",
      "Doing",
      { taskStatus: "DONE" }
    )
  })

  it("moves a list down by asking for the next position", async () => {
    vi.mocked(boardApi.updateList).mockResolvedValue(initialBoard)
    await renderSettings()

    fireEvent.click(screen.getByRole("button", { name: "Move To do down" }))

    await waitFor(() =>
      expect(boardApi.updateList).toHaveBeenCalledWith(
        "token",
        "workspace",
        "project",
        "board",
        "To do",
        { position: 1 }
      )
    )
    expect(screen.getByRole("button", { name: "Move To do up" })).toBeDisabled()
  })

  it("asks where a deleted list's cards go", async () => {
    vi.mocked(boardApi.deleteList).mockResolvedValue(
      boardOf(makeList("To do", "TODO"), makeList("Done", "DONE"))
    )
    await renderSettings()

    fireEvent.click(screen.getByRole("button", { name: "Delete Doing" }))
    fireEvent.change(screen.getByLabelText("Move the cards of Doing to"), {
      target: { value: "Done" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Delete list" }))

    await waitFor(() =>
      expect(screen.queryByLabelText("Status of Doing")).not.toBeInTheDocument()
    )
    expect(boardApi.deleteList).toHaveBeenCalledWith(
      "token",
      "workspace",
      "project",
      "board",
      "Doing",
      "Done"
    )
  })

  it("shows why the API refused a change", async () => {
    vi.mocked(boardApi.updateList).mockRejectedValue(
      new ApiError("A board needs a list with status TODO.", 409)
    )
    await renderSettings()

    fireEvent.change(screen.getByLabelText("Status of To do"), {
      target: { value: "DONE" },
    })

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "A board needs a list with status TODO."
    )
    expect(screen.getByLabelText("Status of To do")).toHaveValue("TODO")
  })

  it("adds a list with the chosen status", async () => {
    vi.mocked(boardApi.createList).mockResolvedValue(initialBoard)
    await renderSettings()

    fireEvent.change(screen.getByLabelText("New list title"), {
      target: { value: " Review " },
    })
    fireEvent.change(screen.getByLabelText("New list status"), {
      target: { value: "IN_PROGRESS" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Add list" }))

    await waitFor(() =>
      expect(boardApi.createList).toHaveBeenCalledWith(
        "token",
        "workspace",
        "project",
        "board",
        "Review",
        "IN_PROGRESS"
      )
    )
  })

  it("keeps a completed project's workflow read-only", async () => {
    await renderSettings("COMPLETED")

    expect(screen.getByLabelText("Status of Doing")).toBeDisabled()
    expect(screen.queryByLabelText("New list title")).not.toBeInTheDocument()
  })

  it("says when the project has no board yet", async () => {
    vi.mocked(boardApi.getDefaultBoard).mockRejectedValue(
      new ApiError("Not found", 404)
    )
    render(<BoardSettings project={project} workspaceId="workspace" />)

    expect(
      await screen.findByText(/This project has no board yet/)
    ).toBeInTheDocument()
  })

  it("saves the planning preferences", async () => {
    vi.mocked(boardApi.savePlanningPreferences).mockImplementation(
      (_token, _workspace, _project, _board, preferences) =>
        Promise.resolve(preferences)
    )
    await renderSettings()
    const planning = await screen.findByRole("form", {
      name: "Planning preferences",
    })

    fireEvent.change(within(planning).getByLabelText("Focus time"), {
      target: { value: "120" },
    })
    fireEvent.click(within(planning).getByLabelText("Include unassigned cards"))
    fireEvent.click(
      within(planning).getByRole("button", { name: "Save preferences" })
    )

    expect(await screen.findByText(/Saved/)).toBeInTheDocument()
    expect(boardApi.savePlanningPreferences).toHaveBeenCalledWith(
      "token",
      "workspace",
      "project",
      "board",
      {
        ...defaultPlanningPreferences,
        availableMinutes: 120,
        includeUnassigned: false,
      }
    )
  })
})
