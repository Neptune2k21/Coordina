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
import type { AuthSession } from "@/features/auth/auth-types"

type AuthContextValue = {
  session: AuthSession | null
  isLoading: boolean
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
        })
      })
      .catch(() => {
        clearStoredSession()
        setState({
          isLoading: false,
          session: null,
        })
      })
  }, [state.isLoading, state.session])

  const setSession = useCallback((nextSession: AuthSession) => {
    storeSession(nextSession)
    setState({
      isLoading: false,
      session: nextSession,
    })
  }, [])

  const signOut = useCallback(() => {
    clearStoredSession()
    localStorage.removeItem("coordina.activeWorkspaceId")
    setState({
      isLoading: false,
      session: null,
    })
  }, [])

  const value = useMemo(
    () => ({
      session: state.session,
      isLoading: state.isLoading,
      setSession,
      signOut,
    }),
    [setSession, signOut, state.isLoading, state.session]
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
