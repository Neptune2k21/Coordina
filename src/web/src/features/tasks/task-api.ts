import { ApiError } from "@/features/auth/auth-api"
import type {
  Board,
  BoardCardInput,
  BoardTemplate,
} from "@/features/tasks/task-types"

const apiBaseUrl = import.meta.env.VITE_API_URL ?? "http://localhost:5050"

type ApiProblem = {
  title?: string
  message?: string
  errors?: Record<string, string[]>
}

export async function getDefaultBoard(
  accessToken: string,
  workspaceId: string,
  projectId: string
) {
  return request<Board>(
    `/workspaces/${workspaceId}/projects/${projectId}/board`,
    accessToken
  )
}

export async function createBoard(
  accessToken: string,
  workspaceId: string,
  projectId: string,
  input: { name: string; template: BoardTemplate; customListTitles?: string[] }
) {
  return request<Board>(
    `/workspaces/${workspaceId}/projects/${projectId}/boards`,
    accessToken,
    {
      method: "POST",
      body: JSON.stringify(input),
    }
  )
}

export async function createList(
  accessToken: string,
  workspaceId: string,
  projectId: string,
  boardId: string,
  title: string
) {
  return request<Board>(
    `/workspaces/${workspaceId}/projects/${projectId}/boards/${boardId}/lists`,
    accessToken,
    {
      method: "POST",
      body: JSON.stringify({ title }),
    }
  )
}

export async function updateList(
  accessToken: string,
  workspaceId: string,
  projectId: string,
  boardId: string,
  listId: string,
  title: string
) {
  return request<Board>(
    `/workspaces/${workspaceId}/projects/${projectId}/boards/${boardId}/lists/${listId}`,
    accessToken,
    {
      method: "PATCH",
      body: JSON.stringify({ title }),
    }
  )
}

export async function createCard(
  accessToken: string,
  workspaceId: string,
  projectId: string,
  boardId: string,
  listId: string,
  input: BoardCardInput
) {
  return request<Board>(
    `/workspaces/${workspaceId}/projects/${projectId}/boards/${boardId}/lists/${listId}/cards`,
    accessToken,
    {
      method: "POST",
      body: JSON.stringify(normalizeCardInput(input)),
    }
  )
}

export async function updateCard(
  accessToken: string,
  workspaceId: string,
  projectId: string,
  boardId: string,
  cardId: string,
  input: BoardCardInput
) {
  return request<Board>(
    `/workspaces/${workspaceId}/projects/${projectId}/boards/${boardId}/cards/${cardId}`,
    accessToken,
    {
      method: "PATCH",
      body: JSON.stringify(normalizeCardUpdateInput(input)),
    }
  )
}

export async function moveCard(
  accessToken: string,
  workspaceId: string,
  projectId: string,
  boardId: string,
  cardId: string,
  listId: string
) {
  return request<Board>(
    `/workspaces/${workspaceId}/projects/${projectId}/boards/${boardId}/cards/${cardId}/move`,
    accessToken,
    {
      method: "PATCH",
      body: JSON.stringify({ listId }),
    }
  )
}

export async function deleteCard(
  accessToken: string,
  workspaceId: string,
  projectId: string,
  boardId: string,
  cardId: string
) {
  await request<void>(
    `/workspaces/${workspaceId}/projects/${projectId}/boards/${boardId}/cards/${cardId}`,
    accessToken,
    {
      method: "DELETE",
    }
  )
}

function normalizeCardInput(input: BoardCardInput) {
  return {
    ...input,
    priority: input.priority || null,
    dueDate: input.dueDate || null,
    labels: input.labels ?? [],
    assigneeIds: input.assigneeIds ?? [],
  }
}

function normalizeCardUpdateInput(input: BoardCardInput) {
  return {
    ...normalizeCardInput(input),
    clearDueDate: input.dueDate === null || input.dueDate === "",
    priority: input.priority ?? "",
  }
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
    problem.message ?? problem.title ?? "Board request failed.",
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
