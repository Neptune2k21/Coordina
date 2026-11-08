import { apiRequest, ApiError } from "@/lib/api"
import type { AuthSession } from "@/types/auth"

export { ApiError }

// The API keeps the refresh token in an HttpOnly cookie scoped to /auth, so
// these requests send and accept cookies; the access token stays in memory.
const withCookie = { credentials: "include" } as const

// The cookie endpoints require this header: browsers only add it after a
// CORS preflight, which other origins fail.
const cookieHeaders = { "X-Requested-With": "coordina-web" }

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

/** Exchanges the refresh cookie for a new session. */
export async function refreshSession(): Promise<AuthSession> {
  return apiRequest<AuthSession>("/auth/refresh", {
    ...withCookie,
    method: "POST",
    headers: cookieHeaders,
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
