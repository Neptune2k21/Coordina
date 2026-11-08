import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

import { ProjectSettings } from "@/features/projects/components/project-settings"
import * as projectApi from "@/features/projects/project-api"
import type { Project } from "@/types/project"

vi.mock("@/features/projects/project-api")
vi.mock("@/features/board", () => ({ BoardSettings: () => null }))
vi.mock("@/features/auth", () => ({
  useAuth: () => ({ session: { accessToken: "token", user: { id: "me" } } }),
}))

let workspaceRole = "OWNER"
vi.mock("@/features/workspaces", () => ({
  useWorkspaces: () => ({
    activeWorkspace: { id: "workspace", role: workspaceRole },
  }),
}))

const project = {
  id: "project",
  name: "Launch",
  description: null,
  key: "LCH",
  icon: "kanban",
  color: "teal",
  projectOwnerId: "someone-else",
  status: "ACTIVE",
} as Project

function renderSettings(overrides: Partial<Project> = {}) {
  const onProjectChange = vi.fn()
  const onDeleted = vi.fn()

  render(
    <ProjectSettings
      project={{ ...project, ...overrides }}
      onProjectChange={onProjectChange}
      onDeleted={onDeleted}
    />
  )

  return { onProjectChange, onDeleted }
}

describe("ProjectSettings", () => {
  afterEach(() => {
    cleanup()
    vi.resetAllMocks()
    workspaceRole = "OWNER"
  })

  it("saves the project details", async () => {
    const renamed = { ...project, name: "Launch v2" }
    vi.mocked(projectApi.updateProject).mockResolvedValue(renamed)
    const { onProjectChange } = renderSettings()

    fireEvent.change(screen.getByLabelText("Project name"), {
      target: { value: "Launch v2" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Save details" }))

    await waitFor(() => expect(onProjectChange).toHaveBeenCalledWith(renamed))
    expect(projectApi.updateProject).toHaveBeenCalledWith(
      "token",
      "workspace",
      "project",
      {
        name: "Launch v2",
        description: "",
        key: "LCH",
        icon: "kanban",
        color: "teal",
      }
    )
  })

  it("completes the project after confirmation", async () => {
    const completed = { ...project, status: "COMPLETED" as const }
    vi.mocked(projectApi.updateProject).mockResolvedValue(completed)
    const { onProjectChange } = renderSettings()

    fireEvent.click(screen.getByRole("button", { name: "Complete project" }))
    fireEvent.click(await screen.findByRole("button", { name: "Confirm" }))

    await waitFor(() => expect(onProjectChange).toHaveBeenCalledWith(completed))
    expect(projectApi.updateProject).toHaveBeenCalledWith(
      "token",
      "workspace",
      "project",
      { status: "COMPLETED" }
    )
  })

  it("leaves the project once it is deleted permanently", async () => {
    vi.mocked(projectApi.permanentlyDeleteProject).mockResolvedValue()
    const { onDeleted } = renderSettings()

    fireEvent.click(screen.getByRole("button", { name: "Delete permanently" }))
    fireEvent.click(await screen.findByRole("button", { name: "Confirm" }))

    await waitFor(() => expect(onDeleted).toHaveBeenCalled())
  })

  it("is read-only for a member who does not own the project", () => {
    workspaceRole = "MEMBER"
    renderSettings()

    expect(screen.getByLabelText("Project name")).toBeDisabled()
    expect(
      screen.queryByRole("button", { name: "Save details" })
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole("button", { name: "Archive project" })
    ).not.toBeInTheDocument()
  })

  it("offers restore, not archive, for an archived project", () => {
    renderSettings({ status: "ARCHIVED" })

    expect(
      screen.getByRole("button", { name: "Restore project" })
    ).toBeInTheDocument()
    expect(
      screen.queryByRole("button", { name: "Archive project" })
    ).not.toBeInTheDocument()
  })
})
