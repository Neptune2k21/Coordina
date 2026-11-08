import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react"

import { getCurrentUser } from "@/features/auth/auth-api"
import {
  clearStoredSession,
  readStoredSession,
  storeSession,
} from "@/features/auth/auth-session"
import { onSessionRejected } from "@/lib/api"
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

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState(() => {
    const storedSession = readStoredSession()

    return {
      isLoading: Boolean(storedSession),
      session: storedSession,
      notice: null as string | null,
    }
  })

  useEffect(() => {
    if (!state.isLoading || !state.session) {
      return
    }

    void getCurrentUser(state.session.accessToken)
      .then((user) => {
        const refreshedSession = { ...state.session!, user }
        storeSession(refreshedSession)
        setState({
          isLoading: false,
          session: refreshedSession,
          notice: null,
        })
      })
      .catch(() => {
        clearStoredSession()
        setState((current) => ({ ...current, isLoading: false, session: null }))
      })
  }, [state.isLoading, state.session])

  const setSession = useCallback((nextSession: AuthSession) => {
    storeSession(nextSession)
    setState({
      isLoading: false,
      session: nextSession,
      notice: null,
    })
  }, [])

  const endSession = useCallback((notice: string | null) => {
    clearStoredSession()
    localStorage.removeItem("coordina.activeWorkspaceId")
    setState({
      isLoading: false,
      session: null,
      notice,
    })
  }, [])

  const signOut = useCallback(() => endSession(null), [endSession])

  // Every authenticated request reports a 401 here, so an expired session
  // ends once, in one place, whatever screen the user is on.
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
