import type { AuthSession } from "@/features/auth/auth-types"

export const authStorageKey = "coordina.auth.session"

export function readStoredSession(): AuthSession | null {
  try {
    const rawSession = localStorage.getItem(authStorageKey)

    if (!rawSession) {
      return null
    }

    return JSON.parse(rawSession) as AuthSession
  } catch {
    localStorage.removeItem(authStorageKey)
    return null
  }
}

export function storeSession(session: AuthSession) {
  localStorage.setItem(authStorageKey, JSON.stringify(session))
}

export function clearStoredSession() {
  localStorage.removeItem(authStorageKey)
}
