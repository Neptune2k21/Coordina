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

type SessionRejectedListener = () => void

/** Gets a new access token, or null when the session cannot be renewed. */
type SessionRenewer = () => Promise<string | null>

const sessionRejectedListeners = new Set<SessionRejectedListener>()
let sessionRenewer: SessionRenewer | null = null
let renewal: Promise<string | null> | null = null
// A request retried after a renewal is not renewed again.
const retriedRequests = new WeakSet<ApiRequestOptions>()

/**
 * Calls `listener` when a request sent with an access token gets a 401 and
 * the session cannot be renewed: it expired or was revoked. Returns a
 * function that unsubscribes. Requests without a token, like signing in,
 * never trigger it.
 */
export function onSessionRejected(listener: SessionRejectedListener) {
  sessionRejectedListeners.add(listener)

  return () => {
    sessionRejectedListeners.delete(listener)
  }
}

/**
 * Lets a 401 on an authenticated request renew the session once and retry,
 * so a short-lived access token expiring is invisible to the user. Pass null
 * to stop.
 */
export function setSessionRenewer(renewer: SessionRenewer | null) {
  sessionRenewer = renewer
}

// Requests that fail together share one renewal, so a token is refreshed once.
function renewSession() {
  if (!sessionRenewer) {
    return Promise.resolve(null)
  }

  renewal ??= sessionRenewer().finally(() => {
    renewal = null
  })

  return renewal
}

export async function apiRequest<T>(
  path: string,
  options: ApiRequestOptions = {}
): Promise<T> {
  const {
    accessToken,
    errorMessage = "Request failed.",
    headers,
    unauthorizedMessage,
    ...init
  } = options
  const requestHeaders = new Headers(headers)
  requestHeaders.set("Content-Type", "application/json")

  if (accessToken) {
    requestHeaders.set("Authorization", `Bearer ${accessToken}`)
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: requestHeaders,
  })

  if (response.status === 401 && accessToken) {
    const renewedToken = retriedRequests.has(options)
      ? null
      : await renewSession()

    if (renewedToken) {
      const retry = { ...options, accessToken: renewedToken }
      retriedRequests.add(retry)

      return apiRequest<T>(path, retry)
    }

    sessionRejectedListeners.forEach((listener) => listener())
  }

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
