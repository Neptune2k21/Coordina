import { useEffect, useState } from "react"

import * as boardApi from "@/features/board/api/board-api"
import {
  defaultPlanningPreferences,
  type PlanningPreferences,
} from "@/features/board/model/planning-preferences"
import { ApiError } from "@/lib/api"

type PlanningSettingsScope = {
  accessToken: string
  workspaceId: string
  projectId: string
  boardId: string
}

/** The signed-in user's saved planning preferences for one board. */
export function usePlanningSettings({
  accessToken,
  workspaceId,
  projectId,
  boardId,
}: PlanningSettingsScope) {
  const [preferences, setPreferences] = useState<PlanningPreferences>(
    defaultPlanningPreferences
  )
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)
  const [isSaved, setIsSaved] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let isCurrent = true

    boardApi
      .getPlanningPreferences(accessToken, workspaceId, projectId, boardId)
      .then((saved) => isCurrent && setPreferences(saved))
      .catch(
        () => isCurrent && setError("Planning preferences could not be loaded.")
      )
      .finally(() => isCurrent && setIsLoading(false))

    return () => {
      isCurrent = false
    }
  }, [accessToken, boardId, projectId, workspaceId])

  function change(next: Partial<PlanningPreferences>) {
    setPreferences((current) => ({ ...current, ...next }))
    setIsSaved(false)
  }

  async function save() {
    setIsSaving(true)
    setError(null)

    try {
      setPreferences(
        await boardApi.savePlanningPreferences(
          accessToken,
          workspaceId,
          projectId,
          boardId,
          preferences
        )
      )
      setIsSaved(true)
    } catch (requestError) {
      setError(
        requestError instanceof ApiError
          ? (Object.values(requestError.errors ?? {})[0]?.[0] ??
              requestError.message)
          : "Planning preferences could not be saved."
      )
    } finally {
      setIsSaving(false)
    }
  }

  return { preferences, isLoading, isSaving, isSaved, error, change, save }
}
