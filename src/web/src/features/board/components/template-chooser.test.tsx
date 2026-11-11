import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"

import * as boardApi from "@/features/board/api/board-api"
import { TemplateChooser } from "@/features/board/components/template-chooser"
import type { BoardTemplateDefinition } from "@/types/task"

vi.mock("@/features/board/api/board-api")
vi.mock("@/features/auth", () => ({
  useAuth: () => ({ session: { accessToken: "token" } }),
}))

const template: BoardTemplateDefinition = {
  id: "BUG_TRACKING",
  name: "Bug Tracking",
  summary: "From the API.",
  lists: [
    {
      title: "Reported",
      taskStatus: "TODO",
      cards: [
        {
          key: "fix",
          title: "Fix the crash",
          description: "Details",
          priority: "HIGH",
          size: "XS",
          dueInDays: 2,
          labels: ["bug"],
          subtasks: [],
          dependsOn: ["find"],
        },
      ],
    },
    {
      title: "Investigating",
      taskStatus: "IN_PROGRESS",
      cards: [
        {
          key: "find",
          title: "Find the cause",
          description: "Details",
          priority: "HIGH",
          size: "XS",
          dueInDays: null,
          labels: ["bug"],
          subtasks: [],
          dependsOn: [],
        },
      ],
    },
  ],
}

describe("TemplateChooser", () => {
  beforeEach(() => {
    vi.mocked(boardApi.getBoardTemplates).mockResolvedValue([template])
  })

  afterEach(() => {
    cleanup()
    vi.resetAllMocks()
  })

  it("renders its preview from the API response", async () => {
    render(<TemplateChooser isCreating={false} onCreate={vi.fn()} />)

    expect(await screen.findByText("Bug Tracking")).toBeTruthy()
    expect(screen.getByText("From the API.")).toBeTruthy()
    expect(screen.getByText("Reported")).toBeTruthy()
    expect(screen.getByText("Fix the crash")).toBeTruthy()
    expect(screen.getByText("2 example cards, 1 dependency")).toBeTruthy()
    expect(boardApi.getBoardTemplates).toHaveBeenCalledWith("token")
  })

  it("creates a board with example cards by default", async () => {
    const onCreate = vi.fn()
    render(<TemplateChooser isCreating={false} onCreate={onCreate} />)

    fireEvent.click(await screen.findByText("Bug Tracking"))

    expect(onCreate).toHaveBeenCalledWith({
      template: "BUG_TRACKING",
      includeExampleCards: true,
    })
  })

  it("previews and creates lists only when example cards are off", async () => {
    const onCreate = vi.fn()
    render(<TemplateChooser isCreating={false} onCreate={onCreate} />)
    await screen.findByText("Bug Tracking")

    fireEvent.click(screen.getByLabelText(/include example cards/i))

    expect(screen.queryByText("Fix the crash")).toBeNull()
    expect(screen.getByText("2 empty lists")).toBeTruthy()

    fireEvent.click(screen.getByText("Bug Tracking"))

    expect(onCreate).toHaveBeenCalledWith({
      template: "BUG_TRACKING",
      includeExampleCards: false,
    })
  })

  it("creates a blank board with no lists or cards", async () => {
    const onCreate = vi.fn()
    render(<TemplateChooser isCreating={false} onCreate={onCreate} />)
    await screen.findByText("Bug Tracking")

    fireEvent.click(screen.getByRole("button", { name: /^Blank/ }))

    expect(onCreate).toHaveBeenCalledWith({ template: "BLANK" })
  })

  it("offers a retry and keeps custom boards available on failure", async () => {
    const onCreate = vi.fn()
    vi.mocked(boardApi.getBoardTemplates).mockRejectedValueOnce(
      new Error("Down")
    )
    render(<TemplateChooser isCreating={false} onCreate={onCreate} />)

    expect((await screen.findByRole("alert")).textContent).toContain(
      "Templates could not be loaded."
    )

    expect(screen.getByRole("button", { name: /^Blank/ })).toBeTruthy()

    fireEvent.click(screen.getByRole("button", { name: "Create custom" }))
    expect(onCreate).toHaveBeenCalledWith({
      template: "CUSTOM",
      customListTitles: ["To Do", "Doing", "Review", "Done"],
    })

    fireEvent.click(screen.getByRole("button", { name: "Try again" }))

    expect(await screen.findByText("Bug Tracking")).toBeTruthy()
    expect(screen.queryByRole("alert")).toBeNull()
  })
})
