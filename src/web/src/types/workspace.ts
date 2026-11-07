import type { components } from "@/types/api-contract"

type Schemas = components["schemas"]

export type Workspace = Schemas["WorkspaceResponse"]
export type WorkspaceRole = Workspace["role"]
export type WorkspaceInvite = Schemas["CreateWorkspaceInviteResponse"]
export type WorkspaceMember = Schemas["WorkspaceMemberResponse"]
