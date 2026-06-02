import { apiRequest } from "@/lib/api"
import type {
  Workspace,
  WorkspaceInvite,
  WorkspaceMember,
} from "@/types/workspace"

export async function listWorkspaces(accessToken: string) {
  return apiRequest<Workspace[]>("/workspaces", {
    accessToken,
    errorMessage: "Unable to load workspaces.",
  })
}

export async function getWorkspace(accessToken: string, workspaceId: string) {
  return apiRequest<Workspace>(`/workspaces/${workspaceId}`, {
    accessToken,
    errorMessage: "Unable to load workspace.",
  })
}

export async function createWorkspace(accessToken: string, name: string) {
  return apiRequest<Workspace>("/workspaces", {
    accessToken,
    method: "POST",
    body: JSON.stringify({ name }),
    errorMessage: "Workspace request failed.",
  })
}

export async function joinWorkspace(accessToken: string, inviteCode: string) {
  return apiRequest<Workspace>("/workspaces/join", {
    accessToken,
    method: "POST",
    body: JSON.stringify({ inviteCode }),
    errorMessage: "Workspace request failed.",
  })
}

export async function createWorkspaceInvite(
  accessToken: string,
  workspaceId: string
) {
  return apiRequest<WorkspaceInvite>(`/workspaces/${workspaceId}/invites`, {
    accessToken,
    method: "POST",
    errorMessage: "Workspace request failed.",
  })
}

export async function listWorkspaceMembers(
  accessToken: string,
  workspaceId: string
) {
  return apiRequest<WorkspaceMember[]>(`/workspaces/${workspaceId}/members`, {
    accessToken,
    errorMessage: "Unable to load workspace members.",
  })
}

export async function removeWorkspaceMember(
  accessToken: string,
  workspaceId: string,
  memberUserId: string
) {
  await apiRequest<void>(`/workspaces/${workspaceId}/members/${memberUserId}`, {
    accessToken,
    method: "DELETE",
    errorMessage: "Workspace request failed.",
  })
}

export async function deleteWorkspace(
  accessToken: string,
  workspaceId: string
) {
  await apiRequest<void>(`/workspaces/${workspaceId}`, {
    accessToken,
    method: "DELETE",
    errorMessage: "Workspace request failed.",
  })
}
