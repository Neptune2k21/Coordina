import { ApiError } from "@/features/auth/auth-api"
import type {
  Project,
  ProjectInput,
  ProjectUpdateInput,
} from "@/features/projects/project-types"

const apiBaseUrl = import.meta.env.VITE_API_URL ?? "http://localhost:5050"

type ApiProblem = {
  title?: string
  message?: string
  errors?: Record<string, string[]>
}

export async function listProjects(
  accessToken: string,
  workspaceId: string,
  options: { includeArchived?: boolean; includeCompleted?: boolean } = {}
) {
  const params = new URLSearchParams()

  if (options.includeArchived) {
    params.set("includeArchived", "true")
  }

  if (options.includeCompleted) {
    params.set("includeCompleted", "true")
  }

  const query = params.size > 0 ? `?${params.toString()}` : ""

  return request<Project[]>(
    `/workspaces/${workspaceId}/projects${query}`,
    accessToken
  )
}

export async function createProject(
  accessToken: string,
  workspaceId: string,
  input: ProjectInput
) {
  return request<Project>(`/workspaces/${workspaceId}/projects`, accessToken, {
    method: "POST",
    body: JSON.stringify(input),
  })
}

export async function updateProject(
  accessToken: string,
  workspaceId: string,
  projectId: string,
  input: ProjectUpdateInput
) {
  return request<Project>(
    `/workspaces/${workspaceId}/projects/${projectId}`,
    accessToken,
    {
      method: "PATCH",
      body: JSON.stringify(input),
    }
  )
}

export async function archiveProject(
  accessToken: string,
  workspaceId: string,
  projectId: string
) {
  await request<void>(
    `/workspaces/${workspaceId}/projects/${projectId}`,
    accessToken,
    {
      method: "DELETE",
    }
  )
}

export async function permanentlyDeleteProject(
  accessToken: string,
  workspaceId: string,
  projectId: string
) {
  await request<void>(
    `/workspaces/${workspaceId}/projects/${projectId}/permanent`,
    accessToken,
    {
      method: "DELETE",
    }
  )
}

export async function restoreProject(
  accessToken: string,
  workspaceId: string,
  projectId: string
) {
  return updateProject(accessToken, workspaceId, projectId, {
    status: "ACTIVE",
  })
}

export async function deleteProject(
  accessToken: string,
  workspaceId: string,
  projectId: string
) {
  await archiveProject(accessToken, workspaceId, projectId)
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
    problem.message ?? problem.title ?? "Project request failed.",
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
