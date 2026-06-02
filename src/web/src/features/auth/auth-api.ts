import { apiRequest, ApiError } from "@/lib/api"
import type { AuthSession, CurrentUser } from "@/types/auth"

export { ApiError }

export async function register(input: {
  name: string
  email: string
  password: string
}): Promise<AuthSession> {
  return apiRequest<AuthSession>("/auth/register", {
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
    method: "POST",
    body: JSON.stringify(input),
    errorMessage: "Login request failed.",
    unauthorizedMessage: "Email or password is incorrect.",
  })
}

export async function getCurrentUser(
  accessToken: string
): Promise<CurrentUser> {
  return apiRequest<CurrentUser>("/auth/me", {
    accessToken,
    errorMessage: "Unable to load current user.",
  })
}
