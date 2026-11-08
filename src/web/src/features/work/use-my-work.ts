import { useEffect, useState } from "react"

import { useAuth } from "@/features/auth"
import { getMyWork } from "@/features/work/work-api"
import { useWorkspaces } from "@/features/workspaces"
import { ApiError } from "@/lib/api"
import type { MyWork } from "@/types/work"

/** The signed-in user's ranked work in the active workspace. */
export function useMyWork() {
  const { session } = useAuth()
  const { activeWorkspaceId } = useWorkspaces()
  const [work, setWork] = useState<MyWork | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const accessToken = session?.accessToken

  useEffect(() => {
    if (!accessToken || !activeWorkspaceId) {
      return
    }

    let isCurrent = true

    queueMicrotask(() => {
      setIsLoading(true)
      setError(null)
    })
    getMyWork(accessToken, activeWorkspaceId)
      .then((loaded) => isCurrent && setWork(loaded))
      .catch((requestError: unknown) => {
        if (!isCurrent) {
          return
        }

        setWork(null)
        setError(
          requestError instanceof ApiError
            ? requestError.message
            : "Your work could not be loaded."
        )
      })
      .finally(() => isCurrent && setIsLoading(false))

    return () => {
      isCurrent = false
    }
  }, [accessToken, activeWorkspaceId])

  return { work, isLoading, error }
}
