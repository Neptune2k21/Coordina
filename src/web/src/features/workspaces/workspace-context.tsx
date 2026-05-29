import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react"

import { useAuth } from "@/features/auth/auth-context"
import {
  createWorkspaceInvite,
  createWorkspace,
  deleteWorkspace,
  joinWorkspace,
  listWorkspaceMembers,
  listWorkspaces,
  removeWorkspaceMember,
} from "@/features/workspaces/workspace-api"
import type {
  Workspace,
  WorkspaceInvite,
  WorkspaceMember,
} from "@/features/workspaces/workspace-types"

const activeWorkspaceStorageKey = "coordina.activeWorkspaceId"

type WorkspaceContextValue = {
  activeWorkspace: Workspace | null
  activeWorkspaceId: string | null
  error: string | null
  isLoading: boolean
  isMutating: boolean
  workspaces: Workspace[]
  create: (name: string) => Promise<Workspace>
  createInvite: (workspaceId: string) => Promise<WorkspaceInvite>
  join: (inviteCode: string) => Promise<Workspace>
  listMembers: (workspaceId: string) => Promise<WorkspaceMember[]>
  remove: (workspaceId: string) => Promise<void>
  removeMember: (workspaceId: string, memberUserId: string) => Promise<void>
  refresh: () => Promise<void>
  setActiveWorkspaceId: (workspaceId: string) => void
}

const WorkspaceContext = createContext<WorkspaceContextValue | null>(null)

export function WorkspaceProvider({ children }: { children: React.ReactNode }) {
  const { session, signOut } = useAuth()
  const [workspaces, setWorkspaces] = useState<Workspace[]>([])
  const [activeWorkspaceId, setActiveWorkspaceIdState] = useState<
    string | null
  >(() => localStorage.getItem(activeWorkspaceStorageKey))
  const [isLoading, setIsLoading] = useState(false)
  const [isMutating, setIsMutating] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const persistActiveWorkspaceId = useCallback((workspaceId: string) => {
    localStorage.setItem(activeWorkspaceStorageKey, workspaceId)
    setActiveWorkspaceIdState(workspaceId)
  }, [])

  const reconcileActiveWorkspace = useCallback(
    (nextWorkspaces: Workspace[]) => {
      const storedWorkspaceId = localStorage.getItem(activeWorkspaceStorageKey)
      const nextActiveWorkspace =
        nextWorkspaces.find(
          (workspace) => workspace.id === storedWorkspaceId
        ) ?? nextWorkspaces[0]

      if (nextActiveWorkspace) {
        persistActiveWorkspaceId(nextActiveWorkspace.id)
        return
      }

      localStorage.removeItem(activeWorkspaceStorageKey)
      setActiveWorkspaceIdState(null)
    },
    [persistActiveWorkspaceId]
  )

  const refresh = useCallback(async () => {
    if (!session) {
      setWorkspaces([])
      setActiveWorkspaceIdState(null)
      return
    }

    setIsLoading(true)
    setError(null)

    try {
      const nextWorkspaces = await listWorkspaces(session.accessToken)
      setWorkspaces(nextWorkspaces)
      reconcileActiveWorkspace(nextWorkspaces)
    } catch (requestError) {
      if (requestError instanceof Error) {
        setError(requestError.message)
      } else {
        setError("Unable to load workspaces.")
      }

      if (
        typeof requestError === "object" &&
        requestError !== null &&
        "status" in requestError &&
        requestError.status === 401
      ) {
        signOut()
      }
    } finally {
      setIsLoading(false)
    }
  }, [reconcileActiveWorkspace, session, signOut])

  useEffect(() => {
    queueMicrotask(() => void refresh())
  }, [refresh])

  const setActiveWorkspaceId = useCallback(
    (workspaceId: string) => {
      persistActiveWorkspaceId(workspaceId)
    },
    [persistActiveWorkspaceId]
  )

  const create = useCallback(
    async (name: string) => {
      if (!session) {
        throw new Error("Sign in before creating a workspace.")
      }

      setIsMutating(true)

      try {
        const workspace = await createWorkspace(session.accessToken, name)
        setWorkspaces((current) =>
          [...current, workspace].sort((a, b) => a.name.localeCompare(b.name))
        )
        persistActiveWorkspaceId(workspace.id)
        return workspace
      } finally {
        setIsMutating(false)
      }
    },
    [persistActiveWorkspaceId, session]
  )

  const join = useCallback(
    async (inviteCode: string) => {
      if (!session) {
        throw new Error("Sign in before joining a workspace.")
      }

      setIsMutating(true)

      try {
        const workspace = await joinWorkspace(session.accessToken, inviteCode)
        setWorkspaces((current) => {
          const withoutDuplicate = current.filter(
            (item) => item.id !== workspace.id
          )
          return [...withoutDuplicate, workspace].sort((a, b) =>
            a.name.localeCompare(b.name)
          )
        })
        persistActiveWorkspaceId(workspace.id)
        return workspace
      } finally {
        setIsMutating(false)
      }
    },
    [persistActiveWorkspaceId, session]
  )

  const createInvite = useCallback(
    async (workspaceId: string) => {
      if (!session) {
        throw new Error("Sign in before creating an invitation code.")
      }

      setIsMutating(true)

      try {
        return await createWorkspaceInvite(session.accessToken, workspaceId)
      } finally {
        setIsMutating(false)
      }
    },
    [session]
  )

  const listMembers = useCallback(
    async (workspaceId: string) => {
      if (!session) {
        throw new Error("Sign in before listing workspace members.")
      }

      return await listWorkspaceMembers(session.accessToken, workspaceId)
    },
    [session]
  )

  const removeMember = useCallback(
    async (workspaceId: string, memberUserId: string) => {
      if (!session) {
        throw new Error("Sign in before removing a workspace member.")
      }

      setIsMutating(true)

      try {
        await removeWorkspaceMember(
          session.accessToken,
          workspaceId,
          memberUserId
        )
      } finally {
        setIsMutating(false)
      }
    },
    [session]
  )

  const remove = useCallback(
    async (workspaceId: string) => {
      if (!session) {
        throw new Error("Sign in before deleting a workspace.")
      }

      setIsMutating(true)

      try {
        await deleteWorkspace(session.accessToken, workspaceId)
        const nextWorkspaces = workspaces.filter(
          (workspace) => workspace.id !== workspaceId
        )
        setWorkspaces(nextWorkspaces)
        reconcileActiveWorkspace(nextWorkspaces)
      } finally {
        setIsMutating(false)
      }
    },
    [reconcileActiveWorkspace, session, workspaces]
  )

  const activeWorkspace =
    workspaces.find((workspace) => workspace.id === activeWorkspaceId) ?? null

  const value = useMemo(
    () => ({
      activeWorkspace,
      activeWorkspaceId,
      create,
      createInvite,
      error,
      isLoading,
      isMutating,
      join,
      listMembers,
      refresh,
      remove,
      removeMember,
      setActiveWorkspaceId,
      workspaces,
    }),
    [
      activeWorkspace,
      activeWorkspaceId,
      create,
      createInvite,
      error,
      isLoading,
      isMutating,
      join,
      listMembers,
      refresh,
      remove,
      removeMember,
      setActiveWorkspaceId,
      workspaces,
    ]
  )

  return (
    <WorkspaceContext.Provider value={value}>
      {children}
    </WorkspaceContext.Provider>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export function useWorkspaces() {
  const context = useContext(WorkspaceContext)

  if (!context) {
    throw new Error("useWorkspaces must be used within WorkspaceProvider.")
  }

  return context
}
