import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react"

import { logout, refreshSession } from "@/features/auth/auth-api"
import { onSessionRejected, setSessionRenewer } from "@/lib/api"
import type { AuthSession } from "@/types/auth"

const sessionExpiredNotice = "Your session expired. Sign in again."

type AuthContextValue = {
  session: AuthSession | null
  isLoading: boolean
  /** Why the user was signed out without asking, shown on the sign-in page. */
  notice: string | null
  setSession: (session: AuthSession) => void
  signOut: () => void
}

type AuthState = {
  isLoading: boolean
  session: AuthSession | null
  notice: string | null
}

const AuthContext = createContext<AuthContextValue | null>(null)

// Two tabs reloading together race for one refresh cookie; the loser retries
// once the winner's new cookie is in place.
async function refreshWithRetry() {
  try {
    return await refreshSession()
  } catch {
    await new Promise((resolve) => setTimeout(resolve, 300))
    return refreshSession()
  }
}

/**
 * Holds the session in memory only. The access token is never stored: on
 * load, and whenever it expires, the HttpOnly refresh cookie gets a new one.
 */
export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<AuthState>({
    isLoading: true,
    session: null,
    notice: null,
  })

  useEffect(() => {
    let isCurrent = true

    refreshWithRetry()
      .then(
        (session) =>
          isCurrent && setState({ isLoading: false, session, notice: null })
      )
      .catch(
        () =>
          isCurrent &&
          setState({ isLoading: false, session: null, notice: null })
      )

    return () => {
      isCurrent = false
    }
  }, [])

  const setSession = useCallback((nextSession: AuthSession) => {
    setState({ isLoading: false, session: nextSession, notice: null })
  }, [])

  const endSession = useCallback((notice: string | null) => {
    localStorage.removeItem("coordina.activeWorkspaceId")
    setState({ isLoading: false, session: null, notice })
  }, [])

  const signOut = useCallback(() => {
    // The server revokes the refresh token; the app signs out either way.
    void logout().catch(() => undefined)
    endSession(null)
  }, [endSession])

  // A request that gets a 401 renews the session once before giving up.
  useEffect(() => {
    setSessionRenewer(async () => {
      try {
        const renewed = await refreshWithRetry()
        setState((current) => ({ ...current, session: renewed }))
        return renewed.accessToken
      } catch {
        return null
      }
    })

    return () => setSessionRenewer(null)
  }, [])

  // When renewing fails too, the session ends once, in one place, whatever
  // screen the user is on.
  useEffect(
    () => onSessionRejected(() => endSession(sessionExpiredNotice)),
    [endSession]
  )

  const value = useMemo(
    () => ({
      session: state.session,
      isLoading: state.isLoading,
      notice: state.notice,
      setSession,
      signOut,
    }),
    [setSession, signOut, state.isLoading, state.notice, state.session]
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const context = useContext(AuthContext)

  if (!context) {
    throw new Error("useAuth must be used within AuthProvider.")
  }

  return context
}
