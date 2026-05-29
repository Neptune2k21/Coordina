import { ApiError } from "@/features/auth/auth-api"
import type {
  Workspace,
  WorkspaceInvite,
  WorkspaceMember,
} from "@/features/workspaces/workspace-types"

const apiBaseUrl = import.meta.env.VITE_API_URL ?? "http://localhost:5050"

type ApiProblem = {
  title?: string
  message?: string
  errors?: Record<string, string[]>
}

export async function listWorkspaces(accessToken: string) {
  return request<Workspace[]>("/workspaces", accessToken)
}

export async function getWorkspace(accessToken: string, workspaceId: string) {
  return request<Workspace>(`/workspaces/${workspaceId}`, accessToken)
}

export async function createWorkspace(accessToken: string, name: string) {
  return request<Workspace>("/workspaces", accessToken, {
    method: "POST",
    body: JSON.stringify({ name }),
  })
}

export async function joinWorkspace(accessToken: string, inviteCode: string) {
  return request<Workspace>("/workspaces/join", accessToken, {
    method: "POST",
    body: JSON.stringify({ inviteCode }),
  })
}

export async function createWorkspaceInvite(
  accessToken: string,
  workspaceId: string
) {
  return request<WorkspaceInvite>(
    `/workspaces/${workspaceId}/invites`,
    accessToken,
    {
      method: "POST",
    }
  )
}

export async function listWorkspaceMembers(
  accessToken: string,
  workspaceId: string
) {
  return request<WorkspaceMember[]>(
    `/workspaces/${workspaceId}/members`,
    accessToken
  )
}

export async function removeWorkspaceMember(
  accessToken: string,
  workspaceId: string,
  memberUserId: string
) {
  await request<void>(
    `/workspaces/${workspaceId}/members/${memberUserId}`,
    accessToken,
    {
      method: "DELETE",
    }
  )
}

export async function deleteWorkspace(
  accessToken: string,
  workspaceId: string
) {
  await request<void>(`/workspaces/${workspaceId}`, accessToken, {
    method: "DELETE",
  })
}

async function request<T>(
  path: string,
  accessToken: string,
  init: RequestInit = {}
): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${accessToken}`,
      ...init.headers,
    },
  })

  if (!response.ok) {
    throw await toApiError(response)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return response.json() as Promise<T>
}

async function toApiError(response: Response) {
  const problem = await readProblem(response)

  return new ApiError(
    problem.message ?? problem.title ?? "Workspace request failed.",
    response.status,
    problem.errors
  )
}

async function readProblem(response: Response): Promise<ApiProblem> {
  try {
    return (await response.json()) as ApiProblem
  } catch {
    return {}
  }
}
