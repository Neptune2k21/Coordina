import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

import { WorkspaceMembersPanel } from "@/features/workspaces/components/workspace-members-panel"
import { ApiError } from "@/lib/api"
import type { WorkspaceMember } from "@/types/workspace"

vi.mock("@/features/auth", () => ({
  useAuth: () => ({ session: { accessToken: "token", user: { id: "me" } } }),
}))

const workspace = vi.hoisted(() => ({
  role: "OWNER",
  members: [] as WorkspaceMember[],
  changeMemberRole: vi.fn(),
  leave: vi.fn(),
  removeMember: vi.fn(),
}))

vi.mock("@/features/workspaces/workspace-context", () => ({
  useWorkspaces: () => ({
    activeWorkspace: { id: "workspace", role: workspace.role },
    isMutating: false,
    changeMemberRole: workspace.changeMemberRole,
    leave: workspace.leave,
    listMembers: () => Promise.resolve(workspace.members),
    removeMember: workspace.removeMember,
  }),
}))

function member(
  userId: string,
  name: string,
  role: "OWNER" | "MEMBER"
): WorkspaceMember {
  return {
    userId,
    name,
    email: `${userId}@coordina.test`,
    role,
    joinedAt: "2026-11-01T10:00:00Z",
  }
}

async function renderPanel(
  role: "OWNER" | "MEMBER",
  members: WorkspaceMember[]
) {
  workspace.role = role
  workspace.members = members
  render(<WorkspaceMembersPanel />)
  await screen.findByText(members[0].name ?? "")
}

describe("WorkspaceMembersPanel", () => {
  afterEach(() => {
    cleanup()
    vi.clearAllMocks()
  })

  it("lets an owner promote a member, demote an owner and remove a member", async () => {
    await renderPanel("OWNER", [
      member("me", "Ada", "OWNER"),
      member("grace", "Grace", "OWNER"),
      member("linus", "Linus", "MEMBER"),
    ])

    expect(screen.getByRole("button", { name: "Make member" })).toBeTruthy()
    expect(screen.getByRole("button", { name: "Make owner" })).toBeTruthy()
    expect(screen.getByRole("button", { name: "Remove Linus" })).toBeTruthy()
    expect(screen.queryByRole("button", { name: "Remove Grace" })).toBeNull()
    expect(screen.getByRole("button", { name: "Step down" })).toBeTruthy()
  })

  it("shows the new role once the change is saved", async () => {
    workspace.changeMemberRole.mockResolvedValue(
      member("linus", "Linus", "OWNER")
    )
    await renderPanel("OWNER", [
      member("me", "Ada", "OWNER"),
      member("linus", "Linus", "MEMBER"),
    ])

    fireEvent.click(screen.getByRole("button", { name: "Make owner" }))

    await waitFor(() =>
      expect(
        screen.getAllByRole("button", { name: "Make member" })
      ).toHaveLength(1)
    )
    expect(workspace.changeMemberRole).toHaveBeenCalledWith(
      "workspace",
      "linus",
      "OWNER"
    )
  })

  it("keeps the last owner from leaving or stepping down, and says why", async () => {
    await renderPanel("OWNER", [
      member("me", "Ada", "OWNER"),
      member("linus", "Linus", "MEMBER"),
    ])

    const leave = screen.getByRole("button", { name: "Leave" })

    expect((leave as HTMLButtonElement).disabled).toBe(true)
    expect(screen.queryByRole("button", { name: "Step down" })).toBeNull()
    expect(
      screen.getByText(/You are the last owner\. Make another member an owner/)
    ).toBeTruthy()
  })

  it("shows a member only their own leave action, confirmed in two steps", async () => {
    workspace.leave.mockResolvedValue(undefined)
    await renderPanel("MEMBER", [
      member("ada", "Ada", "OWNER"),
      member("me", "Linus", "MEMBER"),
    ])

    expect(screen.queryByRole("button", { name: /Make/ })).toBeNull()
    expect(screen.queryByRole("button", { name: /Remove/ })).toBeNull()

    fireEvent.click(screen.getByRole("button", { name: "Leave" }))
    expect(workspace.leave).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole("button", { name: "Confirm leave" }))
    await waitFor(() =>
      expect(workspace.leave).toHaveBeenCalledWith("workspace")
    )
  })

  it("shows why the API refused a change", async () => {
    workspace.changeMemberRole.mockRejectedValue(
      new ApiError("Only workspace owners can change member roles.", 403)
    )
    await renderPanel("OWNER", [
      member("me", "Ada", "OWNER"),
      member("linus", "Linus", "MEMBER"),
    ])

    fireEvent.click(screen.getByRole("button", { name: "Make owner" }))

    expect(
      await screen.findByText("Only workspace owners can change member roles.")
    ).toBeTruthy()
  })
})
