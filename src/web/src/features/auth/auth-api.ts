import { apiRequest, ApiError } from "@/lib/api"
import type { AuthSession } from "@/types/auth"

export { ApiError }

// The API keeps the refresh token in an HttpOnly cookie scoped to /auth, so
// these requests send and accept cookies; the access token stays in memory.
const withCookie = { credentials: "include" } as const

// The cookie endpoints require this header: browsers only add it after a
// CORS preflight, which other origins fail.
const cookieHeaders = { "X-Requested-With": "coordina-web" }

const refreshLock = "coordina.refresh"
const refreshRetryDelays = [200, 400, 800, 1600]
let refreshing: Promise<AuthSession> | null = null

export async function register(input: {
  name: string
  email: string
  password: string
}): Promise<AuthSession> {
  return apiRequest<AuthSession>("/auth/register", {
    ...withCookie,
    method: "POST",
    body: JSON.stringify(input),
    errorMessage: "Registration request failed.",
  })
}

export async function login(input: {
  email: string
  password: string
}): Promise<AuthSession> {
  return apiRequest<AuthSession>("/auth/login", {
    ...withCookie,
    method: "POST",
    body: JSON.stringify(input),
    errorMessage: "Login request failed.",
    unauthorizedMessage: "Email or password is incorrect.",
  })
}

/**
 * Exchanges the refresh cookie for a new session. Each exchange replaces the
 * cookie the next one must send, so they run one at a time: calls in this tab
 * share one, and other tabs wait for the lock.
 */
export function refreshSession(): Promise<AuthSession> {
  refreshing ??= withRefreshLock(refreshWithRetry).finally(() => {
    refreshing = null
  })

  return refreshing
}

function withRefreshLock(refresh: () => Promise<AuthSession>) {
  // Browsers only offer locks in secure contexts.
  return "locks" in navigator
    ? navigator.locks.request(refreshLock, refresh)
    : refresh()
}

// The lock cannot cover a page that reloads while its refresh is on the way:
// the next page still has the old cookie, and gets a 409 until the new one
// arrives.
async function refreshWithRetry(): Promise<AuthSession> {
  for (const delay of refreshRetryDelays) {
    try {
      return await requestRefresh()
    } catch (error) {
      if (!(error instanceof ApiError) || error.status !== 409) {
        throw error
      }
    }

    await new Promise((resolve) => setTimeout(resolve, delay))
  }

  return requestRefresh()
}

function requestRefresh() {
  return apiRequest<AuthSession>("/auth/refresh", {
    ...withCookie,
    method: "POST",
    headers: cookieHeaders,
    // Lets the new cookie arrive even if the page reloads meanwhile.
    keepalive: true,
    errorMessage: "Session could not be refreshed.",
  })
}

/** Revokes the sign-in on the server and clears the refresh cookie. */
export async function logout(): Promise<void> {
  await apiRequest<void>("/auth/logout", {
    ...withCookie,
    method: "POST",
    headers: cookieHeaders,
    errorMessage: "Sign out failed.",
  })
}
