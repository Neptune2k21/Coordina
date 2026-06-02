import type { ApiProblem, ApiValidationErrors } from "@/types/api"

export const apiBaseUrl =
  import.meta.env.VITE_API_URL ?? "http://localhost:5050"

type ApiRequestOptions = RequestInit & {
  accessToken?: string
  errorMessage?: string
  unauthorizedMessage?: string
}

export class ApiError extends Error {
  readonly status: number
  readonly errors?: ApiValidationErrors

  constructor(message: string, status: number, errors?: ApiValidationErrors) {
    super(message)
    this.name = "ApiError"
    this.status = status
    this.errors = errors
  }
}

export async function apiRequest<T>(
  path: string,
  {
    accessToken,
    errorMessage = "Request failed.",
    headers,
    unauthorizedMessage,
    ...init
  }: ApiRequestOptions = {}
): Promise<T> {
  const requestHeaders = new Headers(headers)
  requestHeaders.set("Content-Type", "application/json")

  if (accessToken) {
    requestHeaders.set("Authorization", `Bearer ${accessToken}`)
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: requestHeaders,
  })

  if (!response.ok) {
    throw await toApiError(response, errorMessage, unauthorizedMessage)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return response.json() as Promise<T>
}

async function toApiError(
  response: Response,
  errorMessage: string,
  unauthorizedMessage?: string
) {
  const problem = await readProblem(response)

  if (response.status === 401 && unauthorizedMessage) {
    return new ApiError(unauthorizedMessage, response.status, problem.errors)
  }

  return new ApiError(
    problem.message ?? problem.title ?? errorMessage,
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
