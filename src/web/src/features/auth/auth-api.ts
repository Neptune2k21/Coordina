import type { AuthSession, CurrentUser } from "@/features/auth/auth-types"

const apiBaseUrl = import.meta.env.VITE_API_URL ?? "http://localhost:5050"

type ApiProblem = {
  title?: string
  message?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly errors?: Record<string, string[]>

  constructor(
    message: string,
    status: number,
    errors?: Record<string, string[]>
  ) {
    super(message)
    this.name = "ApiError"
    this.status = status
    this.errors = errors
  }
}

export async function register(input: {
  name: string
  email: string
  password: string
}): Promise<AuthSession> {
  return request<AuthSession>("/auth/register", {
    method: "POST",
    body: JSON.stringify(input),
  })
}

export async function login(input: {
  email: string
  password: string
}): Promise<AuthSession> {
  return request<AuthSession>("/auth/login", {
    method: "POST",
    body: JSON.stringify(input),
  })
}

export async function getCurrentUser(
  accessToken: string
): Promise<CurrentUser> {
  return request<CurrentUser>("/auth/me", {
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
  })
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...init.headers,
    },
  })

  if (!response.ok) {
    throw await toApiError(response)
  }

  return response.json() as Promise<T>
}

async function toApiError(response: Response): Promise<ApiError> {
  const problem = await readProblem(response)

  if (response.status === 401) {
    return new ApiError("Email or password is incorrect.", response.status)
  }

  return new ApiError(
    problem.message ?? problem.title ?? "Something went wrong.",
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
