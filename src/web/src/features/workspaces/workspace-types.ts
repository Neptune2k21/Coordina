export type WorkspaceRole = "OWNER" | "MEMBER"

export type Workspace = {
  id: string
  name: string
  role: WorkspaceRole
  createdAt: string
}

export type WorkspaceInvite = {
  code: string
  expiresAt: string
}

export type WorkspaceMember = {
  userId: string
  name: string | null
  email: string | null
  role: WorkspaceRole
  joinedAt: string
}
