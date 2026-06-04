import { apiRequest } from "@/lib/api"
import type { Project, ProjectInput, ProjectUpdateInput } from "@/types/project"

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

  return apiRequest<Project[]>(`/workspaces/${workspaceId}/projects${query}`, {
    accessToken,
    errorMessage: "Project request failed.",
  })
}

export async function getProject(
  accessToken: string,
  workspaceId: string,
  projectId: string
) {
  return apiRequest<Project>(
    `/workspaces/${workspaceId}/projects/${projectId}`,
    {
      accessToken,
      errorMessage: "Project request failed.",
    }
  )
}

export async function createProject(
  accessToken: string,
  workspaceId: string,
  input: ProjectInput
) {
  return apiRequest<Project>(`/workspaces/${workspaceId}/projects`, {
    accessToken,
    method: "POST",
    body: JSON.stringify(input),
    errorMessage: "Project request failed.",
  })
}

export async function updateProject(
  accessToken: string,
  workspaceId: string,
  projectId: string,
  input: ProjectUpdateInput
) {
  return apiRequest<Project>(
    `/workspaces/${workspaceId}/projects/${projectId}`,
    {
      accessToken,
      method: "PATCH",
      body: JSON.stringify(input),
      errorMessage: "Project request failed.",
    }
  )
}

export async function archiveProject(
  accessToken: string,
  workspaceId: string,
  projectId: string
) {
  await apiRequest<void>(`/workspaces/${workspaceId}/projects/${projectId}`, {
    accessToken,
    method: "DELETE",
    errorMessage: "Project request failed.",
  })
}

export async function permanentlyDeleteProject(
  accessToken: string,
  workspaceId: string,
  projectId: string
) {
  await apiRequest<void>(
    `/workspaces/${workspaceId}/projects/${projectId}/permanent`,
    {
      accessToken,
      method: "DELETE",
      errorMessage: "Project request failed.",
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
