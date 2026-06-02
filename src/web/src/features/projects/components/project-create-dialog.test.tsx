import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

import { ProjectCreateDialog } from "@/features/projects/components/project-create-dialog"

describe("ProjectCreateDialog", () => {
  afterEach(() => {
    cleanup()
    vi.restoreAllMocks()
  })

  it("submits project metadata only", async () => {
    const onCreate = vi.fn().mockResolvedValue(undefined)

    render(<ProjectCreateDialog isCreating={false} onCreate={onCreate} />)

    fireEvent.click(screen.getByRole("button", { name: "New project" }))
    fireEvent.change(screen.getByLabelText("Project name"), {
      target: { value: "Roadmap" },
    })
    fireEvent.change(screen.getByLabelText("Key"), {
      target: { value: "MAP" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Create project" }))

    await waitFor(() => {
      expect(onCreate).toHaveBeenCalledWith(
        expect.objectContaining({
          name: "Roadmap",
          key: "MAP",
        })
      )
    })
  })

  it("keeps board templates out of the project creation flow", async () => {
    const onCreate = vi.fn().mockResolvedValue(undefined)

    render(<ProjectCreateDialog isCreating={false} onCreate={onCreate} />)

    fireEvent.click(screen.getByRole("button", { name: "New project" }))

    expect(
      screen.queryByRole("button", { name: /Product Roadmap/i })
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole("button", { name: /Blank project/i })
    ).not.toBeInTheDocument()
  })
})
